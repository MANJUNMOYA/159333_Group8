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
        public DbSet<OrderReview> OrderReviews => Set<OrderReview>();
        public DbSet<ProductReviewRating> ProductReviewRatings => Set<ProductReviewRating>();
        public DbSet<CustomerProfile> CustomerProfiles => Set<CustomerProfile>();

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

            builder.Entity<CustomerProfile>(profile =>
            {
                profile.HasKey(item => item.UserId);
                profile.HasOne(item => item.User)
                    .WithOne()
                    .HasForeignKey<CustomerProfile>(item => item.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            builder.Entity<OrderItem>()
                .HasOne(item => item.CustomerOrder)
                .WithMany(order => order.Items)
                .HasForeignKey(item => item.CustomerOrderId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Entity<OrderReview>(review =>
            {
                review.HasKey(item => item.ReviewId);
                review.HasIndex(item => item.OrderId).IsUnique();
                review.HasIndex(item => item.UserId);
                review.HasIndex(item => new { item.Status, item.CreatedAtUtc });
                review.Property(item => item.IsAnonymous).HasDefaultValue(false);
                review.Property(item => item.Status).HasDefaultValue(ReviewStatuses.Active);
                review.ToTable(table => table.HasCheckConstraint(
                    "CK_OrderReviews_OverallRating",
                    "[OverallRating] >= 1 AND [OverallRating] <= 5"));

                review.HasOne(item => item.Order)
                    .WithOne(order => order.Review)
                    .HasForeignKey<OrderReview>(item => item.OrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                review.HasOne(item => item.User)
                    .WithMany()
                    .HasForeignKey(item => item.UserId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            builder.Entity<ProductReviewRating>(rating =>
            {
                rating.HasKey(item => item.ProductReviewRatingId);
                rating.HasIndex(item => new { item.ReviewId, item.ProductId }).IsUnique();
                rating.HasIndex(item => item.ProductId);
                rating.ToTable(table => table.HasCheckConstraint(
                    "CK_ProductReviewRatings_Rating",
                    "[Rating] >= 1 AND [Rating] <= 5"));

                rating.HasOne(item => item.Review)
                    .WithMany(review => review.ProductRatings)
                    .HasForeignKey(item => item.ReviewId)
                    .OnDelete(DeleteBehavior.Cascade);

                rating.HasOne(item => item.Product)
                    .WithMany()
                    .HasForeignKey(item => item.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);
            });
        }
    }
}
