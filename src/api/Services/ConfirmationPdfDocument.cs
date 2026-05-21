using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace Api.Services;

/// <summary>
/// QuestPDF <see cref="IDocument"/> that renders the single-page appointment confirmation PDF
/// containing all AC-002 mandatory fields and a QR code (us_022; AC-002).
///
/// <para>
/// Mandatory fields rendered (AC-002):
/// <list type="bullet">
///   <item>Patient full name</item>
///   <item>Appointment date and time</item>
///   <item>Clinic name (document header)</item>
///   <item>Clinic address</item>
///   <item>Provider name (or "To be assigned" when null)</item>
///   <item>Booking reference ID (text + QR code)</item>
/// </list>
/// </para>
/// </summary>
public sealed class ConfirmationPdfDocument : IDocument
{
    private readonly ConfirmationData _data;

    public ConfirmationPdfDocument(ConfirmationData data)
    {
        _data = data;
    }

    /// <inheritdoc />
    public DocumentMetadata GetMetadata() => new()
    {
        Title  = "Appointment Confirmation",
        Author = "Clinic System",
    };

    /// <inheritdoc />
    public DocumentSettings GetSettings() => DocumentSettings.Default;

    /// <inheritdoc />
    public void Compose(IDocumentContainer container)
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A4);
            page.Margin(40);
            page.DefaultTextStyle(t => t.FontFamily(Fonts.Arial).FontSize(10));

            page.Content().Column(col =>
            {
                // ── Header: clinic name + subtitle ─────────────────────────────────
                col.Item().PaddingBottom(16).Column(header =>
                {
                    header.Item()
                          .Text(_data.ClinicName)
                          .Bold()
                          .FontSize(18)
                          .FontColor(Colors.Blue.Darken3);

                    header.Item()
                          .Text("Appointment Confirmation")
                          .FontSize(12)
                          .FontColor(Colors.Grey.Darken1);
                });

                // ── Horizontal rule ────────────────────────────────────────────────
                col.Item().PaddingBottom(16).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                // ── Mandatory fields (AC-002) ──────────────────────────────────────
                col.Item().PaddingBottom(24).Column(fields =>
                {
                    AddField(fields, "Patient",    _data.PatientFullName);
                    AddField(fields, "Date & Time",
                        _data.AppointmentDateTime.ToString("dddd, MMMM d, yyyy 'at' h:mm tt"));
                    AddField(fields, "Location",   _data.ClinicAddress);
                    AddField(fields, "Provider",   _data.ProviderName ?? "To be assigned");
                    AddField(fields, "Reference",  _data.BookingReferenceId.ToString());
                });

                // ── QR code cell (AC-002 — encodes booking reference) ──────────────
                // QRCodeGenerator and PngByteQRCode are disposed after use (resource management; checklist)
                using var qrGenerator = new QRCodeGenerator();
                var qrData  = qrGenerator.CreateQrCode(
                    _data.BookingReferenceId.ToString(),
                    QRCodeGenerator.ECCLevel.Q);
                using var pngQr    = new PngByteQRCode(qrData);
                var       pngBytes = pngQr.GetGraphic(5, true);

                col.Item().Row(row =>
                {
                    row.RelativeItem(); // left spacer — pushes QR code to the right

                    row.ConstantItem(80).Column(qrCol =>
                    {
                        qrCol.Item()
                             .Width(60)
                             .Height(60)
                             .Image(pngBytes);

                        qrCol.Item()
                             .Text("Scan to verify")
                             .FontSize(7)
                             .FontColor(Colors.Grey.Darken1)
                             .AlignCenter();
                    });
                });
            });
        });
    }

    // ── Helpers ───────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Renders a two-column label/value row in a bold label + normal value style.
    /// </summary>
    private static void AddField(ColumnDescriptor col, string label, string value)
    {
        col.Item().PaddingBottom(8).Row(row =>
        {
            row.ConstantItem(100)
               .Text(label + ":")
               .Bold()
               .FontColor(Colors.Grey.Darken2);

            row.RelativeItem()
               .Text(value);
        });
    }
}
