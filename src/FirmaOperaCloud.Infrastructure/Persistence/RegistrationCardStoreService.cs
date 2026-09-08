using FirmaOperaCloud.Application.Contracts;
using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Pdf;

namespace FirmaOperaCloud.Infrastructure.Persistence;

/// <summary>
/// Implementación de <see cref="IRegistrationCardStoreService"/>: genera el PDF,
/// lo persiste localmente, firma y registra auditoría en SQL Server.
/// </summary>
public sealed class RegistrationCardStoreService : IRegistrationCardStoreService
{
    private readonly IRegistrationCardService _cardService;
    private readonly IRegistrationCardRepository _repository;

    public RegistrationCardStoreService(
        IRegistrationCardService cardService,
        IRegistrationCardRepository repository)
    {
        _cardService = cardService;
        _repository = repository;
    }

    /// <inheritdoc />
    public async Task<RegistrationCard> GenerateAndStoreAsync(
        Reservation reservation,
        string? receptionist,
        string? deviceIp,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        var card = _cardService.BuildCard(reservation);
        card.HotelId = reservation.HotelId;
        card.ReservationId = reservation.ReservationIdList
            .FirstOrDefault(r => r.Type.Equals("Reservation", StringComparison.OrdinalIgnoreCase))?.Id;

        var pdfBytes = _cardService.GeneratePdf(card);
        card.PdfBase64 = Convert.ToBase64String(pdfBytes);
        card.DocumentHash = RegistrationCardService.ComputeHash(pdfBytes);
        card.Receptionist = receptionist;
        card.DeviceIp = deviceIp;
        card.DeviceInfo = deviceInfo;
        card.Status = "Generated";

        var id = await _repository.SaveAsync(card, cancellationToken);

        await _repository.AddAuditEntryAsync(new SignatureAuditEntry
        {
            RegistrationCardId = id,
            Action = "Generated",
            PerformedBy = receptionist,
            DeviceIp = deviceIp,
            DeviceInfo = deviceInfo,
            Detail = $"PDF generado. Hash SHA-256: {card.DocumentHash}"
        }, cancellationToken);

        return card;
    }

    /// <inheritdoc />
    public async Task<RegistrationCard> SignAsync(
        Guid cardId,
        string signaturePngBase64,
        string? signatureSvgBase64,
        string? receptionist,
        string? deviceIp,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        var card = await _repository.GetByIdAsync(cardId, cancellationToken)
            ?? throw new KeyNotFoundException($"Tarjeta de registro {cardId} no encontrada.");

        card.SignatureBase64Png = signaturePngBase64;
        card.SignatureBase64Svg = signatureSvgBase64;
        card.SignedAt = DateTime.UtcNow;
        card.Receptionist = receptionist;
        card.DeviceIp = deviceIp;
        card.DeviceInfo = deviceInfo;
        card.Status = "Signed";

        // El documento firmado es un artefacto nuevo: vuelve a generar el PDF con
        // la imagen capturada y calcula el hash sobre los bytes finales.
        var signedPdfBytes = _cardService.GeneratePdf(card);
        card.PdfBase64 = Convert.ToBase64String(signedPdfBytes);
        card.DocumentHash = RegistrationCardService.ComputeHash(signedPdfBytes);

        await _repository.SaveAsync(card, cancellationToken);

        await _repository.AddAuditEntryAsync(new SignatureAuditEntry
        {
            RegistrationCardId = cardId,
            Action = "Signed",
            PerformedBy = receptionist,
            DeviceIp = deviceIp,
            DeviceInfo = deviceInfo,
            Detail = $"PDF firmado generado. Hash SHA-256: {card.DocumentHash}"
        }, cancellationToken);

        return card;
    }

    /// <inheritdoc />
    public Task<RegistrationCard?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        _repository.GetByIdAsync(id, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RegistrationCard>> GetByConfirmationNumberAsync(
        string confirmationNumber, CancellationToken cancellationToken) =>
        _repository.GetByConfirmationNumberAsync(confirmationNumber, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RegistrationCard>> GetRecentAsync(int limit, CancellationToken cancellationToken) =>
        _repository.GetRecentAsync(limit, cancellationToken);

    /// <inheritdoc />
    public Task<IReadOnlyList<RegistrationCard>> SearchAsync(string term, int limit, CancellationToken cancellationToken) =>
        _repository.SearchAsync(term, limit, cancellationToken);

    /// <inheritdoc />
    public async Task AuditAsync(
        Guid cardId, string action, string? performedBy, string? deviceIp, string? deviceInfo, string? detail, CancellationToken cancellationToken)
    {
        await _repository.AddAuditEntryAsync(new SignatureAuditEntry
        {
            RegistrationCardId = cardId,
            Action = action,
            PerformedBy = performedBy,
            DeviceIp = deviceIp,
            DeviceInfo = deviceInfo,
            Detail = detail
        }, cancellationToken);
    }
}
