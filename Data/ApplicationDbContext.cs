using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using CampusCoffeeSystem.Models;

namespace CampusCoffeeSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<MerchantApplication> MerchantApplications => Set<MerchantApplication>();
        public DbSet<Product> Products => Set<Product>();
        public DbSet<CustomerOrder> CustomerOrders => Set<CustomerOrder>();
        public DbSet<OrderItem> OrderItems => Set<OrderItem>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<MerchantApplication>()
                .HasIndex(application => application.Email);

            builder.Entity<Product>()
                .HasIndex(product => product.Name)
                .IsUnique();

            builder.Entity<CustomerOrder>()
                .HasIndex(order => order.OrderNumber)
                .IsUnique();

            builder.Entity<OrderItem>()
                .HasOne(item => item.CustomerOrder)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.CustomerOrderId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
