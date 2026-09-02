using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CampusCoffeeSystem.Tests
{
    public class ProductDetailsTests
    {
        // Create a new temporary database for each test.
        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        // Create the HomeController for product details testing.
        private HomeController CreateController(ApplicationDbContext context)
        {
            return new HomeController(
                NullLogger<HomeController>.Instance,
                context,
                null!
            );
        }

        [Fact]
        public async Task ProductDetails_ActiveProduct_ReturnsProduct()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Test Latte",
                Category = "Coffee",
                Description = "A test latte for product details.",
                Price = 6.50m,
                StockQuantity = 10,
                IsActive = true,
                SortOrder = 1
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.ProductDetails(product.Id);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);

            var returnedProduct = Assert.IsType<Product>(
                viewResult.Model
            );

            Assert.Equal(product.Id, returnedProduct.Id);
            Assert.Equal("Test Latte", returnedProduct.Name);
            Assert.Equal(6.50m, returnedProduct.Price);
            Assert.Equal(10, returnedProduct.StockQuantity);
            Assert.True(returnedProduct.IsActive);
        }

        [Fact]
        public async Task ProductDetails_ProductDoesNotExist_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateDbContext();

            var controller = CreateController(context);

            // Act
            var result = await controller.ProductDetails(999);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }

        [Fact]
        public async Task ProductDetails_InactiveProduct_ReturnsNotFound()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Inactive Latte",
                Category = "Coffee",
                Description = "This product is not available.",
                Price = 5.50m,
                StockQuantity = 10,
                IsActive = false,
                SortOrder = 1
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.ProductDetails(product.Id);

            // Assert
            Assert.IsType<NotFoundResult>(result);
        }
    }
}