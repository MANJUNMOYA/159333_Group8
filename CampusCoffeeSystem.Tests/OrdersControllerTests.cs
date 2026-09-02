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

        // Create a controller with a fake user and URL helper.
        private OrdersController CreateController(ApplicationDbContext context)
        {
            var httpContext = new DefaultHttpContext();

            httpContext.User = new ClaimsPrincipal(
                new ClaimsIdentity()
            );

            var controller = new OrdersController(context);

            controller.ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            };

            controller.Url = new TestUrlHelper(controller.ControllerContext);

            return controller;
        }

        [Fact]
        public async Task PlaceOrder_ValidOrder_CreatesOrder()
        {
            // Arrange
            using var context = CreateDbContext();

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

            var controller = CreateController(context);

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

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);

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

        [Fact]
        public async Task PlaceOrder_ValidOrder_ReducesStock()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Stock Test Coffee",
                Category = "Coffee",
                Price = 6.00m,
                StockQuantity = 10,
                IsActive = true
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var request = new PlaceOrderRequest
            {
                Name = "Test Customer",
                Email = "stock@example.com",
                Phone = "0211234567",
                OrderMethod = "Pickup",
                PickupTime = "1:00 PM",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 3
                    }
                }
            };

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var updatedProduct = await context.Products.FindAsync(product.Id);

            Assert.NotNull(updatedProduct);
            Assert.Equal(7, updatedProduct.StockQuantity);
        }

        [Fact]
        public async Task PlaceOrder_DeliveryOrder_AddsDeliveryFee()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Delivery Test Coffee",
                Category = "Coffee",
                Price = 5.00m,
                StockQuantity = 10,
                IsActive = true
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var request = new PlaceOrderRequest
            {
                Name = "Delivery Customer",
                Email = "delivery@example.com",
                Phone = "0211234567",
                OrderMethod = "Delivery",
                PickupTime = "2:00 PM",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 2
                    }
                }
            };

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var savedOrder = await context.CustomerOrders.SingleAsync();

            Assert.Equal(10.00m, savedOrder.Subtotal);
            Assert.Equal(3.50m, savedOrder.DeliveryFee);
            Assert.Equal(13.50m, savedOrder.Total);
        }

        [Fact]
        public async Task PlaceOrder_InsufficientStock_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Low Stock Coffee",
                Category = "Coffee",
                Price = 4.00m,
                StockQuantity = 2,
                IsActive = true
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var request = new PlaceOrderRequest
            {
                Name = "Test Customer",
                Email = "lowstock@example.com",
                Phone = "0211234567",
                OrderMethod = "Pickup",
                PickupTime = "3:00 PM",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 5
                    }
                }
            };

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.CustomerOrders);
        }

        [Fact]
        public async Task PlaceOrder_InactiveProduct_ReturnsBadRequest()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Inactive Coffee",
                Category = "Coffee",
                Price = 5.00m,
                StockQuantity = 10,
                IsActive = false
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var request = new PlaceOrderRequest
            {
                Name = "Test Customer",
                Email = "inactive@example.com",
                Phone = "0211234567",
                OrderMethod = "Pickup",
                PickupTime = "4:00 PM",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 1
                    }
                }
            };

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<BadRequestObjectResult>(result);
            Assert.Empty(context.CustomerOrders);
        }

        [Fact]
        public async Task PlaceOrder_DuplicateProductIds_CombinesQuantity()
        {
            // Arrange
            using var context = CreateDbContext();

            var product = new Product
            {
                Name = "Duplicate Test Coffee",
                Category = "Coffee",
                Price = 5.00m,
                StockQuantity = 10,
                IsActive = true
            };

            context.Products.Add(product);
            await context.SaveChangesAsync();

            var controller = CreateController(context);

            var request = new PlaceOrderRequest
            {
                Name = "Test Customer",
                Email = "duplicate@example.com",
                Phone = "0211234567",
                OrderMethod = "Pickup",
                PickupTime = "5:00 PM",
                Items = new List<PlaceOrderItemRequest>
                {
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 1
                    },
                    new PlaceOrderItemRequest
                    {
                        ProductId = product.Id,
                        Quantity = 2
                    }
                }
            };

            // Act
            var result = await controller.PlaceOrder(request);

            // Assert
            Assert.IsType<OkObjectResult>(result);

            var savedOrder = await context.CustomerOrders
                .Include(order => order.Items)
                .SingleAsync();

            Assert.Single(savedOrder.Items);
            Assert.Equal(3, savedOrder.Items.First().Quantity);
            Assert.Equal(15.00m, savedOrder.Subtotal);
            Assert.Equal(7, product.StockQuantity);
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