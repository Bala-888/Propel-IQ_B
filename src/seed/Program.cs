using Npgsql;
using Seed;

// OWASP A02: connection string sourced exclusively from environment variable —
// no literal credentials in source code
var connStr = Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? throw new InvalidOperationException(
        "POSTGRES_CONNECTION_STRING environment variable is not set. " +
        "Export the variable before running: " +
        "export POSTGRES_CONNECTION_STRING='Host=...;Database=...;Username=...;Password=...'");

await using var conn = new NpgsqlConnection(connStr);
await conn.OpenAsync();

var before = await CountAsync(conn);
Console.WriteLine($"[seed] insurance_records before seeding: {before}");

await InsuranceRecordSeeder.SeedAsync(conn);

var after = await CountAsync(conn);
Console.WriteLine($"[seed] insurance_records after seeding:  {after}");

if (after < 10)
{
    Console.Error.WriteLine($"[seed] FAIL: expected ≥10 rows, found {after}");
    Environment.Exit(1);
}

Console.WriteLine("[seed] Seed complete.");

static async Task<long> CountAsync(NpgsqlConnection c)
{
    await using var cmd = new NpgsqlCommand("SELECT COUNT(*) FROM insurance_records", c);
    return (long)(await cmd.ExecuteScalarAsync())!;
}
