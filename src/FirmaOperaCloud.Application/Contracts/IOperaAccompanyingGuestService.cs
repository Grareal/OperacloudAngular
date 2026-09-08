namespace FirmaOperaCloud.Application.Contracts;

public interface IOperaAccompanyingGuestService
{
    Task<AccompanyingGuestProfileLookup> LookupAdultAsync(
        string hotelId, string confirmationNumber, string givenName, string surname,
        CancellationToken cancellationToken = default);

    Task<AccompanyingGuestChangePreview> PreviewAddAdultAsync(
        string hotelId, string confirmationNumber, string profileId, CancellationToken cancellationToken = default);

    Task<AccompanyingGuestChangeResult> AddAdultAsync(
        string hotelId, string confirmationNumber, string profileId, string expectedLastModifyDateTime,
        string userName, CancellationToken cancellationToken = default);

    Task<AccompanyingGuestChangeResult> CreateAndAddAdultAsync(
        string hotelId, string confirmationNumber, string givenName, string surname,
        string expectedLastModifyDateTime, string userName, CancellationToken cancellationToken = default);
}

public sealed record OperaGuestProfileInfo(string ProfileId, string FullName, bool Primary);

public sealed record AccompanyingGuestProfileLookup(
    string GivenName,
    string Surname,
    IReadOnlyList<OperaGuestProfileInfo> Matches,
    AccompanyingGuestChangePreview? NewProfilePreview);

public sealed record AccompanyingGuestChangePreview(
    string HotelId,
    string ConfirmationNumber,
    string ReservationId,
    string ReservationStatus,
    string LastModifyDateTime,
    int CurrentAdults,
    int ProposedAdults,
    int Children,
    OperaGuestProfileInfo RequestedProfile,
    IReadOnlyList<OperaGuestProfileInfo> CurrentGuests,
    IReadOnlyList<OperaGuestProfileInfo> ProposedGuests,
    bool AlreadyLinked,
    bool CanApply,
    string ValidationMessage);

public sealed record AccompanyingGuestChangeResult(
    Guid AuditId,
    string CorrelationId,
    AccompanyingGuestChangePreview Before,
    AccompanyingGuestChangePreview After,
    string OperaResponse);
