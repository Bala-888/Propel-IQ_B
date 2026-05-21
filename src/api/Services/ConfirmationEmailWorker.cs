using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Api.Services;

/// <summary>
/// Background worker that consumes <see cref="BookingConfirmedEvent"/> items from the bounded
/// in-process channel and delegates email delivery to <see cref="IConfirmationEmailService"/>
/// (us_022; AC-003).
///
/// <para>
/// A fresh DI scope is created per event so that scoped services (e.g. <c>AppDbContext</c>)
/// are properly disposed without lifetime-mismatch warnings (OWASP A04).
/// </para>
/// </summary>
public sealed class ConfirmationEmailWorker : BackgroundService
{
    private readonly Channel<BookingConfirmedEvent>  _channel;
    private readonly IServiceScopeFactory            _scopeFactory;
    private readonly ILogger<ConfirmationEmailWorker> _logger;

    public ConfirmationEmailWorker(
        Channel<BookingConfirmedEvent>    channel,
        IServiceScopeFactory             scopeFactory,
        ILogger<ConfirmationEmailWorker> logger)
    {
        _channel      = channel;
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("ConfirmationEmailWorker started.");

        await foreach (var evt in _channel.Reader.ReadAllAsync(stoppingToken))
        {
            await ProcessEventAsync(evt, stoppingToken);
        }

        _logger.LogInformation("ConfirmationEmailWorker stopped.");
    }

    private async Task ProcessEventAsync(BookingConfirmedEvent evt, CancellationToken ct)
    {
        try
        {
            await using var scope   = _scopeFactory.CreateAsyncScope();
            var emailService        = scope.ServiceProvider.GetRequiredService<IConfirmationEmailService>();
            await emailService.SendAsync(evt, ct);
        }
        catch (OperationCanceledException)
        {
            // Graceful shutdown — do not log as error
            throw;
        }
        catch (Exception ex)
        {
            // Per-event catch: worker stays alive for subsequent events (AC-003)
            _logger.LogError(ex,
                "ConfirmationEmailWorkerError: unhandled exception processing BookingId={BookingId}.",
                evt.BookingId);
        }
    }
}
