using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoffeeSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class EnsureProductReviewRatingsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Some databases applied the original review migration while it still used
            // OrderReviews.Rating and ProductReviewComments. Repair that schema without
            // deleting either overall reviews or legacy product-comment data.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[dbo].[OrderReviews]', N'Rating') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[CK_OrderReviews_Rating]', N'C') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[OrderReviews]
                            DROP CONSTRAINT [CK_OrderReviews_Rating];
                    END;

                    EXEC sp_rename N'[dbo].[OrderReviews].[Rating]', N'OverallRating', N'COLUMN';
                END;
                """);

            // Run this as a separate database command. SQL Server compiles an entire
            // command before executing it, so OverallRating must exist before the new
            // check constraint is parsed.
            migrationBuilder.Sql(
                """
                IF COL_LENGTH(N'[dbo].[OrderReviews]', N'OverallRating') IS NOT NULL
                BEGIN
                    IF OBJECT_ID(N'[dbo].[CK_OrderReviews_Rating]', N'C') IS NOT NULL
                    BEGIN
                        ALTER TABLE [dbo].[OrderReviews]
                            DROP CONSTRAINT [CK_OrderReviews_Rating];
                    END;

                    IF OBJECT_ID(N'[dbo].[CK_OrderReviews_OverallRating]', N'C') IS NULL
                    BEGIN
                        EXEC(N'ALTER TABLE [dbo].[OrderReviews]
                            ADD CONSTRAINT [CK_OrderReviews_OverallRating]
                            CHECK ([OverallRating] >= 1 AND [OverallRating] <= 5);');
                    END;
                END;
                """);

            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[ProductReviewRatings]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [dbo].[ProductReviewRatings]
                    (
                        [ProductReviewRatingId] int IDENTITY(1,1) NOT NULL,
                        [ReviewId] int NOT NULL,
                        [ProductId] int NOT NULL,
                        [Rating] int NOT NULL,
                        [CreatedAtUtc] datetime2 NOT NULL,
                        [UpdatedAtUtc] datetime2 NOT NULL,
                        CONSTRAINT [PK_ProductReviewRatings]
                            PRIMARY KEY ([ProductReviewRatingId]),
                        CONSTRAINT [CK_ProductReviewRatings_Rating]
                            CHECK ([Rating] >= 1 AND [Rating] <= 5),
                        CONSTRAINT [FK_ProductReviewRatings_OrderReviews_ReviewId]
                            FOREIGN KEY ([ReviewId]) REFERENCES [dbo].[OrderReviews] ([ReviewId])
                            ON DELETE CASCADE,
                        CONSTRAINT [FK_ProductReviewRatings_Products_ProductId]
                            FOREIGN KEY ([ProductId]) REFERENCES [dbo].[Products] ([Id])
                    );
                END;

                IF OBJECT_ID(N'[dbo].[ProductReviewRatings]', N'U') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[ProductReviewRatings]')
                         AND [name] = N'IX_ProductReviewRatings_ProductId'
                   )
                BEGIN
                    CREATE INDEX [IX_ProductReviewRatings_ProductId]
                        ON [dbo].[ProductReviewRatings] ([ProductId]);
                END;

                IF OBJECT_ID(N'[dbo].[ProductReviewRatings]', N'U') IS NOT NULL
                   AND NOT EXISTS
                   (
                       SELECT 1
                       FROM sys.indexes
                       WHERE [object_id] = OBJECT_ID(N'[dbo].[ProductReviewRatings]')
                         AND [name] = N'IX_ProductReviewRatings_ReviewId_ProductId'
                   )
                BEGIN
                    CREATE UNIQUE INDEX [IX_ProductReviewRatings_ReviewId_ProductId]
                        ON [dbo].[ProductReviewRatings] ([ReviewId], [ProductId]);
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally does not drop ProductReviewRatings. This compatibility
            // migration may run where the table was created by an earlier migration,
            // and rollback must not delete existing review data.
        }
    }
}
