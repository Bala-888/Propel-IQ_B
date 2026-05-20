using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.HttpOverrides;
using Prometheus;
using Serilog;
using Api.Middleware;
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
