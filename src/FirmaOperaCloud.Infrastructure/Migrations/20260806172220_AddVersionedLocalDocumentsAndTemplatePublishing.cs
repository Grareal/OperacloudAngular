using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVersionedLocalDocumentsAndTemplatePublishing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "PdfTemplates",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "PdfTemplates",
                type: "nvarchar(12)",
                maxLength: 12,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RoomTypePrefix",
                table: "PdfTemplates",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LocalDocuments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReservationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    PdfTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Version = table.Column<int>(type: "int", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    PdfData = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    DocumentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    AttachmentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    SigningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LocalDocuments_PdfTemplates_PdfTemplateId",
                        column: x => x.PdfTemplateId,
                        principalTable: "PdfTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "LocalDocumentSignatures",
                columns: table => new
                {
                    LocalDocumentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StoredSignatureId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Role = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Position = table.Column<int>(type: "int", nullable: false),
                    Reused = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalDocumentSignatures", x => new { x.LocalDocumentId, x.StoredSignatureId });
                    table.ForeignKey(
                        name: "FK_LocalDocumentSignatures_LocalDocuments_LocalDocumentId",
                        column: x => x.LocalDocumentId,
                        principalTable: "LocalDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LocalDocumentSignatures_StoredSignatures_StoredSignatureId",
                        column: x => x.StoredSignatureId,
                        principalTable: "StoredSignatures",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LocalDocuments_HotelId_ConfirmationNumber_Version",
                table: "LocalDocuments",
                columns: new[] { "HotelId", "ConfirmationNumber", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LocalDocuments_PdfTemplateId",
                table: "LocalDocuments",
                column: "PdfTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_LocalDocumentSignatures_StoredSignatureId",
                table: "LocalDocumentSignatures",
                column: "StoredSignatureId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalDocumentSignatures");

            migrationBuilder.DropTable(
                name: "LocalDocuments");

            migrationBuilder.DropColumn(
                name: "IsDefault",
                table: "PdfTemplates");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "PdfTemplates");

            migrationBuilder.DropColumn(
                name: "RoomTypePrefix",
                table: "PdfTemplates");
        }
    }
}
