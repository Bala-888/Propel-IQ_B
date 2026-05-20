using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Prometheus;
using Serilog;
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
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
// Centralised audit logging — OWASP A09: all audit events flow through IAuditLogger (AC-004)
builder.Services.AddSingleton<IAuditLogger, AuditLoggerService>();
// PHI field encryption service — key sourced from PHI_ENCRYPTION_KEY env var (us_006/AC-004; OWASP A02)
builder.Services.AddSingleton<IPhiEncryptionService, PhiEncryptionService>();
// Patient repository — PHI decryption is transparent via EF Core value converters (us_006/AC-002)
builder.Services.AddScoped<IPatientRepository, PatientRepository>();
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
