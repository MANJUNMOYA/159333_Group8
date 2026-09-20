using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoffeeSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class IntegrateCoffeeAgencyData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Preserve AI history created by the previous deployment.
            migrationBuilder.Sql("""
            IF OBJECT_ID(N'[UserActivities]', N'U') IS NULL
            BEGIN
                CREATE TABLE [UserActivities] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] nvarchar(450) NOT NULL,
                    [ActivityType] nvarchar(32) NOT NULL,
                    [ProductName] nvarchar(160) NOT NULL,
                    [ProductCategory] nvarchar(80) NOT NULL,
                    [OccurredAt] datetimeoffset NOT NULL
                );
                CREATE INDEX [IX_UserActivities_UserId_OccurredAt] ON [UserActivities] ([UserId], [OccurredAt]);
            END

            IF OBJECT_ID(N'[AiChatMessages]', N'U') IS NULL
            BEGIN
                CREATE TABLE [AiChatMessages] (
                    [Id] int IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    [UserId] nvarchar(450) NOT NULL,
                    [Role] nvarchar(16) NOT NULL,
                    [Content] nvarchar(2000) NOT NULL,
                    [CreatedAt] datetimeoffset NOT NULL
                );
                CREATE INDEX [IX_AiChatMessages_UserId_CreatedAt] ON [AiChatMessages] ([UserId], [CreatedAt]);
            END
            """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiChatMessages");

            migrationBuilder.DropTable(
                name: "UserActivities");
        }
    }
}
