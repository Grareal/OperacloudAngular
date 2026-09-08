using FirmaOperaCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Infrastructure.Persistence;

/// <summary>
/// Contexto de base de datos local (SQL Server).
/// Persiste las tarjetas de registro generadas, las firmas y la auditoría.
/// Esta base de datos es local al sistema: nunca se escribe en OPERA.
/// </summary>
public sealed class FirmaOperaCloudDbContext : DbContext
{
    public FirmaOperaCloudDbContext(DbContextOptions<FirmaOperaCloudDbContext> options)
        : base(options)
    {
    }

    public DbSet<RegistrationCard> RegistrationCards => Set<RegistrationCard>();
    public DbSet<SignatureAuditEntry> SignatureAuditEntries => Set<SignatureAuditEntry>();
    public DbSet<AppUser> AppUsers => Set<AppUser>();
    public DbSet<UserGroup> UserGroups => Set<UserGroup>();
    public DbSet<StoredSignature> StoredSignatures => Set<StoredSignature>();
    public DbSet<PdfTemplate> PdfTemplates => Set<PdfTemplate>();
    public DbSet<PdfTemplateField> PdfTemplateFields => Set<PdfTemplateField>();
    public DbSet<LocalDocument> LocalDocuments => Set<LocalDocument>();
    public DbSet<ReservationFile> ReservationFiles => Set<ReservationFile>();
    public DbSet<AuditEvent> AuditEvents => Set<AuditEvent>();
    public DbSet<LocalDocumentSignature> LocalDocumentSignatures => Set<LocalDocumentSignature>();
    public DbSet<HotelDocumentSetting> HotelDocumentSettings => Set<HotelDocumentSetting>();
    public DbSet<CommunicationDocument> CommunicationDocuments => Set<CommunicationDocument>();
    public DbSet<GuestEmailDelivery> GuestEmailDeliveries => Set<GuestEmailDelivery>();
    public DbSet<GuestEmailDeliveryItem> GuestEmailDeliveryItems => Set<GuestEmailDeliveryItem>();
    public DbSet<PromotionCatalogEntry> PromotionCatalogEntries => Set<PromotionCatalogEntry>();
    public DbSet<GuestEmailSetting> GuestEmailSettings => Set<GuestEmailSetting>();
    public DbSet<ReservationGuestChangeAudit> ReservationGuestChangeAudits => Set<ReservationGuestChangeAudit>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.ToTable("AppUsers");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Username).HasMaxLength(64).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.DisplayName).HasMaxLength(200);
            entity.Property(e => e.Role).HasMaxLength(32);

            entity.HasIndex(e => e.Username).IsUnique();
            entity.HasOne(e => e.UserGroup).WithMany(e => e.Users).HasForeignKey(e => e.UserGroupId).OnDelete(DeleteBehavior.SetNull);
        });

        modelBuilder.Entity<UserGroup>(entity =>
        {
            entity.ToTable("UserGroups"); entity.HasKey(e => e.Id);
            entity.Property(e => e.Name).HasMaxLength(100).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(300);
            entity.Property(e => e.PermissionsJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.HasIndex(e => e.Name).IsUnique();
        });

        modelBuilder.Entity<RegistrationCard>(entity =>
        {
            entity.ToTable("RegistrationCards");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.HotelId).HasMaxLength(20);
            entity.Property(e => e.ReservationId).HasMaxLength(64);
            entity.Property(e => e.HotelName).HasMaxLength(200);
            entity.Property(e => e.HotelAddress).HasMaxLength(300);
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(64);
            entity.Property(e => e.TswNumber).HasMaxLength(64);
            entity.Property(e => e.GuestFullName).HasMaxLength(200);
            entity.Property(e => e.ArrivalDate).HasMaxLength(32);
            entity.Property(e => e.DepartureDate).HasMaxLength(32);
            entity.Property(e => e.RoomNumber).HasMaxLength(32);
            entity.Property(e => e.RoomType).HasMaxLength(64);
            entity.Property(e => e.RoomClass).HasMaxLength(64);
            entity.Property(e => e.Email).HasMaxLength(200);
            entity.Property(e => e.Phone).HasMaxLength(64);
            entity.Property(e => e.Citizenship).HasMaxLength(64);
            entity.Property(e => e.City).HasMaxLength(100);
            entity.Property(e => e.State).HasMaxLength(100);
            entity.Property(e => e.Country).HasMaxLength(64);
            entity.Property(e => e.Company).HasMaxLength(200);
            entity.Property(e => e.RatePlanCode).HasMaxLength(32);
            entity.Property(e => e.RateAmount).HasMaxLength(64);
            entity.Property(e => e.Guarantee).HasMaxLength(200);
            entity.Property(e => e.Observations).HasMaxLength(1000);

            entity.Property(e => e.SignatureBase64Png);
            entity.Property(e => e.SignatureBase64Svg);
            entity.Property(e => e.PdfBase64);
            entity.Property(e => e.DocumentHash).HasMaxLength(64);
            entity.Property(e => e.Receptionist).HasMaxLength(200);
            entity.Property(e => e.DeviceIp).HasMaxLength(64);
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.Status).HasMaxLength(32);

            entity.HasIndex(e => e.ConfirmationNumber);
            entity.HasIndex(e => e.GuestFullName);
            entity.HasIndex(e => e.ArrivalDate);
            entity.HasIndex(e => e.GeneratedAt);
            entity.HasIndex(e => e.Status);
        });

        modelBuilder.Entity<SignatureAuditEntry>(entity =>
        {
            entity.ToTable("SignatureAuditEntries");
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Action).HasMaxLength(32);
            entity.Property(e => e.PerformedBy).HasMaxLength(200);
            entity.Property(e => e.DeviceIp).HasMaxLength(64);
            entity.Property(e => e.DeviceInfo).HasMaxLength(500);
            entity.Property(e => e.Detail).HasMaxLength(1000);

            entity.HasIndex(e => e.RegistrationCardId);
            entity.HasIndex(e => e.OccurredAt);

            entity.HasOne(e => e.RegistrationCard)
                .WithMany()
                .HasForeignKey(e => e.RegistrationCardId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReservationGuestChangeAudit>(entity =>
        {
            entity.ToTable("ReservationGuestChangeAudits");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.HotelId).HasMaxLength(20).IsRequired();
            entity.Property(x => x.ConfirmationNumber).HasMaxLength(64).IsRequired();
            entity.Property(x => x.ReservationId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RequestedProfileId).HasMaxLength(64).IsRequired();
            entity.Property(x => x.RequestedProfileName).HasMaxLength(300);
            entity.Property(x => x.UserName).HasMaxLength(200);
            entity.Property(x => x.CorrelationId).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(32).IsRequired();
            entity.Property(x => x.BeforeJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.RequestJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ResponseJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.AfterJson).HasColumnType("nvarchar(max)");
            entity.Property(x => x.ErrorMessage).HasColumnType("nvarchar(max)");
            entity.HasIndex(x => new { x.HotelId, x.ConfirmationNumber, x.CreatedAtUtc });
        });

        modelBuilder.Entity<LocalDocument>(entity =>
        {
            entity.ToTable("LocalDocuments"); entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired(); entity.Property(e => e.ConfirmationNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservationId).HasMaxLength(64); entity.Property(e => e.RoomNumber).HasMaxLength(32);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired(); entity.Property(e => e.PdfData).IsRequired();
            entity.Property(e => e.DocumentHash).HasMaxLength(64).IsRequired(); entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.AttachmentId).HasMaxLength(64); entity.Property(e => e.AttachmentFileName).HasMaxLength(255); entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.HumanReviewedBy).HasMaxLength(200);
            entity.HasIndex(e => new { e.HotelId, e.ConfirmationNumber, e.Version }).IsUnique();
            entity.HasOne(e => e.PdfTemplate).WithMany().HasForeignKey(e => e.PdfTemplateId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.ReservationFile).WithMany(e => e.Documents).HasForeignKey(e => e.ReservationFileId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<ReservationFile>(entity =>
        {
            entity.ToTable("ReservationFiles");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservationId).HasMaxLength(64);
            entity.Property(e => e.Status).HasMaxLength(32).IsRequired();
            entity.Property(e => e.ManifestJson).HasColumnType("nvarchar(max)").IsRequired();
            entity.Property(e => e.ManifestHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(200);
            entity.Property(e => e.SealedBy).HasMaxLength(200);
            entity.HasIndex(e => new { e.HotelId, e.ConfirmationNumber, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.HotelId, e.ConfirmationNumber, e.Status });
        });
        modelBuilder.Entity<AuditEvent>(entity =>
        {
            entity.ToTable("AuditEvents");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ActorUsername).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Action).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ResourceType).HasMaxLength(80).IsRequired();
            entity.Property(e => e.ResourceId).HasMaxLength(128);
            entity.Property(e => e.HotelId).HasMaxLength(20);
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(64);
            entity.Property(e => e.AccessReason).HasMaxLength(500);
            entity.Property(e => e.IpAddress).HasMaxLength(64);
            entity.Property(e => e.UserAgent).HasMaxLength(500);
            entity.Property(e => e.CorrelationId).HasMaxLength(64).IsRequired();
            entity.Property(e => e.Outcome).HasMaxLength(32).IsRequired();
            entity.Property(e => e.DetailJson).HasColumnType("nvarchar(max)");
            entity.Property(e => e.PreviousHash).HasMaxLength(64);
            entity.Property(e => e.EventHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => e.OccurredAtUtc);
            entity.HasIndex(e => new { e.ConfirmationNumber, e.OccurredAtUtc });
            entity.HasIndex(e => new { e.ActorUsername, e.OccurredAtUtc });
        });
        modelBuilder.Entity<LocalDocumentSignature>(entity =>
        {
            entity.ToTable("LocalDocumentSignatures"); entity.HasKey(e => new { e.LocalDocumentId, e.StoredSignatureId });
            entity.Property(e => e.Role).HasMaxLength(32).IsRequired();
            entity.HasOne(e => e.LocalDocument).WithMany(e => e.Signatures).HasForeignKey(e => e.LocalDocumentId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.StoredSignature).WithMany().HasForeignKey(e => e.StoredSignatureId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<StoredSignature>(entity =>
        {
            entity.ToTable("StoredSignatures");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.ConfirmationNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ReservationId).HasMaxLength(64);
            entity.Property(e => e.RoomNumber).HasMaxLength(32);
            entity.Property(e => e.SignerName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SignerKey).HasMaxLength(256).IsRequired();
            entity.Property(e => e.OperaProfileId).HasMaxLength(64);
            entity.Property(e => e.SignerRole).HasMaxLength(32).IsRequired();
            entity.Property(e => e.SignaturePng).IsRequired();
            entity.Property(e => e.SignatureHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.CapturedBy).HasMaxLength(100);
            entity.HasIndex(e => e.ConfirmationNumber);
            entity.HasIndex(e => e.RoomNumber);
            entity.HasIndex(e => e.SignerName);
            entity.HasIndex(e => e.SignerKey);
        });

        modelBuilder.Entity<PdfTemplate>(entity =>
        {
            entity.ToTable("PdfTemplates"); entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(150).IsRequired();
            entity.Property(e => e.TemplateType).HasMaxLength(32).HasDefaultValue(PdfTemplateTypes.RegistrationCard).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired();
            entity.Property(e => e.PdfData).IsRequired();
            entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.Property(e => e.RoomTypePrefix).HasMaxLength(32);
            entity.Property(e => e.Language).HasMaxLength(12);
            entity.HasIndex(e => new { e.HotelId, e.TemplateType, e.Name, e.Version }).IsUnique();
        });
        modelBuilder.Entity<PdfTemplateField>(entity =>
        {
            entity.ToTable("PdfTemplateFields"); entity.HasKey(e => e.Id);
            entity.Property(e => e.FieldKey).HasMaxLength(80).IsRequired();
            entity.Property(e => e.Label).HasMaxLength(120).IsRequired();
            entity.Property(e => e.FieldType).HasMaxLength(20).IsRequired();
            entity.HasIndex(e => e.PdfTemplateId);
            entity.HasOne(e => e.PdfTemplate).WithMany(e => e.Fields).HasForeignKey(e => e.PdfTemplateId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HotelDocumentSetting>(entity =>
        {
            entity.ToTable("HotelDocumentSettings"); entity.HasKey(e => e.HotelId);
            entity.Property(e => e.HotelId).HasMaxLength(20); entity.Property(e => e.SourceMode).HasMaxLength(16).IsRequired(); entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.HasOne(e => e.PreferredPdfTemplate).WithMany().HasForeignKey(e => e.PreferredPdfTemplateId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<CommunicationDocument>(entity =>
        {
            entity.ToTable("CommunicationDocuments"); entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired(); entity.Property(e => e.Type).HasMaxLength(32).IsRequired();
            entity.Property(e => e.Name).HasMaxLength(160).IsRequired(); entity.Property(e => e.Language).HasMaxLength(12).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(16).IsRequired(); entity.Property(e => e.SourceUrl).HasMaxLength(1000);
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired(); entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.Property(e => e.DocumentHash).HasMaxLength(64); entity.Property(e => e.CreatedBy).HasMaxLength(100);
            entity.HasIndex(e => new { e.HotelId, e.Type, e.Name, e.Language, e.Version }).IsUnique();
            entity.HasIndex(e => new { e.HotelId, e.IsPublished, e.Language });
        });
        modelBuilder.Entity<GuestEmailDelivery>(entity =>
        {
            entity.ToTable("GuestEmailDeliveries"); entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired(); entity.Property(e => e.ConfirmationNumber).HasMaxLength(64).IsRequired();
            entity.Property(e => e.GuestName).HasMaxLength(200).IsRequired(); entity.Property(e => e.RecipientEmail).HasMaxLength(320).IsRequired();
            entity.Property(e => e.Language).HasMaxLength(12).IsRequired(); entity.Property(e => e.Status).HasMaxLength(24).IsRequired();
            entity.Property(e => e.ProviderMessageId).HasMaxLength(255); entity.Property(e => e.LastError).HasMaxLength(2000);
            entity.HasIndex(e => e.Status); entity.HasIndex(e => e.ConfirmationNumber);
            entity.HasOne(e => e.LocalDocument).WithMany().HasForeignKey(e => e.LocalDocumentId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<GuestEmailDeliveryItem>(entity =>
        {
            entity.ToTable("GuestEmailDeliveryItems"); entity.HasKey(e => e.Id);
            entity.Property(e => e.DocumentType).HasMaxLength(32).IsRequired(); entity.Property(e => e.Name).HasMaxLength(160).IsRequired();
            entity.Property(e => e.FileName).HasMaxLength(255).IsRequired(); entity.Property(e => e.DocumentHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.ContentType).HasMaxLength(100).IsRequired();
            entity.HasOne(e => e.Delivery).WithMany(e => e.Items).HasForeignKey(e => e.GuestEmailDeliveryId).OnDelete(DeleteBehavior.Cascade);
            entity.HasOne(e => e.CommunicationDocument).WithMany().HasForeignKey(e => e.CommunicationDocumentId).OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(e => e.PdfTemplate).WithMany().HasForeignKey(e => e.PdfTemplateId).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<PromotionCatalogEntry>(entity =>
        {
            entity.ToTable("PromotionCatalogEntries"); entity.HasKey(e => e.Id);
            entity.Property(e => e.HotelId).HasMaxLength(20).IsRequired();
            entity.Property(e => e.OperaCode).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OperaDescription).HasMaxLength(4000);
            entity.Property(e => e.GuestTitle).HasMaxLength(200);
            entity.Property(e => e.GuestDescription).HasMaxLength(4000);
            entity.Property(e => e.Language).HasMaxLength(12).IsRequired();
            entity.Property(e => e.Source).HasMaxLength(32).IsRequired();
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
            entity.HasIndex(e => new { e.HotelId, e.OperaCode, e.Language }).IsUnique();
            entity.HasIndex(e => new { e.HotelId, e.IsActive, e.IsApprovedForGuest });
        });
        modelBuilder.Entity<GuestEmailSetting>(entity =>
        {
            entity.ToTable("GuestEmailSettings"); entity.HasKey(e => e.HotelId);
            entity.Property(e => e.HotelId).HasMaxLength(20); entity.Property(e => e.Host).HasMaxLength(255);
            entity.Property(e => e.Username).HasMaxLength(320); entity.Property(e => e.Password).HasMaxLength(1000);
            entity.Property(e => e.FromAddress).HasMaxLength(320); entity.Property(e => e.FromName).HasMaxLength(200);
            entity.Property(e => e.Subject).HasMaxLength(500); entity.Property(e => e.BodyHtml).HasMaxLength(8000);
            entity.Property(e => e.UpdatedBy).HasMaxLength(100);
        });
    }
}
