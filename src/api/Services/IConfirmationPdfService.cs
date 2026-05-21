namespace Api.Services;

/// <summary>
/// Generates a single-page appointment confirmation PDF containing all AC-002 mandatory fields
/// and a QR code encoding the booking reference (us_022; AC-002).
///
/// <para>
/// Implementations must run the synchronous QuestPDF render on a ThreadPool thread so the
/// method is safely awaitable from async call sites (AC-002; Edge: PDF timeout).
/// </para>
/// </summary>
public interface IConfirmationPdfService
{
    /// <summary>
    /// Renders the confirmation PDF and returns it as a byte array.
    /// </summary>
    /// <param name="data">Scheduling and identity data to include in the document (AC-002).</param>
    /// <param name="ct">
    /// Cancellation token. If cancelled before the <c>Task.Run</c> starts, an
    /// <see cref="OperationCanceledException"/> is thrown immediately. If cancelled mid-render,
    /// the exception propagates to the caller — the caller (email service) handles the
    /// no-attachment path (Edge: PDF generation timeout).
    /// </param>
    /// <returns>Raw PDF bytes beginning with the <c>%PDF</c> header.</returns>
    Task<byte[]> GenerateAsync(ConfirmationData data, CancellationToken ct = default);
}
