using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CampusCoffeeSystem.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCateringRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CateringRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CustomerUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CustomerDisplayName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CustomerEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    EventDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EventTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    NumberOfGuests = table.Column<int>(type: "int", nullable: false),
                    Budget = table.Column<decimal>(type: "decimal(10,2)", nullable: true),
                    DietaryRequirements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    AdditionalRequirements = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CateringRequests", x => x.Id);
                    table.CheckConstraint("CK_CateringRequests_Budget", "[Budget] IS NULL OR [Budget] >= 0");
                    table.CheckConstraint("CK_CateringRequests_NumberOfGuests", "[NumberOfGuests] >= 1 AND [NumberOfGuests] <= 10000");
                    table.CheckConstraint("CK_CateringRequests_Status", "[Status] IN ('Pending', 'Accepted', 'Declined', 'Completed')");
                    table.ForeignKey(
                        name: "FK_CateringRequests_AspNetUsers_CustomerUserId",
                        column: x => x.CustomerUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CateringRequests_CustomerUserId",
                table: "CateringRequests",
                column: "CustomerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CateringRequests_Status_SubmittedAtUtc",
                table: "CateringRequests",
                columns: new[] { "Status", "SubmittedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CateringRequests");
        }
    }
}
