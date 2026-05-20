using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Pgvector;
using Prometheus;
using Serilog;
using System.Text;
using System.Threading.RateLimiting;
using Upacip.Api.Infrastructure.Persistence;

// ─── Bootstrap Serilog ────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);

    // ─── Serilog (TASK-006) ───────────────────────────────────────────────────
    builder.Host.UseSerilog((ctx, services, config) =>
    {
        config
            .ReadFrom.Configuration(ctx.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext()
            .Enrich.WithMachineName()
            .WriteTo.Console(outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .WriteTo.Seq(ctx.Configuration["Seq:ServerUrl"] ?? "http://seq:5341");
    });

    // ─── Database (TASK-008, TASK-009) ────────────────────────────────────────
    var connStr = builder.Configuration.GetConnectionString("DefaultConnection")!;
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connStr);
    dataSourceBuilder.UseVector();
    var dataSource = dataSourceBuilder.Build();

    builder.Services.AddDbContext<AppDbContext>((sp, opts) =>
    {
        opts.UseNpgsql(dataSource);
    });

    // ─── JWT Authentication (TASK-015) ────────────────────────────────────────
    var jwtKey = builder.Configuration["Jwt:Key"]
        ?? throw new InvalidOperationException("Jwt:Key is required.");

    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(opts =>
        {
            opts.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
                ClockSkew = TimeSpan.Zero  // strict 15-min expiry
            };

            // Allow SignalR to pass token via query string (TASK-056)
            opts.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var accessToken = context.Request.Query["access_token"];
                    var path = context.HttpContext.Request.Path;
                    if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                        context.Token = accessToken;
                    return Task.CompletedTask;
                }
            };
        });

    builder.Services.AddAuthorization();

    // ─── CORS ─────────────────────────────────────────────────────────────────
    builder.Services.AddCors(opts =>
    {
        opts.AddPolicy("Frontend", policy =>
        {
            var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
                          ?? ["http://localhost:5173", "https://localhost"];
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();  // Required for SignalR
        });
    });

    // ─── Rate Limiting (TASK-017) ─────────────────────────────────────────────
    builder.Services.AddRateLimiter(opts =>
    {
        opts.AddFixedWindowLimiter("AuthPolicy", limiterOpts =>
        {
            limiterOpts.PermitLimit = 5;
            limiterOpts.Window = TimeSpan.FromMinutes(15);
            limiterOpts.QueueLimit = 0;
        });
        opts.RejectionStatusCode = 429;
    });

    // ─── SignalR (TASK-056) ───────────────────────────────────────────────────
    builder.Services.AddSignalR();

    // ─── Controllers ──────────────────────────────────────────────────────────
    builder.Services.AddControllers();

    // ─── Health checks ────────────────────────────────────────────────────────
    builder.Services.AddHealthChecks()
        .AddDbContextCheck<AppDbContext>();

    // ─── HTTP Context Accessor (needed by AuditLogger - TASK-024) ────────────
    builder.Services.AddHttpContextAccessor();

    // ─── Memory Cache ─────────────────────────────────────────────────────────
    builder.Services.AddMemoryCache();

    // ─── Build application ────────────────────────────────────────────────────
    var app = builder.Build();

    // ─── Middleware pipeline ──────────────────────────────────────────────────
    app.UseSerilogRequestLogging(opts =>
    {
        opts.MessageTemplate =
            "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    });

    app.UseCors("Frontend");
    app.UseRouting();
    app.UseRateLimiter();
    app.UseAuthentication();
    app.UseAuthorization();

    // Prometheus scrape endpoint at /metrics (TASK-005)
    app.UseHttpMetrics();
    app.MapMetrics();

    app.MapControllers();
    app.MapHealthChecks("/health");

    // SignalR hub — uncomment when QueueHub is implemented (TASK-056)
    // app.MapHub<QueueHub>("/hubs/queue");

    // ─── Apply EF Core migrations on startup ──────────────────────────────────
    if (app.Environment.IsDevelopment() || app.Environment.IsEnvironment("Migration"))
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
    }

    Log.Information("UPACIP API starting — {Environment}", app.Environment.EnvironmentName);
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "UPACIP API failed to start");
}
finally
{
    Log.CloseAndFlush();
}
