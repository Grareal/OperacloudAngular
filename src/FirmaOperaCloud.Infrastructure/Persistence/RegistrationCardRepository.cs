using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FirmaOperaCloud.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="IRegistrationCardRepository"/> sobre SQL Server
/// usando EF Core. Base de datos local; no escribe en OPERA.
/// </summary>
public sealed class RegistrationCardRepository : IRegistrationCardRepository
{
    private readonly FirmaOperaCloudDbContext _db;

    public RegistrationCardRepository(FirmaOperaCloudDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<Guid> SaveAsync(RegistrationCard card, CancellationToken cancellationToken)
    {
        var existing = await _db.RegistrationCards.FindAsync([card.Id], cancellationToken);
        if (existing is null)
        {
            _db.RegistrationCards.Add(card);
        }
        else
        {
            _db.Entry(existing).CurrentValues.SetValues(card);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return card.Id;
    }

    /// <inheritdoc />
    public Task<RegistrationCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _db.RegistrationCards.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistrationCard>> GetByConfirmationNumberAsync(
        string confirmationNumber, CancellationToken cancellationToken) =>
        await _db.RegistrationCards
            .Where(c => c.ConfirmationNumber == confirmationNumber)
            .OrderByDescending(c => c.GeneratedAt)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistrationCard>> GetRecentAsync(
        int limit, CancellationToken cancellationToken) =>
        await _db.RegistrationCards
            .OrderByDescending(c => c.GeneratedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<RegistrationCard>> SearchAsync(
        string term, int limit, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(term))
        {
            return Array.Empty<RegistrationCard>();
        }

        var t = term.Trim();
        return await _db.RegistrationCards
            .Where(c =>
                c.ConfirmationNumber.Contains(t) ||
                c.GuestFullName.Contains(t) ||
                c.Email.Contains(t) ||
                c.Phone.Contains(t) ||
                c.RoomNumber.Contains(t) ||
                c.TswNumber!.Contains(t))
            .OrderByDescending(c => c.GeneratedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task AddAuditEntryAsync(SignatureAuditEntry entry, CancellationToken cancellationToken)
    {
        _db.SignatureAuditEntries.Add(entry);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
