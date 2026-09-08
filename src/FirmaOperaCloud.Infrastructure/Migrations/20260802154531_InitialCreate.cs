using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RegistrationCards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: true),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ReservationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    HotelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HotelAddress = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TswNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GuestFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ArrivalDate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    DepartureDate = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RoomType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RoomClass = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Adults = table.Column<int>(type: "int", nullable: false),
                    Children = table.Column<int>(type: "int", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Citizenship = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    City = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Country = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Company = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RatePlanCode = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    RateAmount = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Guarantee = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Observations = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SignatureBase64Png = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SignatureBase64Svg = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PdfBase64 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DocumentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    Receptionist = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceInfo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RegistrationCards", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignatureAuditEntries",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RegistrationCardId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DeviceIp = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceInfo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignatureAuditEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignatureAuditEntries_RegistrationCards_RegistrationCardId",
                        column: x => x.RegistrationCardId,
                        principalTable: "RegistrationCards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationCards_ArrivalDate",
                table: "RegistrationCards",
                column: "ArrivalDate");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationCards_ConfirmationNumber",
                table: "RegistrationCards",
                column: "ConfirmationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationCards_GeneratedAt",
                table: "RegistrationCards",
                column: "GeneratedAt");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationCards_GuestFullName",
                table: "RegistrationCards",
                column: "GuestFullName");

            migrationBuilder.CreateIndex(
                name: "IX_RegistrationCards_Status",
                table: "RegistrationCards",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureAuditEntries_OccurredAt",
                table: "SignatureAuditEntries",
                column: "OccurredAt");

            migrationBuilder.CreateIndex(
                name: "IX_SignatureAuditEntries_RegistrationCardId",
                table: "SignatureAuditEntries",
                column: "RegistrationCardId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignatureAuditEntries");

            migrationBuilder.DropTable(
                name: "RegistrationCards");
        }
    }
}
