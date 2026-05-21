using QuestPDF.Fluent;

namespace Api.Services;

/// <summary>
/// Generates appointment confirmation PDFs using QuestPDF (us_022; AC-002).
///
/// <para>
/// The synchronous QuestPDF render runs inside <see cref="Task.Run"/> so the method is safely
/// awaitable from async call sites and does not block I/O ThreadPool threads. The
/// <see cref="CancellationToken"/> is forwarded to <see cref="Task.Run"/>: if cancelled before
/// the render starts, <see cref="OperationCanceledException"/> is thrown immediately; if
/// cancelled mid-render, the exception propagates to the caller (Edge: PDF timeout; AC-002).
/// </para>
/// </summary>
public sealed class ConfirmationPdfService : IConfirmationPdfService
{
    /// <inheritdoc />
    public Task<byte[]> GenerateAsync(ConfirmationData data, CancellationToken ct = default)
    {
        // Task.Run offloads the synchronous QuestPDF render to a ThreadPool thread (AC-002; performance).
        // The CancellationToken is passed to Task.Run — pre-start cancellation throws OperationCanceledException
        // before any rendering work begins (Edge: PDF timeout propagation; checklist).
        return Task.Run(
            () =>
            {
                var document = new ConfirmationPdfDocument(data);
                // Document.Create delegates to IDocument.Compose then calls GeneratePdf (AC-002)
                return Document.Create(document.Compose).GeneratePdf();
            },
            ct);
    }
}
