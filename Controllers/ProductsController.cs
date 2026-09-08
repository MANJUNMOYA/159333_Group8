using CampusCoffeeSystem.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CampusCoffeeSystem.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(ApplicationDbContext context) : ControllerBase
{
    [HttpGet("availability")]
    public async Task<IActionResult> GetAvailability([FromQuery] int[] ids)
    {
        var productIds = ids
            .Where(id => id > 0)
            .Distinct()
            .Take(100)
            .ToArray();

        if (productIds.Length == 0)
        {
            return Ok(Array.Empty<object>());
        }

        var products = await context.Products
            .AsNoTracking()
            .Where(product => productIds.Contains(product.Id))
            .Select(product => new
            {
                product.Id,
                product.StockQuantity,
                product.IsActive
            })
            .ToListAsync();

        return Ok(products);
    }
}
