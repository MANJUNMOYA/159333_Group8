using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/platform")]
public class PlatformController(
    ApplicationDbContext context,
    UserManager<IdentityUser> userManager) : ControllerBase
{
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

        application.Status = requestedStatus;
        application.ReviewedAtUtc = DateTime.UtcNow;
        if (user is not null)
        {
            application.UserId = user.Id;
            var hasMerchantRole = await userManager.IsInRoleAsync(user, PlatformRoles.Merchant);
            if (requestedStatus == MerchantApplicationStatuses.Approved && !hasMerchantRole)
            {
                await userManager.AddToRoleAsync(user, PlatformRoles.Merchant);
            }
            else if (requestedStatus == MerchantApplicationStatuses.Rejected && hasMerchantRole)
            {
                await userManager.RemoveFromRoleAsync(user, PlatformRoles.Merchant);
            }
        }

        await context.SaveChangesAsync();
        return Ok(new { status = requestedStatus, message = $"{application.BusinessName} is now {requestedStatus.ToLowerInvariant()}." });
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
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProduct(ProductRequest request)
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
            ImagePath = request.ImagePath.Trim(),
            SortOrder = maxSortOrder + 1,
            IsActive = true
        };
        context.Products.Add(product);
        await context.SaveChangesAsync();
        return Ok(new { product.Id, message = $"{product.Name} has been added." });
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

