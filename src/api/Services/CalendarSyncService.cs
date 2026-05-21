using System.Net.Http.Headers;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Implements calendar event lifecycle (create/update/delete) for Google Calendar and
/// Microsoft Graph APIs (us_028; AC-001–AC-005).
///
/// <para>
/// <b>Token lifecycle</b>: <see cref="ResolveTokenAsync"/> loads the encrypted
/// <see cref="PatientCalendarToken"/> row, decrypts via <see cref="IPhiEncryptionService"/>,
/// and refreshes if <c>TokenExpiry &lt;= UtcNow + 60 s</c>.  The raw access token is kept
/// only as a local variable inside the method call — it is never logged or added to response
/// bodies (OWASP A02; HIPAA minimum-necessary).
/// </para>
///
/// <para>
/// <b>Failure isolation</b>: all <see cref="HttpRequestException"/> and non-success HTTP
/// responses are caught; <c>booking_calendar_syncs</c> is set to <c>Status = "Failed"</c>;
/// <c>CalendarSyncFailed</c> is logged; the exception is NOT propagated (AC-005; OWASP A04).
/// </para>
///
/// <para>
/// <b>HttpClient</b>: named instances <c>"GoogleCalendar"</c> and <c>"MicrosoftGraph"</c>
/// are resolved from <see cref="IHttpClientFactory"/> — no <c>new HttpClient()</c> ever
/// constructed (OWASP A03; socket exhaustion prevention).
/// </para>
/// </summary>
public sealed class CalendarSyncService : ICalendarSyncService
{
    private readonly AppDbContext               _db;
    private readonly IPhiEncryptionService      _phi;
    private readonly IHttpClientFactory         _httpFactory;
    private readonly ILogger<CalendarSyncService> _logger;

    private const string ClinicName    = "PropelIQ Clinic";
    private const string ClinicAddress = "123 Health Ave, Suite 100";

    public CalendarSyncService(
        AppDbContext                  db,
        IPhiEncryptionService         phi,
        IHttpClientFactory            httpFactory,
        ILogger<CalendarSyncService>  logger)
    {
        _db          = db;
        _phi         = phi;
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    // ── SyncAsync (AC-001, AC-002) ────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SyncResult> SyncAsync(
        int               bookingId,
        int               patientId,
        string            provider,
        CancellationToken ct = default)
    {
        var booking = await _db.Bookings
            .Include(b => b.AppointmentSlot)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null)
        {
            _logger.LogWarning(
                "CalendarSyncFailed: booking not found. BookingId={BookingId} Provider={Provider}",
                bookingId, provider);
            return SyncResult.Failed;
        }

        var (accessToken, tokenResult) = await ResolveTokenAsync(patientId, provider, ct);
        if (tokenResult != SyncResult.Success)
        {
            // Persist TokenExpired status so GET /api/calendar/sync/status can signal the frontend
            // to show the reconnect banner (Edge: expired token; task_002).
            await UpsertSyncRowAsync(bookingId, provider, externalEventId: null, status: "TokenExpired", ct);
            return tokenResult;
        }

        var slotStart = booking.AppointmentSlot.SlotStart;
        var slotEnd   = booking.AppointmentSlot.SlotEnd;

        HttpResponseMessage response;
        try
        {
            response = await PostEventAsync(
                accessToken!, provider, bookingId, slotStart, slotEnd, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CalendarSyncFailed: BookingId={BookingId} Provider={Provider}",
                bookingId, provider);
            await UpsertSyncRowAsync(bookingId, provider, externalEventId: null, status: "Failed", ct);
            return SyncResult.Failed;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CalendarSyncFailed: BookingId={BookingId} Provider={Provider} StatusCode={StatusCode}",
                bookingId, provider, (int)response.StatusCode);
            await UpsertSyncRowAsync(bookingId, provider, externalEventId: null, status: "Failed", ct);
            return SyncResult.Failed;
        }

        var body            = await response.Content.ReadAsStringAsync(ct);
        using var doc       = JsonDocument.Parse(body);
        var externalEventId = doc.RootElement.GetProperty("id").GetString();

        await UpsertSyncRowAsync(bookingId, provider, externalEventId, status: "Synced", ct);

        _logger.LogInformation(
            "CalendarSynced: BookingId={BookingId} Provider={Provider}",
            bookingId, provider);
        return SyncResult.Success;
    }

    // ── UpdateAsync (AC-003) ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SyncResult> UpdateAsync(
        int               bookingId,
        string            provider,
        CancellationToken ct = default)
    {
        var syncRow = await _db.BookingCalendarSyncs
            .FirstOrDefaultAsync(
                s => s.BookingId == bookingId && s.Provider == provider && s.Status == "Synced",
                ct);

        if (syncRow is null || string.IsNullOrWhiteSpace(syncRow.ExternalEventId))
        {
            // No synced event to update — idempotent no-op (AC-003 guard)
            return SyncResult.Success;
        }

        var booking = await _db.Bookings
            .Include(b => b.AppointmentSlot)
            .Include(b => b.Patient)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null)
            return SyncResult.Failed;

        var (accessToken, tokenResult) = await ResolveTokenAsync(booking.PatientId, provider, ct);
        if (tokenResult != SyncResult.Success)
            return tokenResult;

        var slotStart = booking.AppointmentSlot.SlotStart;
        var slotEnd   = booking.AppointmentSlot.SlotEnd;

        HttpResponseMessage response;
        try
        {
            response = await PatchEventAsync(
                accessToken!, provider, syncRow.ExternalEventId, bookingId, slotStart, slotEnd, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CalendarSyncFailed: Channel=Update BookingId={BookingId} Provider={Provider}",
                bookingId, provider);
            return SyncResult.Failed;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CalendarSyncFailed: Channel=Update BookingId={BookingId} Provider={Provider} StatusCode={StatusCode}",
                bookingId, provider, (int)response.StatusCode);
            return SyncResult.Failed;
        }

        syncRow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CalendarUpdated: BookingId={BookingId} Provider={Provider}",
            bookingId, provider);
        return SyncResult.Success;
    }

    // ── DeleteAsync (AC-004) ──────────────────────────────────────────────────────────────────

    /// <inheritdoc />
    public async Task<SyncResult> DeleteAsync(
        int               bookingId,
        string            provider,
        CancellationToken ct = default)
    {
        var syncRow = await _db.BookingCalendarSyncs
            .FirstOrDefaultAsync(
                s => s.BookingId == bookingId && s.Provider == provider && s.Status == "Synced",
                ct);

        if (syncRow is null || string.IsNullOrWhiteSpace(syncRow.ExternalEventId))
        {
            // No synced event — idempotent success (safe to call on uncalendared bookings)
            return SyncResult.Success;
        }

        var booking = await _db.Bookings
            .Include(b => b.Patient)
            .FirstOrDefaultAsync(b => b.Id == bookingId, ct);

        if (booking is null)
            return SyncResult.Failed;

        var (accessToken, tokenResult) = await ResolveTokenAsync(booking.PatientId, provider, ct);
        if (tokenResult != SyncResult.Success)
            return tokenResult;

        HttpResponseMessage response;
        try
        {
            response = await DeleteEventAsync(accessToken!, provider, syncRow.ExternalEventId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "CalendarSyncFailed: Channel=Delete BookingId={BookingId} Provider={Provider}",
                bookingId, provider);
            return SyncResult.Failed;
        }

        // Google Calendar DELETE returns 204 No Content; Graph DELETE also returns 204.
        // Treat 404 as success — event may already have been deleted on the provider side.
        if (!response.IsSuccessStatusCode && response.StatusCode != System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning(
                "CalendarSyncFailed: Channel=Delete BookingId={BookingId} Provider={Provider} StatusCode={StatusCode}",
                bookingId, provider, (int)response.StatusCode);
            return SyncResult.Failed;
        }

        syncRow.Status    = "Deleted";
        syncRow.UpdatedAt = DateTimeOffset.UtcNow;
        await _db.SaveChangesAsync(ct);

        _logger.LogInformation(
            "CalendarSyncDeleted: BookingId={BookingId} Provider={Provider}",
            bookingId, provider);
        return SyncResult.Success;
    }

    // ── OAuth token lifecycle ─────────────────────────────────────────────────────────────────

    /// <summary>
    /// Loads, decrypts, and (if expired) refreshes the OAuth access token for
    /// <paramref name="patientId"/> on the given <paramref name="provider"/>.
    /// </summary>
    private async Task<(string? AccessToken, SyncResult Status)> ResolveTokenAsync(
        int patientId, string provider, CancellationToken ct)
    {
        var tokenRow = await _db.PatientCalendarTokens
            .FirstOrDefaultAsync(t => t.PatientId == patientId && t.Provider == provider, ct);

        if (tokenRow is null)
        {
            _logger.LogWarning(
                "CalendarTokenRefreshFailed: no token row found. PatientId={PatientId} Provider={Provider}",
                patientId, provider);
            return (null, SyncResult.TokenExpired);
        }

        // Refresh if the access token expires within the next 60 seconds
        if (tokenRow.TokenExpiry <= DateTimeOffset.UtcNow.AddSeconds(60))
        {
            var refreshed = await RefreshTokenAsync(patientId, provider, tokenRow, ct);
            if (!refreshed)
                return (null, SyncResult.TokenExpired);
        }

        var accessToken = _phi.Decrypt(tokenRow.EncryptedAccessToken);
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            _logger.LogWarning(
                "CalendarTokenRefreshFailed: decrypted access token is empty. " +
                "PatientId={PatientId} Provider={Provider}",
                patientId, provider);
            return (null, SyncResult.TokenExpired);
        }

        return (accessToken, SyncResult.Success);
    }

    /// <summary>
    /// Calls the provider token endpoint with the stored refresh token.
    /// On success, re-encrypts and persists updated tokens.
    /// On failure, logs <c>CalendarTokenRefreshFailed</c> (OWASP A02 — refresh token never logged).
    /// </summary>
    private async Task<bool> RefreshTokenAsync(
        int patientId, string provider, PatientCalendarToken tokenRow, CancellationToken ct)
    {
        var refreshToken = _phi.Decrypt(tokenRow.EncryptedRefreshToken);
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning(
                "CalendarTokenRefreshFailed: encrypted refresh token is null or empty. " +
                "PatientId={PatientId} Provider={Provider}",
                patientId, provider);
            return false;
        }

        var tokenEndpoint = provider == "Google"
            ? "https://oauth2.googleapis.com/token"
            : "https://login.microsoftonline.com/common/oauth2/v2.0/token";

        // OAuth client credentials are injected via environment variables (OWASP A02; never hardcoded)
        var clientId     = Environment.GetEnvironmentVariable(
            provider == "Google" ? "GOOGLE_OAUTH_CLIENT_ID"     : "OUTLOOK_OAUTH_CLIENT_ID") ?? string.Empty;
        var clientSecret = Environment.GetEnvironmentVariable(
            provider == "Google" ? "GOOGLE_OAUTH_CLIENT_SECRET" : "OUTLOOK_OAUTH_CLIENT_SECRET") ?? string.Empty;

        // Use default (unnamed) HttpClient for token refresh — the endpoint is outside the
        // base addresses of the named "GoogleCalendar"/"MicrosoftGraph" clients (OWASP A03)
        var http = _httpFactory.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"]    = "refresh_token",
                ["refresh_token"] = refreshToken,
                ["client_id"]     = clientId,
                ["client_secret"] = clientSecret,
            })
        };

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (Exception ex)
        {
            // Log only patientId + provider — never the refresh token value (OWASP A02)
            _logger.LogError(ex,
                "CalendarTokenRefreshFailed: network error. PatientId={PatientId} Provider={Provider}",
                patientId, provider);
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "CalendarTokenRefreshFailed: PatientId={PatientId} Provider={Provider} StatusCode={StatusCode}",
                patientId, provider, (int)response.StatusCode);
            return false;
        }

        using var doc       = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var newAccess       = doc.RootElement.GetProperty("access_token").GetString();
        var expiresIn       = doc.RootElement.TryGetProperty("expires_in", out var ei) ? ei.GetInt32() : 3600;
        var newRefreshToken = doc.RootElement.TryGetProperty("refresh_token", out var rt)
            ? rt.GetString() : null;

        tokenRow.EncryptedAccessToken = _phi.Encrypt(newAccess);
        tokenRow.TokenExpiry          = DateTimeOffset.UtcNow.AddSeconds(expiresIn);

        // Some providers (Google) only return a new refresh token on first authorization
        if (!string.IsNullOrWhiteSpace(newRefreshToken))
            tokenRow.EncryptedRefreshToken = _phi.Encrypt(newRefreshToken);

        await _db.SaveChangesAsync(ct);
        return true;
    }

    // ── HTTP helpers ──────────────────────────────────────────────────────────────────────────

    private async Task<HttpResponseMessage> PostEventAsync(
        string accessToken, string provider, int bookingId,
        DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(
            provider == "Google" ? "GoogleCalendar" : "MicrosoftGraph");

        using var request = BuildEventRequest(
            HttpMethod.Post,
            path: provider == "Google" ? "calendar/v3/calendars/primary/events" : "me/events",
            accessToken, provider, bookingId, slotStart, slotEnd);

        return await http.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> PatchEventAsync(
        string accessToken, string provider, string eventId, int bookingId,
        DateTime slotStart, DateTime slotEnd, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(
            provider == "Google" ? "GoogleCalendar" : "MicrosoftGraph");

        var patchPath = provider == "Google"
            ? $"calendar/v3/calendars/primary/events/{Uri.EscapeDataString(eventId)}"
            : $"me/events/{Uri.EscapeDataString(eventId)}";

        using var request = BuildEventRequest(
            HttpMethod.Patch, patchPath, accessToken, provider, bookingId, slotStart, slotEnd);

        return await http.SendAsync(request, ct);
    }

    private async Task<HttpResponseMessage> DeleteEventAsync(
        string accessToken, string provider, string eventId, CancellationToken ct)
    {
        var http = _httpFactory.CreateClient(
            provider == "Google" ? "GoogleCalendar" : "MicrosoftGraph");

        var deletePath = provider == "Google"
            ? $"calendar/v3/calendars/primary/events/{Uri.EscapeDataString(eventId)}"
            : $"me/events/{Uri.EscapeDataString(eventId)}";

        using var request = new HttpRequestMessage(HttpMethod.Delete, deletePath);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        return await http.SendAsync(request, ct);
    }

    private static HttpRequestMessage BuildEventRequest(
        HttpMethod method, string path, string accessToken,
        string provider, int bookingId, DateTime slotStart, DateTime slotEnd)
    {
        object body = provider == "Google"
            ? new
            {
                summary     = $"Appointment at {ClinicName}",
                start       = new { dateTime = slotStart.ToString("o"), timeZone = "UTC" },
                end         = new { dateTime = slotEnd.ToString("o"),   timeZone = "UTC" },
                description = $"{ClinicAddress} | Ref: {bookingId}",
            }
            : new
            {
                subject = $"Appointment at {ClinicName}",
                start   = new { dateTime = slotStart.ToString("o"), timeZone = "UTC" },
                end     = new { dateTime = slotEnd.ToString("o"),   timeZone = "UTC" },
                body    = new { contentType = "Text", content = $"{ClinicAddress} | Ref: {bookingId}" },
            };

        var request = new HttpRequestMessage(method, path)
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return request;
    }

    // ── Persistence helper ────────────────────────────────────────────────────────────────────

    private async Task UpsertSyncRowAsync(
        int bookingId, string provider, string? externalEventId, string status, CancellationToken ct)
    {
        var existing = await _db.BookingCalendarSyncs
            .FirstOrDefaultAsync(s => s.BookingId == bookingId && s.Provider == provider, ct);

        var now = DateTimeOffset.UtcNow;

        if (existing is null)
        {
            _db.BookingCalendarSyncs.Add(new BookingCalendarSync
            {
                BookingId       = bookingId,
                Provider        = provider,
                ExternalEventId = externalEventId,
                Status          = status,
                CreatedAt       = now,
                UpdatedAt       = now,
            });
        }
        else
        {
            existing.ExternalEventId = externalEventId ?? existing.ExternalEventId;
            existing.Status          = status;
            existing.UpdatedAt       = now;
        }

        await _db.SaveChangesAsync(ct);
    }
}
