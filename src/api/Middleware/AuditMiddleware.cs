using Api.Audit;
using Api.Exceptions;

namespace Api.Middleware;

/// <summary>
/// Intercepts every authenticated and authorized request, writes an audit log entry via
/// <see cref="IAuditLogger.RecordAsync"/> <b>before</b> calling the next middleware, then
/// calls <c>next(context)</c> to execute the controller action (AC-001, AC-002).
/// </summary>
/// <remarks>
/// Audit-before-action ordering: if <see cref="IAuditLogger.RecordAsync"/> throws
/// <see cref="AuditLogUnavailableException"/>, the middleware returns HTTP 503 and
/// <c>next</c> is never invoked — the underlying data action is not completed (AC-002;
/// HIPAA §164.312(b); OWASP A09).
/// Registered via <c>app.UseWhen(IsAuthenticated, ...)</c> so unauthenticated routes
/// (e.g. <c>POST /auth/login</c>) are excluded; those controllers call
/// <see cref="IAuditLogger.RecordAsync"/> directly with <see cref="AuditActionTypes.LoginSuccess"/>
/// or <see cref="AuditActionTypes.LoginFailure"/> (AC-005).
/// </remarks>
public sealed class AuditMiddleware : IMiddleware
{
    private readonly IAuditLogger _auditLogger;

    public AuditMiddleware(IAuditLogger auditLogger)
    {
        _auditLogger = auditLogger;
    }

    /// <inheritdoc />
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        // Extract actor context from JWT claims — MapInboundClaims=false keeps short-form names.
        var actorId   = context.User.FindFirst("sub")?.Value  ?? "unknown";
        var actorRole = context.User.FindFirst("role")?.Value ?? "unknown";

        // Extract resource context from route data.
        var controller = context.GetRouteValue("controller") as string ?? "unknown";
        var resourceId = context.GetRouteValue("id")?.ToString() ?? "none";

        var entry = new AuditEntry(
            ActorId:      actorId,
            ActorRole:    actorRole,
            ActionType:   DeriveActionType(context, controller),
            ResourceType: controller,
            ResourceId:   resourceId,
            IpAddress:    context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            // User-Agent is not PHI; it is structural metadata for the audit trail (AC-001).
            UserAgent:    context.Request.Headers.UserAgent.ToString(),
            // Application-side UTC timestamp — not delegated to DB default (AC-001; checklist).
            OccurredAt:   DateTime.UtcNow);

        try
        {
            // AC-002: RecordAsync is called BEFORE next() — the action is never executed if
            // the audit write fails; this satisfies HIPAA §164.312(b) audit-before-action ordering.
            await _auditLogger.RecordAsync(entry, context.RequestAborted);
        }
        catch (AuditLogUnavailableException)
        {
            // AC-002: exact error body as specified; no stack trace in response (OWASP A09).
            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
                context.Response.ContentType = "application/json";
                await context.Response.WriteAsJsonAsync(
                    new { error = "Action could not be completed. Audit logging unavailable." });
            }
            // Do NOT call next — the protected action must not proceed (AC-002).
            return;
        }

        await next(context);
    }

    /// <summary>
    /// Derives the <see cref="AuditActionTypes"/> constant from the request's route and HTTP method.
    /// Controllers that require finer-grained action types (e.g. LoginSuccess vs LoginFailure)
    /// call <see cref="IAuditLogger.RecordAsync"/> directly (AC-005).
    /// </summary>
    private static string DeriveActionType(HttpContext context, string controller) =>
        controller.ToLowerInvariant() switch
        {
            "patients"   => AuditActionTypes.PatientDataAccess,
            "walkins"    => context.Request.Method.Equals("POST", StringComparison.OrdinalIgnoreCase)
                               ? AuditActionTypes.BookingCreate
                               : AuditActionTypes.BookingModify,
            "adminusers" => AuditActionTypes.AdminUserCRUD,
            _            => AuditActionTypes.PatientDataAccess,   // safe default for authenticated routes
        };
}
