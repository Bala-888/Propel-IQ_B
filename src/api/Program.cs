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
using Api.Data;
using Api.Exceptions;
using Api.Middleware;
using Api.Repositories;
using Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Startup guard: fail fast if JWT_SECRET is absent — must precede service registration (Edge: JWT_SECRET missing)
if (string.IsNullOrWhiteSpace(builder.Configuration["JWT_SECRET"]))
{
    Console.Error.WriteLine("FATAL: JWT_SECRET is not configured");
    Environment.Exit(1);
}

// Serilog: read sink/enricher configuration from appsettings.json; replaces default ILogger (AC-003, AC-004)
builder.Host.UseSerilog((ctx, lc) => lc.ReadFrom.Configuration(ctx.Configuration));

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
            // Edge: JWT expiry → must return HTTP 401 {"error":"Token expired"}, NOT 403 (OWASP A07)
            OnChallenge = async ctx =>
            {
                ctx.HandleResponse();
                ctx.Response.StatusCode  = StatusCodes.Status401Unauthorized;
                ctx.Response.ContentType = "application/json";
                var isExpired = ctx.AuthenticateFailure is SecurityTokenExpiredException;
                await ctx.Response.WriteAsJsonAsync(
                    isExpired
                        ? new { error = "Token expired" }
                        : (object)new { error = "Unauthorized" });
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
// Centralised audit logging — OWASP A09: all audit events flow through IAuditLogger (AC-004)
builder.Services.AddSingleton<IAuditLogger, AuditLoggerService>();
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
app.MapControllers();

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
