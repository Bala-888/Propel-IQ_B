using Api.Services;
using System.Text.Json;

namespace Api.Infrastructure.Auth;

/// <summary>
/// Handles JSON 403 responses for role-based authorization failures.
/// Invoked from <c>JwtBearerOptions.Events.OnForbidden</c> in Program.cs so that
/// HTTP 403 responses carry a structured JSON body, an audit log entry, and increment
/// the per-IP threshold counter (AC-001, AC-004, AC-005).
/// </summary>
/// <remarks>
/// Registered as Scoped so it shares the request lifetime with
/// <see cref="RepeatedUnauthorizedAccessTracker"/> and <see cref="IAuditLogger"/>.
/// </remarks>
public sealed class JsonAuthorizationMiddlewareResultHandler
{
    private readonly IAuditLogger _auditLogger;
    private readonly RepeatedUnauthorizedAccessTracker _tracker;

    public JsonAuthorizationMiddlewareResultHandler(
        IAuditLogger auditLogger,
        RepeatedUnauthorizedAccessTracker tracker)
    {
        _auditLogger = auditLogger;
        _tracker     = tracker;
    }

    /// <summary>
    /// Writes a JSON 403 response, persists an audit entry, and tracks the source IP.
    /// Must be called BEFORE any bytes are written to <paramref name="context"/>.
    /// </summary>
    public async Task HandleForbiddenAsync(HttpContext context)
    {
        var actorId    = context.User.FindFirst("sub")?.Value ?? "unknown";
        var actorRole  = context.User.FindFirst("role")?.Value ?? "unknown";
        // Route value "controller" gives the controller name without the "Controller" suffix,
        // which serves as the ResourceType for the audit entry (AC-004).
        var controller = context.GetRouteValue("controller") as string ?? "unknown";

        // AC-004: audit entry written BEFORE the response body — persisted even if the
        // response stream write subsequently fails (OWASP A09 — audit completeness).
        _auditLogger.Log(
            actorId,
            "UnauthorizedAccess",
            controller,
            JsonSerializer.Serialize(new { ActorRole = actorRole }));

        // AC-005: increment per-IP 403 counter and insert AdminNotification on threshold.
        var sourceIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        await _tracker.TrackAndAlertAsync(sourceIp);

        // AC-001: exact error body as specified — no deviation in casing or punctuation.
        context.Response.StatusCode  = StatusCodes.Status403Forbidden;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Access denied. Insufficient role." });
    }
}
