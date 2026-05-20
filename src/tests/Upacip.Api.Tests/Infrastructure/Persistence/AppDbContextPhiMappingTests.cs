using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Upacip.Api.Infrastructure.Persistence;

namespace Upacip.Api.Tests.Infrastructure.Persistence;

/// <summary>
/// Tests that verify every PHI column has <see cref="AesPhiConverter"/> wired
/// in <see cref="AppDbContext.OnModelCreating"/>.
///
/// These are design-time / compile-time safeguards — no DB connection needed.
/// Covers US-040 (PHI encrypted at rest, FR-045) and security findings:
///   • BUG-CRITICAL: GoogleCalendarTokenEncrypted / OutlookCalendarTokenEncrypted
///   • BUG-CRITICAL: WalkInPatientName / WalkInPatientPhone
/// </summary>
public sealed class AppDbContextPhiMappingTests
{
    // ─── Setup ────────────────────────────────────────────────────────────────

    private static AppDbContext CreateDesignTimeContext()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                // "REPLACE…" triggers the design-time zero-key fallback in AppDbContext
                ["Phi:EncryptionKey"] = "REPLACE_WITH_REAL_KEY_FOR_TESTS"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phi-mapping-{Guid.NewGuid()}")
            .Options;

        return new AppDbContext(options, config);
    }

    private static IReadOnlyList<IProperty> GetPropertiesWithConverter(AppDbContext ctx, string entityName)
    {
        var entityType = ctx.Model.FindEntityType(
            $"Upacip.Api.Domain.Entities.{entityName}");

        entityType.Should().NotBeNull($"entity {entityName} must exist in the model");

        return entityType!.GetProperties()
            .Where(p => p.GetValueConverter() is not null)
            .ToList();
    }

    private static void AssertHasPhiConverter(AppDbContext ctx, string entityName, string propertyName)
    {
        var entityType = ctx.Model.FindEntityType(
            $"Upacip.Api.Domain.Entities.{entityName}");

        entityType.Should().NotBeNull($"entity {entityName} must exist");

        var property = entityType!.FindProperty(propertyName);
        property.Should().NotBeNull($"{entityName}.{propertyName} must exist as a mapped column");

        property!.GetValueConverter().Should().NotBeNull(
            $"{entityName}.{propertyName} is PHI and MUST have a value converter (AesPhiConverter)");
    }

    // ─── TC-DB-001 → TC-DB-002: AppUser PHI columns ───────────────────────────

    [Theory(DisplayName = "TC-DB-001/002: AppUser PHI columns have AES converter")]
    [InlineData("Phone")]
    [InlineData("DateOfBirth")]
    public void AppUser_PhiColumns_HaveConverter(string propertyName)
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "AppUser", propertyName);
    }

    // ─── TC-DB-003 → TC-DB-006: Patient PHI columns ───────────────────────────

    [Theory(DisplayName = "TC-DB-003–006: Patient PHI columns have AES converter")]
    [InlineData("InsuranceProvider")]
    [InlineData("InsuranceId")]
    [InlineData("GoogleCalendarTokenEncrypted")]   // BUG-CRITICAL fix
    [InlineData("OutlookCalendarTokenEncrypted")]  // BUG-CRITICAL fix
    public void Patient_PhiColumns_HaveConverter(string propertyName)
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "Patient", propertyName);
    }

    // ─── TC-DB-007: IntakeRecord ───────────────────────────────────────────────

    [Fact(DisplayName = "TC-DB-007: IntakeRecord.DataEncrypted has AES converter")]
    public void IntakeRecord_DataEncrypted_HasConverter()
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "IntakeRecord", "DataEncrypted");
    }

    // ─── TC-DB-008 → TC-DB-009: Booking walk-in PHI columns ──────────────────

    [Theory(DisplayName = "TC-DB-008/009: Booking walk-in PHI columns have AES converter")]
    [InlineData("WalkInPatientName")]   // BUG-CRITICAL fix
    [InlineData("WalkInPatientPhone")]  // BUG-CRITICAL fix
    public void Booking_WalkInPhiColumns_HaveConverter(string propertyName)
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "Booking", propertyName);
    }

    // ─── TC-DB-010: ClinicalDocument storage path ─────────────────────────────

    [Fact(DisplayName = "TC-DB-010: ClinicalDocument.StoragePathEncrypted has AES converter")]
    public void ClinicalDocument_StoragePath_HasConverter()
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "ClinicalDocument", "StoragePathEncrypted");
    }

    // ─── TC-DB-011: ExtractedRecord value ─────────────────────────────────────

    [Fact(DisplayName = "TC-DB-011: ExtractedRecord.ValueEncrypted has AES converter")]
    public void ExtractedRecord_ValueEncrypted_HasConverter()
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "ExtractedRecord", "ValueEncrypted");
    }

    // ─── TC-DB-012: ChunkEmbedding chunk text ─────────────────────────────────

    [Fact(DisplayName = "TC-DB-012: ChunkEmbedding.ChunkTextEncrypted has AES converter")]
    public void ChunkEmbedding_ChunkText_HasConverter()
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "ChunkEmbedding", "ChunkTextEncrypted");
    }

    // ─── TC-DB-013 → TC-DB-014: ConflictFlag encrypted values ───────────────

    [Theory(DisplayName = "TC-DB-013/014: ConflictFlag PHI columns have AES converter")]
    [InlineData("ValueAEncrypted")]
    [InlineData("ValueBEncrypted")]
    public void ConflictFlag_PhiColumns_HaveConverter(string propertyName)
    {
        using var ctx = CreateDesignTimeContext();
        AssertHasPhiConverter(ctx, "ConflictFlag", propertyName);
    }

    // ─── TC-DB-015: Phi:EncryptionKey wrong length → fatal startup ───────────

    [Fact(DisplayName = "TC-DB-015: 24-byte Phi:EncryptionKey throws InvalidOperationException at startup")]
    public void InvalidPhiKeyLength_ThrowsAtStartup()
    {
        var short24bytes = Convert.ToBase64String(new byte[24]);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phi:EncryptionKey"] = short24bytes
            })
            .Build();

        // EnableServiceProviderCaching(false) forces EF Core to rebuild OnModelCreating
        // for each instance, bypassing the compiled-model static cache keyed by type.
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phi-bad-key-{Guid.NewGuid()}")
            .EnableServiceProviderCaching(false)
            .Options;

        var act = () =>
        {
            using var ctx = new AppDbContext(options, config);
            _ = ctx.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*32 bytes*");
    }

    // ─── TC-DB-016: Malformed Base64 key → fatal startup ─────────────────────

    [Fact(DisplayName = "TC-DB-016: Malformed Base64 Phi:EncryptionKey throws InvalidOperationException")]
    public void MalformedBase64PhiKey_ThrowsAtStartup()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Phi:EncryptionKey"] = "not+valid+base64!!!"
            })
            .Build();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase($"phi-bad-b64-{Guid.NewGuid()}")
            .EnableServiceProviderCaching(false)
            .Options;

        var act = () =>
        {
            using var ctx = new AppDbContext(options, config);
            _ = ctx.Model;
        };

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Base64*");
    }
}
