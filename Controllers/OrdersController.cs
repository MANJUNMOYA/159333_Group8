using System.Security.Claims;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController(ApplicationDbContext context) : ControllerBase
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> PlaceOrder(PlaceOrderRequest request)
    {
        var orderMethod = request.OrderMethod.Trim();
        var isDelivery = string.Equals(orderMethod, "Delivery", StringComparison.OrdinalIgnoreCase);
        var isPickup = string.Equals(orderMethod, "Pickup", StringComparison.OrdinalIgnoreCase);
        if (!isDelivery && !isPickup)
        {
            return BadRequest(new { message = "Choose Pickup or Delivery." });
        }

        if (isDelivery &&
            (string.IsNullOrWhiteSpace(request.AddressLine1) ||
             string.IsNullOrWhiteSpace(request.City) ||
             string.IsNullOrWhiteSpace(request.Postcode)))
        {
            return BadRequest(new { message = "Address Line 1, city and postcode are required for delivery." });
        }

        var requestedItems = request.Items
            .GroupBy(item => item.ProductId)
            .ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
        var products = await context.Products
            .Where(product => requestedItems.Keys.Contains(product.Id) && product.IsActive)
            .ToDictionaryAsync(product => product.Id);

        if (products.Count != requestedItems.Count)
        {
            return BadRequest(new { message = "One or more products are no longer available." });
        }

        foreach (var requestedItem in requestedItems)
        {
            if (products[requestedItem.Key].StockQuantity < requestedItem.Value)
            {
                return BadRequest(new { message = $"There is not enough stock for {products[requestedItem.Key].Name}." });
            }
        }

        var orderItems = requestedItems.Select(item =>
        {
            var product = products[item.Key];
            return new OrderItem
            {
                ProductId = product.Id,
                ProductName = product.Name,
                UnitPrice = product.Price,
                Quantity = item.Value
            };
        }).ToList();

        var subtotal = orderItems.Sum(item => item.UnitPrice * item.Quantity);
        var deliveryFee = isDelivery ? 3.50m : 0m;
        var customerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var order = new CustomerOrder
        {
            OrderNumber = $"pending_{Guid.NewGuid():N}",
            CustomerUserId = customerUserId,
            CustomerName = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone.Trim(),
            OrderMethod = isDelivery ? "Delivery" : "Pickup",
            DeliveryAddressLine1 = isDelivery ? request.AddressLine1.Trim() : null,
            DeliveryAddressLine2 = isDelivery ? NullIfWhiteSpace(request.AddressLine2) : null,
            DeliveryCity = isDelivery ? request.City.Trim() : null,
            DeliveryPostcode = isDelivery ? request.Postcode.Trim() : null,
            SpecialNotes = request.SpecialNotes.Trim(),
            Subtotal = subtotal,
            DeliveryFee = deliveryFee,
            Total = subtotal + deliveryFee,
            Items = orderItems
        };

        foreach (var requestedItem in requestedItems)
        {
            products[requestedItem.Key].StockQuantity -= requestedItem.Value;
        }

        if (isDelivery &&
            !string.IsNullOrWhiteSpace(customerUserId) &&
            User.IsInRole(PlatformRoles.Customer))
        {
            var profile = await context.CustomerProfiles
                .FirstOrDefaultAsync(item => item.UserId == customerUserId);
            var shouldSaveAddress = profile?.HasDefaultAddress != true || request.SaveDeliveryAddressAsDefault;
            if (shouldSaveAddress)
            {
                profile ??= new CustomerProfile { UserId = customerUserId };
                if (context.Entry(profile).State == EntityState.Detached)
                {
                    context.CustomerProfiles.Add(profile);
                }

                profile.AddressLine1 = order.DeliveryAddressLine1;
                profile.AddressLine2 = order.DeliveryAddressLine2;
                profile.City = order.DeliveryCity;
                profile.Postcode = order.DeliveryPostcode;
                profile.UpdatedAtUtc = DateTime.UtcNow;
            }
        }

        await using var transaction = await context.Database.BeginTransactionAsync();
        context.CustomerOrders.Add(order);
        await context.SaveChangesAsync();

        order.OrderNumber = $"cc_{order.Id + 1000}";
        await context.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new
        {
            orderNumber = order.OrderNumber,
            redirectUrl = Url.Action("OrderConfirmation", "Home", new { orderNumber = order.OrderNumber })
        });
    }

    private static string? NullIfWhiteSpace(string value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}

