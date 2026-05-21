using System.Threading.Channels;

namespace Api.Services;

/// <summary>
/// Background worker that consumes <see cref="BookingCreatedEvent"/> items from the bounded
/// <see cref="Channel{T}"/> and delegates scoring to <see cref="INoShowRiskScoringService"/>
/// (us_021; AC-001; AC-003; AC-004).
///
/// <para>
/// The worker is a singleton <c>BackgroundService</c>. <see cref="INoShowRiskScoringService"/>
/// is scoped, so a fresh DI scope is created per event via <see cref="IServiceScopeFactory"/>
/// to prevent scoped-service lifetime leaks (OWASP A04 — insecure design prevention; checklist).
/// </para>
///
/// <para>
/// Per-event exceptions are caught and logged with <c>eventType = "RiskScoringFailed"</c> so
/// a single bad booking cannot stall the channel consumer (AC-004; OWASP A09).
/// </para>
/// </summary>
public sealed class NoShowRiskScoringWorker : BackgroundService
{
    private readonly Channel<BookingCreatedEvent> _channel;
    private readonly IServiceScopeFactory        _scopeFactory;
    private readonly ILogger<NoShowRiskScoringWorker> _logger;

    public NoShowRiskScoringWorker(
        Channel<BookingCreatedEvent>      channel,
        IServiceScopeFactory              scopeFactory,
        ILogger<NoShowRiskScoringWorker>  logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("NoShowRiskScoringWorker started.");

        try
        {
            // ReadAllAsync completes when the channel is marked complete or stoppingToken fires
            await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                // Per-event isolation — a scoring failure must never stop the consumer loop (AC-004)
                try
                {
                    await ProcessEventAsync(evt, stoppingToken);
                }
                catch (Exception ex)
                {
                    // Structured error log — includes eventType property for Seq filtering (AC-004; OWASP A09)
                    // HIPAA: only booking/scheduling IDs are logged — no PHI
                    _logger.LogError(ex,
                        "RiskScoringFailed for BookingId {BookingId}. eventType=RiskScoringFailed",
                        evt.BookingId);
                    // Booking columns retain null/Unknown defaults — no update is issued on this path (AC-004)
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — host is stopping; swallow so the service exits cleanly
            _logger.LogInformation("NoShowRiskScoringWorker stopping.");
        }
    }

    /// <summary>
    /// Creates a fresh DI scope, resolves a scoped <see cref="INoShowRiskScoringService"/>,
    /// and delegates computation for the given event. The scope is disposed after the call.
    /// </summary>
    private async Task ProcessEventAsync(BookingCreatedEvent evt, CancellationToken ct)
    {
        // IServiceScopeFactory.CreateScope() — required because this BackgroundService is singleton
        // and INoShowRiskScoringService + AppDbContext are scoped (AC-001; checklist; OWASP A04)
        await using var scope   = _scopeFactory.CreateAsyncScope();
        var             service = scope.ServiceProvider.GetRequiredService<INoShowRiskScoringService>();

        await service.ComputeAndPersistAsync(evt, ct);
    }
}
