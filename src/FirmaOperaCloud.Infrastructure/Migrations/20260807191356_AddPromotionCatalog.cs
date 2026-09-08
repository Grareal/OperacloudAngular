using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionCatalog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PromotionCatalogEntries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    OperaCode = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    OperaDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    GuestTitle = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    GuestDescription = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsApprovedForGuest = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Source = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PromotionCatalogEntries", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCatalogEntries_HotelId_IsActive_IsApprovedForGuest",
                table: "PromotionCatalogEntries",
                columns: new[] { "HotelId", "IsActive", "IsApprovedForGuest" });

            migrationBuilder.CreateIndex(
                name: "IX_PromotionCatalogEntries_HotelId_OperaCode_Language",
                table: "PromotionCatalogEntries",
                columns: new[] { "HotelId", "OperaCode", "Language" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PromotionCatalogEntries");
        }
    }
}
