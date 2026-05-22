using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Api.Data;
using Api.Features.Documents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BackgroundServices;

/// <summary>
/// Singleton <see cref="BackgroundService"/> that consumes <see cref="PatientEntitiesUpdatedEvent"/>
/// events, calls Ollama <c>POST /api/generate</c> with the patient's extracted entities, validates
/// the response, and upserts detected clinical conflicts to <c>clinical_conflicts</c>
/// (us_040/AC-004).
/// </summary>
/// <remarks>
/// Design invariants:
/// <list type="bullet">
///   <item>All <see cref="AppDbContext"/> instances are resolved via <see cref="IServiceScopeFactory"/>
///     per event — the scoped DbContext is never captured in the singleton constructor (OWASP A04).</item>
///   <item>Entity values, conflict descriptions, and conflict types are never written to any
///     <see cref="ILogger"/> call; only the patient ID integer appears in log entries (OWASP A02 — PHI guard).</item>
///   <item>Inserts use EF Core <c>ExecuteSqlAsync</c> with interpolated <see cref="FormattableString"/>
///     parameters — conflict data is never string-concatenated into SQL (OWASP A03).</item>
///   <item>Early-exit guard skips Ollama when fewer than 2 entities exist for the patient —
///     conflict detection requires at least two entities (AC-004).</item>
/// </list>
/// </remarks>
public sealed class ConflictDetectionWorker : BackgroundService
{
    private readonly Channel<PatientEntitiesUpdatedEvent> _channel;
    private readonly IServiceScopeFactory                 _scopeFactory;
    private readonly IHttpClientFactory                   _httpClientFactory;
    private readonly ILogger<ConflictDetectionWorker>     _logger;

    // Valid conflict types as per task spec (AC-004)
    private static readonly HashSet<string> ValidConflictTypes =
        new(StringComparer.Ordinal) { "DrugInteraction", "DrugAllergyConflict", "DuplicateDiagnosis" };

    // Valid severity levels as per task spec (AC-004)
    private static readonly HashSet<string> ValidSeverities =
        new(StringComparer.Ordinal) { "Low", "Medium", "High" };

    public ConflictDetectionWorker(
        Channel<PatientEntitiesUpdatedEvent> channel,
        IServiceScopeFactory                 scopeFactory,
        IHttpClientFactory                   httpClientFactory,
        ILogger<ConflictDetectionWorker>     logger)
    {
        _channel           = channel;
        _scopeFactory      = scopeFactory;
        _httpClientFactory = httpClientFactory;
        _logger            = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var ev in _channel.Reader.ReadAllAsync(ct))
        {
            try
            {
                await ProcessEventAsync(ev, ct);
            }
            catch (Exception ex)
            {
                // Outer safety net — prevents unhandled exception from crashing the consumer loop
                _logger.LogWarning(ex,
                    "Unhandled exception in conflict detection pipeline for patient {PatientId}",
                    ev.PatientId);
            }
        }
    }

    private async Task ProcessEventAsync(PatientEntitiesUpdatedEvent ev, CancellationToken ct)
    {
        await using var scope  = _scopeFactory.CreateAsyncScope();
        var             db     = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        // RT1: load all entities for this patient (AC-004)
        var entities = await db.PatientEntities
            .AsNoTracking()
            .Where(e => e.PatientId == ev.PatientId)
            .Select(e => new { e.Id, e.Type, e.Value })
            .ToListAsync(ct);

        // Early-exit: conflict detection requires ≥ 2 entities (AC-004; Edge: < 2 entities)
        if (entities.Count < 2)
        {
            _logger.LogDebug(
                "ConflictDetection skipped for patient {PatientId}: fewer than 2 entities",
                ev.PatientId);
            return;
        }

        // Build entity list string for prompt — entity values never reach ILogger (OWASP A02)
        var entityList = string.Join("\n",
            entities.Select(e => $"- id={e.Id} type={e.Type} value={e.Value}"));

        var prompt =
            "Review the following clinical entities for patient record. " +
            "Identify clinical conflicts (drug interactions, drug-allergy conflicts, duplicate diagnoses). " +
            "Return ONLY valid JSON: " +
            "{\"conflicts\": [{\"entityAId\": \"<uuid>\", \"entityBId\": \"<uuid>\", " +
            "\"conflictType\": \"DrugInteraction|DrugAllergyConflict|DuplicateDiagnosis\", " +
            "\"description\": \"<string>\", \"severity\": \"Low|Medium|High\"}]}. " +
            "Return {\"conflicts\": []} if none found.\n\n" +
            "Entities:\n" + entityList; // entityList used only here; never reaches a logger

        var client = _httpClientFactory.CreateClient("ollama-conflicts");

        // 30-second CancellationTokenSource — linked to host shutdown ct (AC-004; Edge: Ollama timeout)
        using var cts      = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var       linkedCt = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct).Token;

        HttpResponseMessage response;
        try
        {
            response = await client.PostAsJsonAsync(
                "/api/generate",
                new { model = "llama3.1:8b", prompt, stream = false },
                linkedCt);
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _logger.LogWarning(
                "ConflictDetection Ollama timeout for patient {PatientId}",
                ev.PatientId);
            return;
        }

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning(
                "ConflictDetection Ollama returned {StatusCode} for patient {PatientId}",
                (int)response.StatusCode,
                ev.PatientId);
            return;
        }

        // Level 1: parse Ollama wrapper and extract "response" field
        string conflictJson;
        try
        {
            using var ollamaDoc = await response.Content.ReadFromJsonAsync<JsonDocument>(linkedCt);
            if (ollamaDoc is null ||
                !ollamaDoc.RootElement.TryGetProperty("response", out var responseProp))
            {
                _logger.LogWarning(
                    "ConflictDetection schema error L1 for patient {PatientId}: missing 'response' field",
                    ev.PatientId);
                return;
            }
            conflictJson = responseProp.GetString() ?? string.Empty;
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "ConflictDetection schema error L1 for patient {PatientId}: Ollama wrapper not valid JSON",
                ev.PatientId);
            return;
        }

        // Level 2: parse conflict JSON and validate each entry
        JsonDocument conflictDoc;
        try
        {
            conflictDoc = JsonDocument.Parse(conflictJson);
        }
        catch (JsonException)
        {
            _logger.LogWarning(
                "ConflictDetection schema error L2 for patient {PatientId}: inner JSON not parseable",
                ev.PatientId);
            return;
        }

        using (conflictDoc)
        {
            if (!conflictDoc.RootElement.TryGetProperty("conflicts", out var conflictsArray) ||
                conflictsArray.ValueKind != JsonValueKind.Array)
            {
                _logger.LogWarning(
                    "ConflictDetection schema error L2 for patient {PatientId}: 'conflicts' array missing",
                    ev.PatientId);
                return;
            }

            // Build lookup set of known entity IDs for cross-reference validation (OWASP A03)
            var entityIds = new HashSet<Guid>(entities.Select(e => e.Id));

            var insertedCount = 0;
            foreach (var item in conflictsArray.EnumerateArray())
            {
                // Validate required fields — skip invalid entries rather than failing the whole batch
                if (!item.TryGetProperty("entityAId", out var entityAIdProp) ||
                    !Guid.TryParse(entityAIdProp.GetString(), out var entityAId) ||
                    !entityIds.Contains(entityAId))
                    continue;

                if (!item.TryGetProperty("entityBId", out var entityBIdProp) ||
                    !Guid.TryParse(entityBIdProp.GetString(), out var entityBId) ||
                    !entityIds.Contains(entityBId))
                    continue;

                if (entityAId == entityBId)
                    continue;

                if (!item.TryGetProperty("conflictType", out var conflictTypeProp) ||
                    !ValidConflictTypes.Contains(conflictTypeProp.GetString() ?? string.Empty))
                    continue;

                if (!item.TryGetProperty("severity", out var severityProp) ||
                    !ValidSeverities.Contains(severityProp.GetString() ?? string.Empty))
                    continue;

                if (!item.TryGetProperty("description", out var descriptionProp))
                    continue;

                var conflictType = conflictTypeProp.GetString()!;
                var severity     = severityProp.GetString()!;
                var description  = (descriptionProp.GetString() ?? string.Empty)[..Math.Min(
                    descriptionProp.GetString()?.Length ?? 0, 1000)]; // OWASP A04 — truncation guard

                var newId    = Guid.NewGuid();
                var now      = DateTimeOffset.UtcNow;

                // Idempotent insert: ON CONFLICT DO NOTHING ensures re-runs don't duplicate rows (AC-004).
                // FormattableString $"..." passes each value as a parameterised argument via EF Core
                // ExecuteSqlAsync — no string concatenation into SQL (OWASP A03).
                await db.Database.ExecuteSqlAsync(
                    $"""
                    INSERT INTO clinical_conflicts
                        (id, patient_id, entity_a_id, entity_b_id, conflict_type, description, severity, status, created_at)
                    VALUES
                        ({newId}, {ev.PatientId}, {entityAId}, {entityBId}, {conflictType}, {description}, {severity}, 'Open', {now})
                    ON CONFLICT (patient_id, entity_a_id, entity_b_id) DO NOTHING
                    """,
                    ct);

                insertedCount++;
            }

            _logger.LogInformation(
                "ConflictDetection complete for patient {PatientId}: {Inserted} conflicts persisted",
                ev.PatientId,
                insertedCount);
        }
    }
}
