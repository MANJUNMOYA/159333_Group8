using CampusCoffeeSystem.Controllers;
using CampusCoffeeSystem.Data;
using CampusCoffeeSystem.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace CampusCoffeeSystem.Tests
{
    public class OrdersControllerTests
    {
        // Create a new temporary database for each test.
        private ApplicationDbContext CreateDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task PlaceOrder_ValidOrder_CreatesOrder()
        {
            // Arrange: create a temporary database.
            using var context = CreateDbContext();

            // Add one product for this test.
            var product = new Product
            {
                Name = "Test Coffee",
                Category = "Coffee",
                Price = 5.00m,
                StockQuantity = 10,
                IsActive = true
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            // Create a fake HTTP context for the controller.
            var httpContext = new DefaultHttpContext();

            // Add an empty test user.
            httpContext.User = new ClaimsPrincipal(new ClaimsIdentity());

            // Create the controller with the test database.
            var controller = new OrdersController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            // Add a fake URL helper because PlaceOrder creates a redirect URL.
            controller.Url = new TestUrlHelper(controller.ControllerContext);

            // Create a valid pickup order.
            var request = new PlaceOrderRequest
            {
                Name = "Test Customer",
                Email = "test@example.com",
                Phone = "0211234567",
                OrderMethod = "Pickup",
                PickupTime = "12:00 PM",
                SpecialNotes = "No sugar",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 2
                    }
                }
            };

            // Act: place the order.
            var result = await controller.PlaceOrder(request);

            // Assert: the request should be successful.
            Assert.IsType<OkObjectResult>(result);

            // Check that the order was saved in the database.
            var savedOrder = await context.CustomerOrders
                .Include(order => order.Items)
                .SingleAsync();

            Assert.Equal("Test Customer", savedOrder.CustomerName);
            Assert.Equal("test@example.com", savedOrder.Email);
            Assert.Equal(10.00m, savedOrder.Subtotal);
            Assert.Equal(10.00m, savedOrder.Total);
            Assert.Single(savedOrder.Items);
            Assert.Equal(2, savedOrder.Items.First().Quantity);
        }

        // This fake URL helper is only used for controller testing.
        private class TestUrlHelper : IUrlHelper
        {
            public ActionContext ActionContext { get; }

            public TestUrlHelper(ActionContext actionContext)
            {
                ActionContext = actionContext;
            }

            public string? Action(UrlActionContext actionContext)
            {
                return "/Home/OrderConfirmation";
            }

            public string? Content(string? contentPath)
            {
                return contentPath;
            }

            public bool IsLocalUrl(string? url)
            {
                return true;
            }

            public string? Link(string? routeName, object? values)
            {
                return "/Home/OrderConfirmation";
            }

            public string? RouteUrl(UrlRouteContext routeContext)
            {
                return "/Home/OrderConfirmation";
            }
        }
    }
}