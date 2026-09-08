using FirmaOperaCloud.Domain.Entities;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FirmaOperaCloud.Infrastructure.Pdf;

/// <summary>
/// Genera el PDF de la Tarjeta de Registro (Registration Card) con QuestPDF.
/// Replica la estructura del documento original: encabezado, datos de la reserva,
/// huésped, secciones legales (ES/EN) y espacio para firmas.
/// </summary>
public static class RegistrationCardPdfGenerator
{
    static RegistrationCardPdfGenerator()
    {
        // QuestPDF requiere definir la licencia. La licencia Community es gratuita
        // para uso comercial si la empresa tiene ingresos menores al umbral definido
        // por la librería (ver https://www.questpdf.com/license/).
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public static byte[] Generate(RegistrationCard card)
    {
        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.Letter);
                page.Margin(28);
                page.DefaultTextStyle(x => x.FontSize(9).FontColor(Colors.Black));

                page.Header().Element(b => BuildHeader(b, card));
                page.Content().Element(b => BuildContent(b, card));
                page.Footer().AlignCenter().Text(t =>
                {
                    t.Span("Página ").FontSize(8).FontColor(Colors.Grey.Darken1);
                    t.CurrentPageNumber().FontSize(8).FontColor(Colors.Grey.Darken1);
                });
            });
        });

        return document.GeneratePdf();
    }

    private static void BuildHeader(IContainer container, RegistrationCard card)
    {
        container.Column(column =>
        {
            column.Spacing(6);

            column.Item().Row(row =>
            {
                row.RelativeItem().Text("TARJETA DE REGISTRO | REGISTRATION CARD")
                    .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
            });

            column.Item().Text(RegistrationCardContent.HotelLegalName)
                .FontSize(10).SemiBold();

            column.Item().Text(RegistrationCardContent.HotelAddress)
                .FontSize(8).FontColor(Colors.Grey.Darken1);

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

            // Fila de datos principales: confirmación, tarifa, huéspedes.
            column.Item().Row(row =>
            {
                row.ConstantItem(45).Text("Confirmación").Bold();
                row.ConstantItem(130).Text(card.ConfirmationNumber);
                row.ConstantItem(80).Text("Tarifa Diaria").Bold();
                row.RelativeItem().Text(card.RateAmount);

                row.ConstantItem(45).Text("Llegada").Bold();
                row.ConstantItem(90).Text(card.ArrivalDate);
                row.ConstantItem(70).Text("Huéspedes").Bold();
                row.RelativeItem().Text((card.Adults + card.Children).ToString());
            });

            column.Item().Row(row =>
            {
                row.ConstantItem(45).Text("Salida").Bold();
                row.ConstantItem(130).Text(card.DepartureDate);
                row.ConstantItem(80).Text("Depósito").Bold();
                row.RelativeItem().Text(card.Guarantee);

                row.ConstantItem(45).Text("Tipo Hab.").Bold();
                row.ConstantItem(90).Text(card.RoomType);
                row.ConstantItem(70).Text("Habitación").Bold();
                row.RelativeItem().Text(card.RoomNumber);
            });

            column.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
        });
    }

    private static void BuildContent(IContainer container, RegistrationCard card)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().Element(b => BuildGuestSection(b, card));
            column.Item().Element(BuildConsentSection);
            column.Item().Element(BuildSignatureAuthorizationSection);
            column.Item().Element(b => BuildLegalSection(b, card));
        });
    }

    private static void BuildGuestSection(IContainer container, RegistrationCard card)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);

            column.Item().Text("DATOS DEL HUÉSPED | GUEST INFORMATION")
                .FontSize(10).Bold().FontColor(Colors.Blue.Darken3);

            column.Item().Row(row =>
            {
                row.ConstantItem(70).Text("Nombre | Name").Bold();
                row.RelativeItem().Text(card.GuestFullName);
                row.ConstantItem(90).Text("Nacionalidad").Bold();
                row.RelativeItem().Text(card.Citizenship);
            });

            column.Item().Row(row =>
            {
                row.ConstantItem(70).Text("Ciudad | City").Bold();
                row.RelativeItem().Text(card.City);
                row.ConstantItem(90).Text("Estado | State").Bold();
                row.RelativeItem().Text(card.State);
            });

            column.Item().Row(row =>
            {
                row.ConstantItem(70).Text("País | Country").Bold();
                row.RelativeItem().Text(card.Country);
                row.ConstantItem(90).Text("Teléfono").Bold();
                row.RelativeItem().Text(card.Phone);
            });

            column.Item().Row(row =>
            {
                row.ConstantItem(70).Text("Email").Bold();
                row.RelativeItem().Text(card.Email);
                row.ConstantItem(90).Text("Empresa").Bold();
                row.RelativeItem().Text(card.Company);
            });

            column.Item().Row(row =>
            {
                row.ConstantItem(70).Text("Tarifa").Bold();
                row.RelativeItem().Text(string.IsNullOrWhiteSpace(card.RateAmount) ? card.RatePlanCode : $"{card.RatePlanCode} ({card.RateAmount})");
                row.ConstantItem(90).Text("Observaciones").Bold();
                row.RelativeItem().Text(card.Observations);
            });
        });
    }

    private static void BuildConsentSection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);

            column.Item().Text("CONSENTIMIENTO DE CONTACTO | CONSENT TO CONTACT")
                .FontSize(10).Bold().FontColor(Colors.Blue.Darken3);

            column.Item().Text(RegistrationCardContent.ConsentContactEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.ConsentContactEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildSignatureAuthorizationSection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);

            column.Item().Text($"{RegistrationCardContent.SignatureAuthorizationTitleEs} | {RegistrationCardContent.SignatureAuthorizationTitleEn}")
                .FontSize(10).Bold().FontColor(Colors.Blue.Darken3);

            column.Item().Text(RegistrationCardContent.SignatureAuthorizationEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.SignatureAuthorizationEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);

            // Ocho slots de firma para ocupantes (1..8).
            column.Item().Row(row =>
            {
                for (var i = 0; i < 4; i++)
                {
                    var index = i + 1;
                    row.RelativeItem().Border(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Column(slot =>
                        {
                            slot.Spacing(2);
                            slot.Item().Text($"{index}) Nombre y firma | Name and signature").FontSize(7);
                            slot.Item().Height(30);
                        });
                }
            });

            column.Item().Row(row =>
            {
                for (var i = 0; i < 4; i++)
                {
                    var index = i + 5;
                    row.RelativeItem().Border(0.4f).BorderColor(Colors.Grey.Lighten2).Padding(4)
                        .Column(slot =>
                        {
                            slot.Spacing(2);
                            slot.Item().Text($"{index}) Nombre y firma | Name and signature").FontSize(7);
                            slot.Item().Height(30);
                        });
                }
            });
        });
    }

    private static void BuildLegalSection(IContainer container, RegistrationCard card)
    {
        container.Column(column =>
        {
            column.Spacing(8);

            column.Item().Element(BuildPolicySection);
            column.Item().Element(BuildContractSection);
            column.Item().Element(BuildJurisdictionSection);
            column.Item().Element(BuildPrivacySection);
            column.Item().Element(b => BuildSignatureLine(b, card));
        });
    }

    private static void BuildPolicySection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("POLÍTICA DE PAGO | PAYMENT POLICY").FontSize(10).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Text(RegistrationCardContent.PaymentPolicyEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.PaymentPolicyEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildContractSection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("CONFIRMACIÓN DE ACEPTACIÓN DE CONTRATO | FULL CONTRACT ACKNOWLEDGMENT")
                .FontSize(10).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Text(RegistrationCardContent.ContractAcknowledgmentEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.ContractAcknowledgmentEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildJurisdictionSection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("JURISDICCIÓN | DISPUTE RESOLUTION").FontSize(10).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Text(RegistrationCardContent.JurisdictionEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.JurisdictionEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildPrivacySection(IContainer container)
    {
        container.Border(0.6f).BorderColor(Colors.Grey.Lighten2).Padding(8).Column(column =>
        {
            column.Spacing(4);
            column.Item().Text("TRATAMIENTO DE DATOS PERSONALES Y AVISO | TREATMENT OF PERSONAL DATA AND NOTICE")
                .FontSize(10).Bold().FontColor(Colors.Blue.Darken3);
            column.Item().Text(RegistrationCardContent.PrivacyEs).FontSize(8).Justify();
            column.Item().Text(RegistrationCardContent.PrivacyEn).FontSize(8).Justify().FontColor(Colors.Grey.Darken1);
        });
    }

    private static void BuildSignatureLine(IContainer container, RegistrationCard card)
    {
        container.Column(column =>
        {
            column.Spacing(4);

            column.Item().Text("Firma | Signature").Bold().FontSize(9);

            // Si existe firma digital capturada, se incrusta como imagen.
            if (!string.IsNullOrWhiteSpace(card.SignatureBase64Png))
            {
                // Conserva exactamente el espacio reservado por la línea sin firma;
                // FitArea evita que una imagen panorámica fuerce el ancho o el alto.
                column.Item().Height(60).Image(Convert.FromBase64String(card.SignatureBase64Png)).FitArea();
            }
            else
            {
                column.Item().Height(60).LineHorizontal(0.8f).LineColor(Colors.Grey.Darken1);
            }
        });
    }
}
