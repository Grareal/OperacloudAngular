using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicePairingAndSigningSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SigningDevices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceType = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SecretHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    PairingCodeHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    PairingCodeExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastSeenAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningDevices", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SigningDevicePairings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkstationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TabletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    PairedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UnpairedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningDevicePairings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningDevicePairings_SigningDevices_TabletId",
                        column: x => x.TabletId,
                        principalTable: "SigningDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SigningDevicePairings_SigningDevices_WorkstationId",
                        column: x => x.WorkstationId,
                        principalTable: "SigningDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SigningSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ReservationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    GuestName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: true),
                    RoomType = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    WorkstationId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TabletId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AttachmentId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    AttachmentFileName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DocumentHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ErrorDetail = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ApprovedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningSessions_SigningDevices_TabletId",
                        column: x => x.TabletId,
                        principalTable: "SigningDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SigningSessions_SigningDevices_WorkstationId",
                        column: x => x.WorkstationId,
                        principalTable: "SigningDevices",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SigningSessionEvents",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SigningSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    PerformedBy = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Detail = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SigningSessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SigningSessionEvents_SigningSessions_SigningSessionId",
                        column: x => x.SigningSessionId,
                        principalTable: "SigningSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SigningDevicePairings_TabletId",
                table: "SigningDevicePairings",
                column: "TabletId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningDevicePairings_WorkstationId",
                table: "SigningDevicePairings",
                column: "WorkstationId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningDevices_HotelId_Name_DeviceType",
                table: "SigningDevices",
                columns: new[] { "HotelId", "Name", "DeviceType" });

            migrationBuilder.CreateIndex(
                name: "IX_SigningDevices_PairingCodeHash",
                table: "SigningDevices",
                column: "PairingCodeHash");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessionEvents_OccurredAtUtc",
                table: "SigningSessionEvents",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessionEvents_SigningSessionId",
                table: "SigningSessionEvents",
                column: "SigningSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessions_ConfirmationNumber",
                table: "SigningSessions",
                column: "ConfirmationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessions_Status",
                table: "SigningSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessions_TabletId",
                table: "SigningSessions",
                column: "TabletId");

            migrationBuilder.CreateIndex(
                name: "IX_SigningSessions_WorkstationId",
                table: "SigningSessions",
                column: "WorkstationId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SigningDevicePairings");

            migrationBuilder.DropTable(
                name: "SigningSessionEvents");

            migrationBuilder.DropTable(
                name: "SigningSessions");

            migrationBuilder.DropTable(
                name: "SigningDevices");
        }
    }
}
