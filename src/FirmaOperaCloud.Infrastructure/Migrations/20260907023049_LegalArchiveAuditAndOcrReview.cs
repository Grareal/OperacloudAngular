using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FirmaOperaCloud.Infrastructure.Migrations;

/// <summary>
/// Migración no destructiva: conserva las tablas/columnas históricas de tablet y sesión
/// para no perder evidencia UAT, aunque el modelo y la aplicación ya no las utilizan.
/// </summary>
public partial class LegalArchiveAuditAndOcrReview : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>("ReservationFileId", "LocalDocuments", "uniqueidentifier", nullable: true);
        migrationBuilder.AddColumn<DateTime>("HumanReviewedAtUtc", "LocalDocuments", "datetime2", nullable: true);
        migrationBuilder.AddColumn<string>("HumanReviewedBy", "LocalDocuments", "nvarchar(200)", maxLength: 200, nullable: true);
        migrationBuilder.AddColumn<bool>("IsLegalHold", "LocalDocuments", "bit", nullable: false, defaultValue: false);
        migrationBuilder.AddColumn<DateTime>("RetentionUntilUtc", "LocalDocuments", "datetime2", nullable: true);

        migrationBuilder.CreateTable(
            name: "AuditEvents",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                ActorUserId = table.Column<long>(type: "bigint", nullable: true),
                ActorUsername = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                Action = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ResourceType = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                ResourceId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                AccessReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                IpAddress = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                UserAgent = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                CorrelationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                Outcome = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                DetailJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                PreviousHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                EventHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
            },
            constraints: table => table.PrimaryKey("PK_AuditEvents", x => x.Id));

        migrationBuilder.CreateTable(
            name: "ReservationFiles",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                HotelId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                ConfirmationNumber = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                ReservationId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                Version = table.Column<int>(type: "int", nullable: false),
                Status = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                ManifestJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ManifestHash = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: false),
                SealedAtUtc = table.Column<DateTime>(type: "datetime2", nullable: true),
                CreatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                SealedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_ReservationFiles", x => x.Id));

        migrationBuilder.CreateIndex("IX_LocalDocuments_ReservationFileId", "LocalDocuments", "ReservationFileId");
        migrationBuilder.CreateIndex("IX_AuditEvents_ActorUsername_OccurredAtUtc", "AuditEvents", new[] { "ActorUsername", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_AuditEvents_ConfirmationNumber_OccurredAtUtc", "AuditEvents", new[] { "ConfirmationNumber", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_AuditEvents_OccurredAtUtc", "AuditEvents", "OccurredAtUtc");
        migrationBuilder.CreateIndex("IX_ReservationFiles_HotelId_ConfirmationNumber_Status", "ReservationFiles", new[] { "HotelId", "ConfirmationNumber", "Status" });
        migrationBuilder.CreateIndex("IX_ReservationFiles_HotelId_ConfirmationNumber_Version", "ReservationFiles", new[] { "HotelId", "ConfirmationNumber", "Version" }, unique: true);
        migrationBuilder.AddForeignKey("FK_LocalDocuments_ReservationFiles_ReservationFileId", "LocalDocuments", "ReservationFileId", "ReservationFiles", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_LocalDocuments_ReservationFiles_ReservationFileId", "LocalDocuments");
        migrationBuilder.DropTable("AuditEvents");
        migrationBuilder.DropTable("ReservationFiles");
        migrationBuilder.DropIndex("IX_LocalDocuments_ReservationFileId", "LocalDocuments");
        migrationBuilder.DropColumn("ReservationFileId", "LocalDocuments");
        migrationBuilder.DropColumn("HumanReviewedAtUtc", "LocalDocuments");
        migrationBuilder.DropColumn("HumanReviewedBy", "LocalDocuments");
        migrationBuilder.DropColumn("IsLegalHold", "LocalDocuments");
        migrationBuilder.DropColumn("RetentionUntilUtc", "LocalDocuments");
    }
}
