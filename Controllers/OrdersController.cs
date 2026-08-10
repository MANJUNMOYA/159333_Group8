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
        var deliveryFee = string.Equals(request.OrderMethod, "Delivery", StringComparison.OrdinalIgnoreCase) ? 3.50m : 0m;
        var order = new CustomerOrder
        {
            OrderNumber = $"CC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}",
            CustomerUserId = User.FindFirstValue(ClaimTypes.NameIdentifier),
            CustomerName = request.Name.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            Phone = request.Phone.Trim(),
            OrderMethod = request.OrderMethod.Trim(),
            PickupTime = request.PickupTime.Trim(),
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

        context.CustomerOrders.Add(order);
        await context.SaveChangesAsync();

        return Ok(new
        {
            orderNumber = order.OrderNumber,
            redirectUrl = Url.Action("OrderConfirmation", "Home", new { orderNumber = order.OrderNumber })
        });
    }
}

