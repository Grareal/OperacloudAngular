using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Api.Services;

public sealed class ReservationFileService(FirmaOperaCloudDbContext db)
{
    public async Task AddDocumentAsync(LocalDocument document, string? user, CancellationToken ct)
    {
        var file = await db.ReservationFiles.FirstOrDefaultAsync(x =>
            x.HotelId == document.HotelId &&
            x.ConfirmationNumber == document.ConfirmationNumber &&
            x.Status == ReservationFileStatuses.Open, ct);

        if (file is null)
        {
            var version = (await db.ReservationFiles
                .Where(x => x.HotelId == document.HotelId &&
                    x.ConfirmationNumber == document.ConfirmationNumber)
                .MaxAsync(x => (int?)x.Version, ct) ?? 0) + 1;
            file = new ReservationFile
            {
                HotelId = document.HotelId,
                ConfirmationNumber = document.ConfirmationNumber,
                ReservationId = document.ReservationId,
                Version = version,
                CreatedBy = user
            };
            db.ReservationFiles.Add(file);
        }

        document.ReservationFile = file;
        db.LocalDocuments.Add(document);
        await db.SaveChangesAsync(ct);
        await RefreshManifestAsync(file, ct);
    }

    public async Task SealAsync(ReservationFile file, string user, CancellationToken ct)
    {
        if (file.Status == ReservationFileStatuses.Sealed) return;
        await RefreshManifestAsync(file, ct);
        file.Status = ReservationFileStatuses.Sealed;
        file.SealedAtUtc = DateTime.UtcNow;
        file.SealedBy = user;
        await db.SaveChangesAsync(ct);
    }

    private async Task RefreshManifestAsync(ReservationFile file, CancellationToken ct)
    {
        var documents = await db.LocalDocuments.AsNoTracking()
            .Where(x => x.ReservationFileId == file.Id)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Select(x => new
            {
                x.Id, x.Version, x.FileName, x.DocumentHash, x.Status,
                x.CreatedAtUtc, x.AttachmentId
            }).ToListAsync(ct);
        file.ManifestJson = JsonSerializer.Serialize(documents);
        file.ManifestHash = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(file.ManifestJson)));
        await db.SaveChangesAsync(ct);
    }
}
