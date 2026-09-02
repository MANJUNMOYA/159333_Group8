using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CampusCoffeeSystem.Tests
{
    public class MenuTests
    {
        // Create a new temporary database for each test.
        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        // Create the HomeController for Menu testing.
        private HomeController CreateController(ApplicationDbContext context)
        {
            return new HomeController(
                NullLogger<HomeController>.Instance,
                context,
                null!
            );
        }

        [Fact]
        public async Task Menu_OnlyShowsActiveProducts()
        {
            // Arrange: create a temporary database.
            using var context = CreateDbContext();

            // Add one active product and one inactive product.
            context.Products.AddRange(
                new Product
                {
                    Name = "Active Coffee",
                    Category = "Coffee",
                    Price = 5.00m,
                    StockQuantity = 10,
                    IsActive = true,
                    SortOrder = 1
                },
                new Product
                {
                    Name = "Inactive Coffee",
                    Category = "Coffee",
                    Price = 6.00m,
                    StockQuantity = 10,
                    IsActive = false,
                    SortOrder = 2
                }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act: open the Menu.
            var result = await controller.Menu();

            // Assert: the Menu should return a view.
            var viewResult = Assert.IsType<ViewResult>(result);

            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(
                viewResult.Model
            ).ToList();

            // Only the active product should appear.
            Assert.Single(products);
            Assert.Equal("Active Coffee", products[0].Name);
            Assert.True(products[0].IsActive);
        }

        [Fact]
        public async Task Menu_ProductsAreSortedBySortOrder()
        {
            // Arrange
            using var context = CreateDbContext();

            // Add products in a different order.
            context.Products.AddRange(
                new Product
                {
                    Name = "Third Coffee",
                    Category = "Coffee",
                    Price = 7.00m,
                    StockQuantity = 10,
                    IsActive = true,
                    SortOrder = 3
                },
                new Product
                {
                    Name = "First Coffee",
                    Category = "Coffee",
                    Price = 5.00m,
                    StockQuantity = 10,
                    IsActive = true,
                    SortOrder = 1
                },
                new Product
                {
                    Name = "Second Coffee",
                    Category = "Coffee",
                    Price = 6.00m,
                    StockQuantity = 10,
                    IsActive = true,
                    SortOrder = 2
                }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.Menu();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);

            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(
                viewResult.Model
            ).ToList();

            Assert.Equal(3, products.Count);

            // Products should be returned in SortOrder order.
            Assert.Equal("First Coffee", products[0].Name);
            Assert.Equal("Second Coffee", products[1].Name);
            Assert.Equal("Third Coffee", products[2].Name);

            Assert.Equal(1, products[0].SortOrder);
            Assert.Equal(2, products[1].SortOrder);
            Assert.Equal(3, products[2].SortOrder);
        }

        [Fact]
        public async Task Menu_NoActiveProducts_ReturnsEmptyList()
        {
            // Arrange
            using var context = CreateDbContext();

            // Add an inactive product only.
            context.Products.Add(
                new Product
                {
                    Name = "Unavailable Coffee",
                    Category = "Coffee",
                    Price = 5.00m,
                    StockQuantity = 10,
                    IsActive = false,
                    SortOrder = 1
                }
            );

            await context.SaveChangesAsync();

            var controller = CreateController(context);

            // Act
            var result = await controller.Menu();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);

            var products = Assert.IsAssignableFrom<IEnumerable<Product>>(
                viewResult.Model
            ).ToList();

            // The Menu should still load, but the product list should be empty.
            Assert.Empty(products);
        }
    }
}