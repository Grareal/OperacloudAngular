using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuestCommunicationDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CommunicationDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    Source = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    SourceUrl = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    FileData = table.Column<byte[]>(type: "varbinary(max)", nullable: true),
                    DocumentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    IsPublished = table.Column<bool>(type: "bit", nullable: false),
                    IsRequired = table.Column<bool>(type: "bit", nullable: false),
                    RequiresMarketingConsent = table.Column<bool>(type: "bit", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EffectiveToUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CommunicationDocuments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "GuestEmailDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LocalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RecipientEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    Language = table.Column<string>(type: "nvarchar(12)", maxLength: 12, nullable: false),
                    MarketingConsent = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ProviderMessageId = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastAttemptAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SentAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestEmailDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestEmailDeliveries_LocalDocuments_LocalDocumentId",
                        column: x => x.LocalDocumentId,
                        principalTable: "LocalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "GuestEmailDeliveryItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    GuestEmailDeliveryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CommunicationDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DocumentType = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    DocumentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GuestEmailDeliveryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GuestEmailDeliveryItems_CommunicationDocuments_CommunicationDocumentId",
                        column: x => x.CommunicationDocumentId,
                        principalTable: "CommunicationDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GuestEmailDeliveryItems_GuestEmailDeliveries_GuestEmailDeliveryId",
                        column: x => x.GuestEmailDeliveryId,
                        principalTable: "GuestEmailDeliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDocuments_HotelId_IsPublished_Language",
                table: "CommunicationDocuments",
                columns: new[] { "HotelId", "IsPublished", "Language" });

            migrationBuilder.CreateIndex(
                name: "IX_CommunicationDocuments_HotelId_Type_Name_Language_Version",
                table: "CommunicationDocuments",
                columns: new[] { "HotelId", "Type", "Name", "Language", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveries_ConfirmationNumber",
                table: "GuestEmailDeliveries",
                column: "ConfirmationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveries_LocalDocumentId",
                table: "GuestEmailDeliveries",
                column: "LocalDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveries_Status",
                table: "GuestEmailDeliveries",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveryItems_CommunicationDocumentId",
                table: "GuestEmailDeliveryItems",
                column: "CommunicationDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveryItems_GuestEmailDeliveryId",
                table: "GuestEmailDeliveryItems",
                column: "GuestEmailDeliveryId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GuestEmailDeliveryItems");

            migrationBuilder.DropTable(
                name: "CommunicationDocuments");

            migrationBuilder.DropTable(
                name: "GuestEmailDeliveries");
        }
    }
}
