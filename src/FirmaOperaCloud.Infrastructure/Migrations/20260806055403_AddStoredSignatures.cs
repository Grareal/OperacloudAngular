using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddStoredSignatures : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StoredSignatures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReservationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    RoomNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    SigningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SignerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SignerKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    OperaProfileId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    SignerRole = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    SignaturePng = table.Column<byte[]>(type: "varbinary(max)", nullable: false),
                    SignatureHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    SignedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CapturedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StoredSignatures", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StoredSignatures_SigningSessions_SigningSessionId",
                        column: x => x.SigningSessionId,
                        principalTable: "SigningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StoredSignatures_ConfirmationNumber",
                table: "StoredSignatures",
                column: "ConfirmationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StoredSignatures_RoomNumber",
                table: "StoredSignatures",
                column: "RoomNumber");

            migrationBuilder.CreateIndex(
                name: "IX_StoredSignatures_SignerKey",
                table: "StoredSignatures",
                column: "SignerKey");

            migrationBuilder.CreateIndex(
                name: "IX_StoredSignatures_SignerName",
                table: "StoredSignatures",
                column: "SignerName");

            migrationBuilder.CreateIndex(
                name: "IX_StoredSignatures_SigningSessionId",
                table: "StoredSignatures",
                column: "SigningSessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StoredSignatures");
        }
    }
}
