using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddHotelDocumentSourceSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "HotelDocumentSettings",
                columns: table => new
                {
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SourceMode = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    PreferredPdfTemplateId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HotelDocumentSettings", x => x.HotelId);
                    table.ForeignKey(
                        name: "FK_HotelDocumentSettings_PdfTemplates_PreferredPdfTemplateId",
                        column: x => x.PreferredPdfTemplateId,
                        principalTable: "PdfTemplates",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_HotelDocumentSettings_PreferredPdfTemplateId",
                table: "HotelDocumentSettings",
                column: "PreferredPdfTemplateId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HotelDocumentSettings");
        }
    }
}
