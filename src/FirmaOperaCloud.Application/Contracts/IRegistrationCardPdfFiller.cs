namespace FirmaOperaCloud.Application.Contracts;

public sealed record RegistrationCardOccupantInput(
    string Name,
    string SignaturePngBase64);

public sealed record RegistrationCardFillInput(
    string? Citizenship,
    string? City,
    string? State,
    string? Country,
    string? Email,
    string? CellPhone,
    string? PrimaryGuestName,
    string? PrimarySignaturePngBase64,
    IReadOnlyList<RegistrationCardOccupantInput> Occupants);

public interface IRegistrationCardPdfFiller
{
    byte[] Fill(byte[] officialPdf, RegistrationCardFillInput input);
}
