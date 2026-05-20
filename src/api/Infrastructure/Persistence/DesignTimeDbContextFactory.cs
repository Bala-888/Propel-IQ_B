using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Npgsql;
using Pgvector;

namespace Upacip.Api.Infrastructure.Persistence;

/// <summary>
/// Used by EF Core design-time tools (dotnet ef migrations add / update).
/// Not used at runtime — the application uses NpgsqlDataSourceBuilder in Program.cs.
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // Build a minimal IConfiguration for design-time use
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connStr = config.GetConnectionString("DefaultConnection")
            ?? "Host=localhost;Port=5432;Database=upacip;Username=upacip_user;Password=change_me";

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connStr);
        dataSourceBuilder.UseVector();
        var dataSource = dataSourceBuilder.Build();

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(dataSource);

        return new AppDbContext(optionsBuilder.Options, config);
    }
}
