using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Api.Data;

/// <summary>
/// Design-time factory used exclusively by dotnet-ef CLI tooling (migrations add, migrations remove).
/// Not invoked at runtime — the DI-registered AppDbContext is used instead.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Set placeholder key so PhiEncryptionService can initialise during migration scaffolding.
        // This value is never used for real data — it only satisfies the env-var guard.
        if (Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY") is null)
            Environment.SetEnvironmentVariable("PHI_ENCRYPTION_KEY", "design-time-placeholder-32ch!");

        var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
            ?? "Host=localhost;Database=upacip_dev;Username=postgres;Password=postgres";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(connStr, o => o.UseVector())
            .UseSnakeCaseNamingConvention()
            .Options;

        return new AppDbContext(options, new PhiEncryptionService());
    }
}
