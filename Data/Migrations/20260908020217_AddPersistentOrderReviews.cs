using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoffeeSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPersistentOrderReviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "OrderReviews",
                columns: table => new
                {
                    ReviewId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    OrderId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CustomerDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    OverallRating = table.Column<int>(type: "int", nullable: false),
                    OverallComment = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsAnonymous = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false, defaultValue: "Active"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OrderReviews", x => x.ReviewId);
                    table.CheckConstraint("CK_OrderReviews_OverallRating", "[OverallRating] >= 1 AND [OverallRating] <= 5");
                    table.ForeignKey(
                        name: "FK_OrderReviews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OrderReviews_CustomerOrders_OrderId",
                        column: x => x.OrderId,
                        principalTable: "CustomerOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProductReviewRatings",
                columns: table => new
                {
                    ProductReviewRatingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReviewId = table.Column<int>(type: "int", nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    Rating = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductReviewRatings", x => x.ProductReviewRatingId);
                    table.CheckConstraint("CK_ProductReviewRatings_Rating", "[Rating] >= 1 AND [Rating] <= 5");
                    table.ForeignKey(
                        name: "FK_ProductReviewRatings_OrderReviews_ReviewId",
                        column: x => x.ReviewId,
                        principalTable: "OrderReviews",
                        principalColumn: "ReviewId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductReviewRatings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_OrderId",
                table: "OrderReviews",
                column: "OrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_Status_CreatedAtUtc",
                table: "OrderReviews",
                columns: new[] { "Status", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_OrderReviews_UserId",
                table: "OrderReviews",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewRatings_ProductId",
                table: "ProductReviewRatings",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductReviewRatings_ReviewId_ProductId",
                table: "ProductReviewRatings",
                columns: new[] { "ReviewId", "ProductId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductReviewRatings");

            migrationBuilder.DropTable(
                name: "OrderReviews");
        }
    }
}
