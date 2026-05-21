using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Pgvector.EntityFrameworkCore;
using Prometheus;
using Serilog;
using System.Text;
using Api.AI;
using Api.Constants;
using Api.Data;
using Api.Exceptions;
using Api.Infrastructure.Auth;
using Api.Middleware;
using Api.Repositories;
using Api.Services;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);

// Startup guard: fail fast if JWT_SECRET is absent — must precede service registration (Edge: JWT_SECRET missing)
if (string.IsNullOrWhiteSpace(builder.Configuration["JWT_SECRET"]))
{
    Console.Error.WriteLine("FATAL: JWT_SECRET is not configured");
    Environment.Exit(1);
}

// Startup guard: PHI_ENCRYPTION_KEY must supply ≥ 32 bytes of key material for AES-256.
// A shorter key would silently downgrade to AES-128, violating HIPAA §164.312(a)(2)(iv) and OWASP A02.
// Encoding.UTF8.GetByteCount is used instead of .Length because multi-byte UTF-8 characters can satisfy
// a character-count check while providing fewer bytes of actual entropy (Edge: AES key guard; checklist).
var _phiKeyRaw = Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY") ?? string.Empty;
if (System.Text.Encoding.UTF8.GetByteCount(_phiKeyRaw) < 32)
{
    throw new InvalidOperationException(
        "PHI_ENCRYPTION_KEY must be at least 32 bytes for AES-256. The application will not start with a shorter key.");
}

// Serilog: SEQ_URL env var prevents the Seq server URL from being committed to source
// control (AC-003; OWASP A02). Falls back to the Docker Compose service name for local dev.
builder.Host.UseSerilog((ctx, lc) =>
{
    var seqUrl = Environment.GetEnvironmentVariable("SEQ_URL") ?? "http://seq:5341";
    lc.ReadFrom.Configuration(ctx.Configuration)
      .WriteTo.Seq(seqUrl, queueSizeLimit: 500);
});

builder.Services.AddHealthChecks();
// JWT Bearer authentication — ValidateLifetime=true enforces exp claim; ClockSkew=Zero ensures
// exact 15-minute window with no grace period (AC-001; Edge: JWT expiry → 401 not 403)
var jwtSecret = builder.Configuration["JWT_SECRET"]!; // non-null: startup guard above already exited if missing
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateLifetime          = true,
            ValidateIssuerSigningKey  = true,
            IssuerSigningKey          = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateAudience          = false,
            ValidateIssuer            = false,
            // Zero clock skew: token is invalid the instant exp elapses (AC-001)
            ClockSkew                 = TimeSpan.Zero,
            // MapInboundClaims = false: stop the middleware from renaming short-form claims
            // to full-URL ClaimTypes (e.g. keep "role" as "role", not
            // "http://schemas.microsoft.com/ws/2008/06/identity/claims/role").
            // RoleClaimType = "role": tells [Authorize(Roles="...")] where to find the role
            // after mapping is disabled.  NameClaimType = "sub" for User.Identity.Name. (OWASP A01)
            NameClaimType             = JwtRegisteredClaimNames.Sub,
            RoleClaimType             = "role",
        };
        opts.MapInboundClaims = false;
        opts.Events = new JwtBearerEvents
        {
            // SignalR WebSocket connections cannot send Authorization headers.
            // Extract the JWT access_token from the query string for /hubs/queue connections
            // (us_033/AC-005; OWASP A01 — token sourced from in-memory auth context on the client).
            OnMessageReceived = ctx =>
            {
                if (ctx.Request.Path.StartsWithSegments("/hubs/queue"))
                {
                    var token = ctx.Request.Query["access_token"];
                    if (!string.IsNullOrWhiteSpace(token))
                        ctx.Token = token;
                }
                return Task.CompletedTask;
            },
            // Edge: token expiry ordering — expired tokens must yield HTTP 401 BEFORE
            // authorization policy evaluation, preventing a 403 from firing first (OWASP A07).
            // context.HandleResponse() suppresses the default WWW-Authenticate challenge header
            // so the custom JSON body is the only response content (AC-002).
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                // AC-002: exact error body as specified — covers both missing and expired tokens.
                await ctx.Response.WriteAsJsonAsync(new { error = "Authentication required." });
            },
            // AC-001, AC-004, AC-005: authenticated user lacks the required role.
            // Delegates to the scoped JsonAuthorizationMiddlewareResultHandler which writes the
            // JSON body, persists the audit entry, and increments the per-IP 403 counter.
            OnForbidden = async ctx =>
            {
                var handler = ctx.HttpContext.RequestServices
                    .GetRequiredService<JsonAuthorizationMiddlewareResultHandler>();
                await handler.HandleForbiddenAsync(ctx.HttpContext);
            },
        };
    });
builder.Services.AddAuthorization();
// JWT token generation service — key read from JWT_SECRET env var once at startup (OWASP A02)
builder.Services.AddSingleton<ITokenService, TokenService>();
// IDistributedCache backing store for LoginRateLimiterService (AC-004; us_010).
// AddDistributedMemoryCache is in-process — suitable for development / single-instance deployments.
// TODO: Replace AddDistributedMemoryCache with AddStackExchangeRedisCache in multi-instance deployments
//       so the failed-attempt counter is shared across all nodes (Edge: load-balanced instances).
builder.Services.AddDistributedMemoryCache();
// Login failure-specific rate limiter — counts wrong-password / unknown-email 401s per client IP
builder.Services.AddScoped<ILoginRateLimiter, LoginRateLimiterService>();
// IMemoryCache for DatabaseRoleClaimsTransformation — caches DB role lookups with 60-second
// sliding expiration to avoid a DB hit on every authenticated request (Edge: role changed after token).
builder.Services.AddMemoryCache();
// RBAC enforcement layer (us_013/task_001; AC-001–005)
// Scoped service: handles JSON 403 body, audit log entry, and per-IP 403 counter.
// Wired through JwtBearerOptions.Events.OnForbidden above.
builder.Services.AddScoped<JsonAuthorizationMiddlewareResultHandler>();
// Per-record ownership check for patient endpoints (AC-003; OWASP A01).
builder.Services.AddScoped<IOwnershipAuthorizationService, OwnershipAuthorizationService>();
// Validates JWT role claim against live DB role; clears claims on mismatch to force 401 (Edge).
builder.Services.AddTransient<IClaimsTransformation, DatabaseRoleClaimsTransformation>();
// IDistributedCache-backed 403 counter — inserts AdminNotification on threshold breach (AC-005).
builder.Services.AddScoped<IAdminNotificationRepository, AdminNotificationRepository>();
builder.Services.AddScoped<RepeatedUnauthorizedAccessTracker>();
// MVC controllers — InvalidModelStateResponseFactory emits {validationErrors:{...}} (AC-003; OWASP A03)
builder.Services.AddControllers()
    .ConfigureApiBehaviorOptions(options =>
    {
        options.InvalidModelStateResponseFactory = context =>
        {
            var errors = context.ModelState
                .Where(e => e.Value?.Errors.Count > 0)
                .ToDictionary(
                    e => System.Text.Json.JsonNamingPolicy.CamelCase.ConvertName(e.Key),
                    e => e.Value!.Errors.Select(x => x.ErrorMessage).First());
            return new Microsoft.AspNetCore.Mvc.BadRequestObjectResult(new { validationErrors = errors });
        };
    });
// SignalR: real-time queue push to Staff/Admin clients (us_033/AC-001, AC-002, AC-005)
builder.Services.AddSignalR();
// IQueueHubService: scoped so each request scope resolves its own IHubContext wrapper (us_033)
builder.Services.AddScoped<Api.Features.Queue.IQueueHubService, Api.Features.Queue.QueueHubService>();
// Centralised audit logging — OWASP A09: all audit events flow through IAuditLogger (AC-004)
builder.Services.AddSingleton<Api.Services.IAuditLogger, AuditLoggerService>();
// Persistence-capable audit logger (us_014/task_001; AC-001, AC-002, AC-003).
// Scoped to the request lifetime so it shares the AuditDbContext transaction scope.
// AuditDbContext is a dedicated context isolated from AppDbContext (checklist: transaction isolation).
builder.Services.AddDbContext<AuditDbContext>(opt =>
{
    opt.UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!);
    opt.UseSnakeCaseNamingConvention();
});
builder.Services.AddScoped<Api.Audit.IAuditLogger, Api.Audit.PostgresAuditLogger>();
// AuditMiddleware is resolved per-request (IMiddleware requires Scoped or Transient registration).
builder.Services.AddScoped<AuditMiddleware>();
// PHI field encryption service — key sourced from PHI_ENCRYPTION_KEY env var (us_006/AC-004; OWASP A02)
builder.Services.AddSingleton<IPhiEncryptionService, PhiEncryptionService>();
// Patient repository — PHI decryption is transparent via EF Core value converters (us_006/AC-002)
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
// User management service — admin create/patch operations (us_011/AC-001, AC-002, AC-003)
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
// Walk-in booking service — front-desk walk-in creation with optional account provisioning (us_012)
builder.Services.AddScoped<IWalkInService, WalkInService>();
// SMTP email sender — reads SMTP_HOST, SMTP_PORT, SMTP_FROM, SMTP_USERNAME, SMTP_PASSWORD
// from environment variables only; no credentials in source code (us_011/AC-001; OWASP A02).
// No-ops with a warning log when SMTP_HOST is absent (dev environments without SMTP).
builder.Services.AddScoped<IEmailSender, SmtpEmailSender>();
// EF Core + Npgsql + pgvector — connection string from env var; no credentials in source (OWASP A02, us_005/AC-002)
// UseSnakeCaseNamingConventions produces snake_case table/column names matching AC-002 required names
builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")!, o => o.UseVector());
    opt.UseSnakeCaseNamingConvention();
});

// ── AI Intake services (us_016-I) ────────────────────────────────────────────────────────────
// OllamaIntakeClient: typed HttpClient targeting the internal Docker network endpoint.
// Base URL is sourced from OLLAMA_BASE_URL env var only — never hardcoded (AC-003; OWASP A02;
// checklist). A 30-second HttpClient timeout acts as a secondary safety net; the primary timeout
// is the CancellationToken supplied by the controller (Edge: inference timeout).
var ollamaBaseUrl = Environment.GetEnvironmentVariable("OLLAMA_BASE_URL") ?? "http://ollama:11434";
builder.Services.AddHttpClient<OllamaIntakeClient>(client =>
{
    client.BaseAddress = new Uri(ollamaBaseUrl);
    client.Timeout     = TimeSpan.FromSeconds(35); // 35s outer HttpClient safety net; controller CTS fires at 30s
});
// IntakeSessionService: manages IDistributedCache session CRUD with patientId ownership check (AC-001)
builder.Services.AddScoped<IntakeSessionService>();
// IntakeRecordService: upserts encrypted IntakeRecord Draft row after each AI turn (AC-005)
builder.Services.AddScoped<IntakeRecordService>();
// IntakeModeSwitchService: bidirectional AI↔Manual field mapping for POST /intake/mode-switch (us_018)
builder.Services.AddScoped<IntakeModeSwitchService>();
// SlotsService: paginated appointment slot query for GET /slots (us_019)
builder.Services.AddScoped<SlotsService>();
// BookingService: ACID appointment booking transaction for POST /bookings (us_020)
builder.Services.AddScoped<BookingService>();
// InsurancePreCheckService: insurance completeness check for GET /api/insurance/pre-check (us_023)
builder.Services.AddScoped<IInsurancePreCheckService, InsurancePreCheckService>();
// PreferredSlotService: preferred alternative slot registration for POST /api/bookings/{id}/preferred-slot (us_024)
builder.Services.AddScoped<IPreferredSlotService, PreferredSlotService>();
// PatientPreferencesService: partial update for PATCH /api/patients/{id}/preferences (us_029)
builder.Services.AddScoped<IPatientPreferencesService, PatientPreferencesService>();
// WalkinBookingService: ACID walk-in booking with slot lock + duplicate guard (us_030/AC-003; OWASP A04)
builder.Services.AddScoped<Api.Features.Bookings.IWalkinBookingService, Api.Features.Bookings.WalkinBookingService>();
// WalkinPatientService: minimal patient record creation for walk-in pre-fill (us_030/AC-004)
builder.Services.AddScoped<Api.Features.Patients.IWalkinPatientService, Api.Features.Patients.WalkinPatientService>();
// QueueService: same-day queue dashboard query with DTO projection (us_031/AC-001; OWASP A01, A02)
builder.Services.AddScoped<Api.Features.Queue.IQueueService, Api.Features.Queue.QueueService>();
// AdminMetricsService: Admin-only KPI aggregation with 5-second timeout guard (us_034/AC-001, AC-004)
builder.Services.AddScoped<Api.Features.Admin.IAdminMetricsService, Api.Features.Admin.AdminMetricsService>();

// ── No-show risk scoring pipeline (us_021) ───────────────────────────────────────────────────────
// Bounded channel: capacity=1000, FullMode=Wait means writes block if full — TryWrite is used from
// the request path so the 201 response is NEVER delayed by back-pressure (AC-003; checklist).
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<BookingCreatedEvent>(
        new System.Threading.Channels.BoundedChannelOptions(1000)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
        }));
// Worker: singleton BackgroundService; uses IServiceScopeFactory per event to avoid scoped-lifetime leaks
builder.Services.AddHostedService<NoShowRiskScoringWorker>();
// Scoring service: scoped so each worker-created scope resolves a fresh AppDbContext (OWASP A04)
builder.Services.AddScoped<INoShowRiskScoringService, NoShowRiskScoringService>();

// ── Confirmation PDF service (us_022) ────────────────────────────────────────────────────────────
// Community licence must be declared before any Document.Create call (QuestPDF requirement).
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Scoped: each call site resolves a fresh ConfirmationPdfService instance with no shared state (AC-002; OWASP A04)
builder.Services.AddScoped<IConfirmationPdfService, ConfirmationPdfService>();

// ── Confirmation email pipeline (us_022) ─────────────────────────────────────────────────────────
// Bounded channel: capacity=500, FullMode=Wait; TryWrite from request path never blocks the 201 response (AC-003)
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<BookingConfirmedEvent>(
        new System.Threading.Channels.BoundedChannelOptions(500)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
        }));
// Worker: singleton BackgroundService; uses IServiceScopeFactory per event to avoid scoped-lifetime leaks
builder.Services.AddHostedService<ConfirmationEmailWorker>();
// Service: scoped so each worker-created scope resolves a fresh instance (OWASP A04)
builder.Services.AddScoped<IConfirmationEmailService, ConfirmationEmailService>();

// ── Preferred slot released event channel (us_024) ───────────────────────────────────────────────
// Bounded channel consumed by the us_025 monitoring worker; capacity=500, FullMode=Wait.
// TryWrite from BookingService.CancelBookingAsync is fire-and-forget (AC-003).
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<PreferredSlotReleasedEvent>(
        new System.Threading.Channels.BoundedChannelOptions(500)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
        }));

// ── Preferred slot monitor job (us_025) ──────────────────────────────────────────────────────────
// Bind PreferredSlotMonitorOptions from config; set "PreferredSlotMonitor:IntervalMinutes" to 1 in
// test environments to verify swap behaviour without waiting 5 minutes (AC-001 validation plan).
builder.Services.Configure<PreferredSlotMonitorOptions>(
    builder.Configuration.GetSection("PreferredSlotMonitor"));
// Bounded channel: capacity=500, FullMode=Wait; consumed by the us_026 notification worker (AC-005)
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<SlotSwapCompletedEvent>(
        new System.Threading.Channels.BoundedChannelOptions(500)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait,
        }));
// Worker: singleton BackgroundService; creates a fresh scope per tick via IServiceScopeFactory (DI lifetime; OWASP A04)
builder.Services.AddHostedService<PreferredSlotMonitorJob>();
// Swap service: scoped so each job scope resolves a fresh instance with its own AppDbContext (OWASP A04)
builder.Services.AddScoped<IPreferredSlotSwapService, PreferredSlotSwapService>();

// ── Slot swap notification pipeline (us_026) ─────────────────────────────────────────────────────
// Bind SmsSettings from config; set "Sms:SmsGatewayDomain" in appsettings / env (AC-002; OWASP A02)
builder.Services.Configure<SmsSettings>(builder.Configuration.GetSection("Sms"));
// Worker: singleton BackgroundService; creates a fresh scope per event via IServiceScopeFactory (OWASP A04)
builder.Services.AddHostedService<SlotSwapNotificationWorker>();
// Notification service: scoped so each worker scope resolves a fresh AppDbContext instance (OWASP A04)
builder.Services.AddScoped<ISlotSwapNotificationService, SlotSwapNotificationService>();

// ── Appointment reminder job (us_027; AC-001, AC-002) ─────────────────────────────────────────────
// PeriodicTimer job that fires 24-hour and 2-hour pre-appointment reminder notifications.
// Scoped reminder service resolves a fresh AppDbContext per tick via IServiceScopeFactory (OWASP A04).
// SmsSettings already registered above (us_026 — shared by both services).
builder.Services.AddHostedService<AppointmentReminderJob>();
builder.Services.AddScoped<IAppointmentReminderService, AppointmentReminderService>();

// ── Calendar sync pipeline (us_028; AC-001–AC-005) ────────────────────────────────────────────────
// Named HttpClients for Google Calendar API v3 and Microsoft Graph API.
// Base addresses are validated/set at startup — no new HttpClient() in service code (OWASP A03;
// socket exhaustion prevention; TR-013).
builder.Services.AddHttpClient("GoogleCalendar",
    c => c.BaseAddress = new Uri("https://www.googleapis.com/"));
builder.Services.AddHttpClient("MicrosoftGraph",
    c => c.BaseAddress = new Uri("https://graph.microsoft.com/v1.0/"));

// Bounded channel (500) for calendar sync commands — CalendarSyncWorker consumes asynchronously
// so POST /api/calendar/sync always returns 202 immediately (Edge: SCR-007; AC-005).
builder.Services.AddSingleton(
    System.Threading.Channels.Channel.CreateBounded<CalendarSyncCommand>(
        new System.Threading.Channels.BoundedChannelOptions(500)
        {
            FullMode = System.Threading.Channels.BoundedChannelFullMode.Wait
        }));

// Worker: singleton BackgroundService; creates a fresh scope per command via IServiceScopeFactory
builder.Services.AddHostedService<CalendarSyncWorker>();
// Service: scoped so each worker scope and controller request resolves a fresh AppDbContext (OWASP A04)
builder.Services.AddScoped<ICalendarSyncService, CalendarSyncService>();

var app = builder.Build();

// ForwardedHeaders must be first — resolves correct client IP and protocol from Nginx (AC-002).
// KnownNetworks/KnownProxies are cleared so headers forwarded from the Docker bridge network are trusted;
// restrict to specific proxy IPs when deploying outside a trusted internal network.
var forwardedHeadersOptions = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto,
};
forwardedHeadersOptions.KnownNetworks.Clear();
forwardedHeadersOptions.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeadersOptions);

// PHI decryption exception middleware — must be placed before controller routing so it wraps
// all downstream middleware (Edge: key rotation; OWASP A09 — no stack trace in response body).
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (PhiDecryptionException ex)
    {
        if (!context.Response.HasStarted)
        {
            context.Response.StatusCode  = StatusCodes.Status503ServiceUnavailable;
            context.Response.ContentType = "application/json";
            // Return only the safe user-facing message — no inner exception detail (OWASP A09)
            await context.Response.WriteAsJsonAsync(new { error = ex.Message });
        }
    }
});

// Inject X-Service: api on every response (AC-002)
app.UseMiddleware<ServiceHeaderMiddleware>();

// Serilog request logging: enriches each request with RequestPath, StatusCode, Elapsed (AC-003)
// Must be placed before UseRouting so it wraps the full request lifecycle
app.UseSerilogRequestLogging(opts =>
{
    opts.EnrichDiagnosticContext = (dc, ctx) =>
    {
        dc.Set("RequestPath", ctx.Request.Path);
        dc.Set("StatusCode", ctx.Response.StatusCode);
    };
});

// Record HTTP request metrics for Prometheus (AC-004)
app.UseHttpMetrics();

app.UseAuthentication();
app.UseAuthorization();
// Audit middleware — runs for every authenticated+authorized request; writes audit entry
// BEFORE the controller action; returns HTTP 503 if audit write fails (AC-001, AC-002; HIPAA §164.312(b)).
// Placed after UseAuthorization so it only runs when the request has already passed role checks.
// Unauthenticated routes (login, register) call IAuditLogger.RecordAsync directly (AC-005).
app.UseWhen(
    ctx => ctx.User.Identity?.IsAuthenticated == true,
    branch => branch.UseMiddleware<AuditMiddleware>());
app.MapControllers();
// SignalR hub endpoint — JWT authorisation via query-string access_token for WebSocket (us_033/AC-005)
app.MapHub<Api.Hubs.QueueHub>("/hubs/queue");

// Health check endpoint — returns {"status":"Healthy"} (AC-001)
app.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsync(
            System.Text.Json.JsonSerializer.Serialize(new { status = report.Status.ToString() }));
    },
});

// Prometheus metrics endpoint — Content-Type: text/plain; version=0.0.4 (AC-004)
app.MapMetrics("/metrics");

app.Run();
