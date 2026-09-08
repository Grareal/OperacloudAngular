using FirmaOperaCloud.Domain.Entities;
using FirmaOperaCloud.Infrastructure.Ocr;
using Xunit;

namespace FirmaOperaCloud.Tests;

public sealed class IdentityDocumentParserTests
{
    [Fact]
    public void PassportMrz_WithValidCheckDigits_IsAccepted()
    {
        const string line = "L898902C36UTO7408122F1204159ZE184226B<<<<<10";
        Assert.True(IdentityDocumentParser.VerifyPassportMrz(line));
    }

    [Fact]
    public void PassportMrz_WhenDocumentCheckDigitChanges_IsRejected()
    {
        const string line = "L898902C30UTO7408122F1204159ZE184226B<<<<<10";
        Assert.False(IdentityDocumentParser.VerifyPassportMrz(line));
    }

    [Fact]
    public void NewLegalIdentifiers_AreVersionSevenGuids()
    {
        Assert.Equal(7, new ReservationFile().Id.Version);
        Assert.Equal(7, new AuditEvent().Id.Version);
        Assert.Equal(7, new LocalDocument().Id.Version);
    }
}
