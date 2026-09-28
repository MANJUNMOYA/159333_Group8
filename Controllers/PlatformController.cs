using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using CampusCoffeeSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/platform")]
public class PlatformController(
    ApplicationDbContext context,
    UserManager<IdentityUser> userManager,
    IWebHostEnvironment environment,
    ILogger<PlatformController> logger,
    NotificationService notifications) : ControllerBase
{
    private const long MaximumProductImageBytes = 5 * 1024 * 1024;
    private const string DefaultProductImagePath = "/images/menu/Campus Flat White.jpg";

    [Authorize(Roles = PlatformRoles.Administrator)]
    [HttpPost("merchant-applications/{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateMerchantApplication(int id, StatusUpdateRequest request)
    {
        var requestedStatus = new[] { MerchantApplicationStatuses.Approved, MerchantApplicationStatuses.Rejected }
            .FirstOrDefault(status => string.Equals(status, request.Status, StringComparison.OrdinalIgnoreCase));
        if (requestedStatus is null)
        {
            return BadRequest(new { message = "Merchant status must be Approved or Rejected." });
        }

        var application = await context.MerchantApplications.FindAsync(id);
        if (application is null)
        {
            return NotFound(new { message = "Merchant application was not found." });
        }

        var user = !string.IsNullOrWhiteSpace(application.UserId)
            ? await userManager.FindByIdAsync(application.UserId)
            : await userManager.FindByEmailAsync(application.Email);
        if (requestedStatus == MerchantApplicationStatuses.Approved && user is null)
        {
            return BadRequest(new { message = "The applicant must create a merchant account before approval." });
        }

        var statusChanged = application.Status != requestedStatus;
        await using var transaction = await context.Database.BeginTransactionAsync();
        application.Status = requestedStatus;
        application.ReviewedAtUtc = DateTime.UtcNow;
        if (user is not null)
        {
            application.UserId = user.Id;
            var hasMerchantRole = await userManager.IsInRoleAsync(user, PlatformRoles.Merchant);
            var roleResult = IdentityResult.Success;
            if (requestedStatus == MerchantApplicationStatuses.Approved && !hasMerchantRole)
            {
                roleResult = await userManager.AddToRoleAsync(user, PlatformRoles.Merchant);
            }
            else if (requestedStatus == MerchantApplicationStatuses.Rejected && hasMerchantRole)
            {
                roleResult = await userManager.RemoveFromRoleAsync(user, PlatformRoles.Merchant);
            }
            if (!roleResult.Succeeded)
                return BadRequest(new { message = "The merchant role could not be updated. Please try again." });
        }

        await context.SaveChangesAsync();
        await transaction.CommitAsync();
        var delivery = statusChanged ? await notifications.MerchantDecisionAsync(application) : NotificationDelivery.Skipped;
        return Ok(new
        {
            status = requestedStatus,
            message = ($"{application.BusinessName} is now {requestedStatus.ToLowerInvariant()}. "
                + NotificationService.DeliveryMessage(delivery, "The applicant has been notified by email.")).Trim(),
            notificationStatus = delivery.ToString().ToLowerInvariant()
        });
    }

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("orders/{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateOrderStatus(int id, StatusUpdateRequest request)
    {
        var allowed = new[] { OrderStatuses.Preparing, OrderStatuses.Ready, OrderStatuses.Completed, OrderStatuses.Cancelled };
        var requestedStatus = allowed.FirstOrDefault(status => string.Equals(status, request.Status, StringComparison.OrdinalIgnoreCase));
        if (requestedStatus is null)
        {
            return BadRequest(new { message = "The requested order status is not supported." });
        }

        var order = await context.CustomerOrders.FindAsync(id);
        if (order is null)
        {
            return NotFound(new { message = "Order was not found." });
        }

        order.Status = requestedStatus;
        await context.SaveChangesAsync();
        return Ok(new { status = requestedStatus, message = $"Order {order.OrderNumber} is now {requestedStatus.ToLowerInvariant()}." });
    }

    [Authorize(Roles = PlatformRoles.Administrator)]
    [HttpPost("orders/{id:int}/archive")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ArchiveOrder(int id)
    {
        var order = await context.CustomerOrders.FindAsync(id);
        if (order is null)
        {
            return NotFound(new { message = "Order was not found." });
        }

        order.IsArchived = true;
        await context.SaveChangesAsync();
        return Ok(new { message = $"Order {order.OrderNumber} has been archived." });
    }

    [Authorize(Roles = PlatformRoles.Administrator)]
    [HttpPost("reviews/{id:int}/status")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateReviewStatus(int id, StatusUpdateRequest request)
    {
        var requestedStatus = new[] { ReviewStatuses.Active, ReviewStatuses.Hidden }
            .FirstOrDefault(status => string.Equals(status, request.Status, StringComparison.OrdinalIgnoreCase));
        if (requestedStatus is null)
        {
            return BadRequest(new { message = "Review status must be Active or Hidden." });
        }

        var review = await context.OrderReviews.FindAsync(id);
        if (review is null)
        {
            return NotFound(new { message = "Review was not found." });
        }

        review.Status = requestedStatus;
        review.UpdatedAtUtc = DateTime.UtcNow;
        await context.SaveChangesAsync();
        return Ok(new
        {
            status = requestedStatus,
            message = $"Review {review.ReviewId} is now {requestedStatus.ToLowerInvariant()}."
        });
    }

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("products")]
    [Consumes("application/json")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddProduct([FromBody] ProductRequest request) =>
        CreateProduct(request, image: null);

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("products")]
    [Consumes("multipart/form-data")]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> AddProductWithImage(
        [FromForm] ProductRequest request,
        [FromForm] IFormFile? image) =>
        CreateProduct(request, image);

    private async Task<IActionResult> CreateProduct(ProductRequest request, IFormFile? image)
    {
        if (await context.Products.AnyAsync(product => product.Name == request.Name.Trim()))
        {
            return Conflict(new { message = "A product with this name already exists." });
        }

        var maxSortOrder = await context.Products.Select(product => (int?)product.SortOrder).MaxAsync() ?? 0;
        var product = new Product
        {
            Name = request.Name.Trim(),
            Category = request.Category.Trim().ToLowerInvariant(),
            DisplayCategory = string.IsNullOrWhiteSpace(request.DisplayCategory) ? request.Category.Trim() : request.DisplayCategory.Trim(),
            Description = request.Description.Trim(),
            Price = request.Price,
            StockQuantity = request.StockQuantity,
            ImagePath = string.IsNullOrWhiteSpace(request.ImagePath) ? DefaultProductImagePath : request.ImagePath.Trim(),
            SortOrder = maxSortOrder + 1,
            IsActive = true
        };

        string? savedImageFilePath = null;
        if (image is not null)
        {
            if (image.Length == 0 || image.Length > MaximumProductImageBytes)
            {
                return BadRequest(new { message = "Choose a JPG, PNG or WebP image no larger than 5 MB." });
            }

            string? imageExtension;
            try
            {
                imageExtension = await GetVerifiedProductImageExtension(image);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "A product image could not be read.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product image could not be read. Try again." });
            }

            if (imageExtension is null)
            {
                return BadRequest(new { message = "The product image must be a valid JPG, PNG or WebP image." });
            }

            try
            {
                var savedImage = await SaveProductImage(image, imageExtension);
                product.ImagePath = savedImage.PublicPath;
                savedImageFilePath = savedImage.FilePath;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "A product image could not be stored.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product image could not be stored. Try again." });
            }
        }

        try
        {
            context.Products.Add(product);
            await context.SaveChangesAsync();
        }
        catch (OperationCanceledException)
        {
            DeleteProductImage(savedImageFilePath);
            throw;
        }
        catch (Exception exception)
        {
            DeleteProductImage(savedImageFilePath);
            logger.LogError(exception, "Product {ProductName} could not be created.", product.Name);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product could not be saved. Try again." });
        }

        return Ok(new { product.Id, message = $"{product.Name} has been added." });
    }

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("products/{id:int}")]
    [Consumes("multipart/form-data")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateProduct(
        int id,
        [FromForm] ProductRequest request,
        [FromForm] IFormFile? image)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound(new { message = "Product was not found." });
        }

        var productName = request.Name.Trim();
        if (await context.Products.AnyAsync(item => item.Id != id && item.Name == productName))
        {
            return Conflict(new { message = "A product with this name already exists." });
        }

        string? savedImageFilePath = null;
        string? replacementImagePath = null;
        if (image is not null)
        {
            if (image.Length == 0 || image.Length > MaximumProductImageBytes)
            {
                return BadRequest(new { message = "Choose a JPG, PNG or WebP image no larger than 5 MB." });
            }

            string? imageExtension;
            try
            {
                imageExtension = await GetVerifiedProductImageExtension(image);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "A replacement product image could not be read.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The replacement image could not be read. Try again." });
            }

            if (imageExtension is null)
            {
                return BadRequest(new { message = "The replacement image must be a valid JPG, PNG or WebP image." });
            }

            try
            {
                var savedImage = await SaveProductImage(image, imageExtension);
                replacementImagePath = savedImage.PublicPath;
                savedImageFilePath = savedImage.FilePath;
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                logger.LogError(exception, "A replacement product image could not be stored.");
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The replacement image could not be stored. Try again." });
            }
        }

        var productCategory = request.Category.Trim().ToLowerInvariant();
        var displayCategory = string.IsNullOrWhiteSpace(request.DisplayCategory)
            ? char.ToUpperInvariant(productCategory[0]) + productCategory[1..]
            : request.DisplayCategory.Trim();
        var previousImagePath = product.ImagePath;
        product.Name = productName;
        product.Category = productCategory;
        product.DisplayCategory = displayCategory;
        product.Description = request.Description.Trim();
        product.Price = request.Price;
        product.StockQuantity = request.StockQuantity;
        if (replacementImagePath is not null)
        {
            product.ImagePath = replacementImagePath;
        }

        try
        {
            await context.SaveChangesAsync();
        }
        catch (OperationCanceledException)
        {
            DeleteProductImage(savedImageFilePath);
            throw;
        }
        catch (Exception exception)
        {
            DeleteProductImage(savedImageFilePath);
            logger.LogError(exception, "Product {ProductId} could not be updated.", product.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product changes could not be saved. Try again." });
        }

        if (replacementImagePath is not null)
        {
            await DeleteManagedProductImageIfUnreferenced(previousImagePath, product.Id);
        }

        return Ok(new
        {
            product.Id,
            product.Name,
            product.Category,
            product.DisplayCategory,
            product.Description,
            product.Price,
            product.StockQuantity,
            product.ImagePath,
            message = $"{product.Name} has been updated."
        });
    }

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("products/{id:int}/delete")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteProduct(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound(new { message = "Product was not found." });
        }

        var hasOrderHistory = await context.OrderItems
            .AsNoTracking()
            .AnyAsync(item => item.ProductId == id);
        var hasReviewHistory = await context.ProductReviewRatings
            .AsNoTracking()
            .AnyAsync(rating => rating.ProductId == id);

        if (hasOrderHistory || hasReviewHistory)
        {
            product.IsActive = false;
            try
            {
                await context.SaveChangesAsync();
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Product {ProductId} could not be deactivated during deletion.", product.Id);
                return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product could not be deactivated. Try again." });
            }

            return Ok(new
            {
                permanentlyDeleted = false,
                deactivated = true,
                isActive = product.IsActive,
                message = $"{product.Name} has historical order or review data, so it was retained and deactivated instead of permanently deleted."
            });
        }

        var imagePath = product.ImagePath;
        context.Products.Remove(product);
        try
        {
            await context.SaveChangesAsync();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            logger.LogError(exception, "Product {ProductId} could not be permanently deleted.", product.Id);
            return StatusCode(StatusCodes.Status500InternalServerError, new { message = "The product could not be deleted. Try again." });
        }

        await DeleteManagedProductImageIfUnreferenced(imagePath, id);
        return Ok(new
        {
            permanentlyDeleted = true,
            deactivated = false,
            message = $"{product.Name} has been permanently deleted."
        });
    }

    private static async Task<string?> GetVerifiedProductImageExtension(IFormFile image)
    {
        var contentType = image.ContentType.Trim().ToLowerInvariant();
        if (contentType is not ("image/jpeg" or "image/jpg" or "image/png" or "image/webp"))
        {
            return null;
        }

        var header = new byte[12];
        await using var stream = image.OpenReadStream();
        var bytesRead = await stream.ReadAsync(header.AsMemory(0, header.Length));

        if (bytesRead >= 3 &&
            header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF &&
            contentType is "image/jpeg" or "image/jpg")
        {
            return ".jpg";
        }

        if (bytesRead >= 8 &&
            header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) &&
            contentType == "image/png")
        {
            return ".png";
        }

        if (bytesRead >= 12 &&
            header.AsSpan(0, 4).SequenceEqual("RIFF"u8) &&
            header.AsSpan(8, 4).SequenceEqual("WEBP"u8) &&
            contentType == "image/webp")
        {
            return ".webp";
        }

        return null;
    }

    private async Task<(string PublicPath, string FilePath)> SaveProductImage(IFormFile image, string extension)
    {
        var uploadDirectory = GetProductUploadDirectory();
        Directory.CreateDirectory(uploadDirectory);

        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadDirectory, fileName);
        try
        {
            await using var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            await image.CopyToAsync(output, HttpContext.RequestAborted);
        }
        catch
        {
            DeleteProductImage(filePath);
            throw;
        }

        return ($"/uploads/products/{fileName}", filePath);
    }

    private string GetProductUploadDirectory()
    {
        var webRoot = environment.WebRootPath ?? Path.Combine(environment.ContentRootPath, "wwwroot");
        return Path.Combine(webRoot, "uploads", "products");
    }

    private async Task DeleteManagedProductImageIfUnreferenced(string imagePath, int excludedProductId)
    {
        if (!TryGetManagedProductImageFilePath(imagePath, out var filePath))
        {
            return;
        }

        try
        {
            var isShared = await context.Products
                .AsNoTracking()
                .AnyAsync(product => product.Id != excludedProductId && product.ImagePath == imagePath);
            if (isShared)
            {
                return;
            }
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not verify whether product image {ImagePath} is shared.", imagePath);
            return;
        }

        DeleteProductImage(filePath);
    }

    private bool TryGetManagedProductImageFilePath(string imagePath, out string filePath)
    {
        filePath = string.Empty;
        const string managedPrefix = "/uploads/products/";
        if (string.IsNullOrWhiteSpace(imagePath) ||
            !imagePath.StartsWith(managedPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var fileName = imagePath[managedPrefix.Length..];
        if (fileName.Length == 0 || fileName.Contains('/') || fileName.Contains('\\'))
        {
            return false;
        }

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        var identifier = Path.GetFileNameWithoutExtension(fileName);
        if (extension is not (".jpg" or ".png" or ".webp") ||
            !Guid.TryParseExact(identifier, "N", out _))
        {
            return false;
        }

        var uploadDirectory = Path.GetFullPath(GetProductUploadDirectory());
        var candidatePath = Path.GetFullPath(Path.Combine(uploadDirectory, fileName));
        var requiredPrefix = uploadDirectory.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (!candidatePath.StartsWith(requiredPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        filePath = candidatePath;
        return true;
    }

    private void DeleteProductImage(string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        try
        {
            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            logger.LogWarning(exception, "A managed product image could not be removed.");
        }
    }

    [Authorize(Roles = PlatformRoles.Merchant)]
    [HttpPost("products/{id:int}/stock")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStock(int id, StockUpdateRequest request)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound(new { message = "Product was not found." });
        }

        product.StockQuantity = request.StockQuantity;
        await context.SaveChangesAsync();
        return Ok(new { stockQuantity = product.StockQuantity, message = $"{product.Name} stock has been updated." });
    }

    [Authorize(Roles = $"{PlatformRoles.Merchant},{PlatformRoles.Administrator}")]
    [HttpPost("products/{id:int}/availability")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleProductAvailability(int id)
    {
        var product = await context.Products.FindAsync(id);
        if (product is null)
        {
            return NotFound(new { message = "Product was not found." });
        }

        product.IsActive = !product.IsActive;
        await context.SaveChangesAsync();
        return Ok(new { isActive = product.IsActive, message = $"{product.Name} is now {(product.IsActive ? "active" : "disabled")}." });
    }
}

