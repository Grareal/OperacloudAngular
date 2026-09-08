using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPromotionPdfTemplates : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PdfTemplates_HotelId_Name_Version",
                table: "PdfTemplates");

            migrationBuilder.AddColumn<string>(
                name: "TemplateType",
                table: "PdfTemplates",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "RegistrationCard");

            migrationBuilder.AddColumn<string>(
                name: "ContentType",
                table: "GuestEmailDeliveryItems",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<byte[]>(
                name: "GeneratedFileData",
                table: "GuestEmailDeliveryItems",
                type: "varbinary(max)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PdfTemplateId",
                table: "GuestEmailDeliveryItems",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplates_HotelId_TemplateType_Name_Version",
                table: "PdfTemplates",
                columns: new[] { "HotelId", "TemplateType", "Name", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_GuestEmailDeliveryItems_PdfTemplateId",
                table: "GuestEmailDeliveryItems",
                column: "PdfTemplateId");

            migrationBuilder.AddForeignKey(
                name: "FK_GuestEmailDeliveryItems_PdfTemplates_PdfTemplateId",
                table: "GuestEmailDeliveryItems",
                column: "PdfTemplateId",
                principalTable: "PdfTemplates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_GuestEmailDeliveryItems_PdfTemplates_PdfTemplateId",
                table: "GuestEmailDeliveryItems");

            migrationBuilder.DropIndex(
                name: "IX_PdfTemplates_HotelId_TemplateType_Name_Version",
                table: "PdfTemplates");

            migrationBuilder.DropIndex(
                name: "IX_GuestEmailDeliveryItems_PdfTemplateId",
                table: "GuestEmailDeliveryItems");

            migrationBuilder.DropColumn(
                name: "TemplateType",
                table: "PdfTemplates");

            migrationBuilder.DropColumn(
                name: "ContentType",
                table: "GuestEmailDeliveryItems");

            migrationBuilder.DropColumn(
                name: "GeneratedFileData",
                table: "GuestEmailDeliveryItems");

            migrationBuilder.DropColumn(
                name: "PdfTemplateId",
                table: "GuestEmailDeliveryItems");

            migrationBuilder.CreateIndex(
                name: "IX_PdfTemplates_HotelId_Name_Version",
                table: "PdfTemplates",
                columns: new[] { "HotelId", "Name", "Version" },
                unique: true);
        }
    }
}
