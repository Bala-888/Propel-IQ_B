using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Api.AI.Exceptions;

namespace Api.AI;

/// <summary>
/// Typed HTTP client for the Ollama /api/chat endpoint (AIR-001, AIR-002).
/// Base URL is read from the <c>OLLAMA_BASE_URL</c> environment variable, defaulting to
/// <c>http://ollama:11434</c> (the internal Docker network name) — never hardcoded.
/// All inference traffic stays within the Docker bridge network; no requests reach external IPs
/// (AC-003; OWASP A02; checklist).
/// </summary>
public sealed class OllamaIntakeClient
{
    private const string ModelName = "llama3.1:8b";

    // Extraction block pattern: [EXTRACTION:{...}] at end of model reply (AIR-002; AC-004)
    private static readonly Regex ExtractionRegex =
        new(@"\[EXTRACTION:(\{.*?\})\]", RegexOptions.Singleline | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;
    private readonly ILogger<OllamaIntakeClient> _logger;
    private readonly string _systemPrompt;

    public OllamaIntakeClient(HttpClient httpClient, ILogger<OllamaIntakeClient> logger)
    {
        _httpClient  = httpClient;
        _logger      = logger;
        _systemPrompt = LoadSystemPrompt();
    }

    /// <summary>
    /// Sends the accumulated conversation <paramref name="history"/> to the Ollama /api/chat endpoint
    /// and returns the model's reply text with the embedded extraction block stripped.
    /// </summary>
    /// <param name="history">
    /// Prior assistant + user turns. The system prompt is prepended automatically and must not be
    /// included in <paramref name="history"/> — this prevents double-injection if the client is
    /// called across multiple turns.
    /// </param>
    /// <param name="ct">
    /// Cancellation token supplied by the controller; fires after 30 seconds to enforce the
    /// Ollama inference timeout (Edge: inference timeout; AC-001 latency SLA).
    /// </param>
    /// <returns>
    /// A tuple of (<c>replyText</c>, <c>extraction</c>) where <c>replyText</c> is the natural-
    /// language portion of the model's response (safe to return to the patient) and
    /// <c>extraction</c> is the parsed <see cref="IntakeFieldState"/> from the extraction block.
    /// <c>extraction</c> may be <c>null</c> if the model omitted the block on a given turn.
    /// </returns>
    /// <exception cref="IntakeAiUnavailableException">
    /// Thrown when the Ollama endpoint returns HTTP 503 or the service is unreachable (Edge).
    /// </exception>
    /// <exception cref="OperationCanceledException">
    /// Bubbles up from the <paramref name="ct"/> timeout path — caught in the controller (Edge).
    /// </exception>
    public async Task<(string ReplyText, IntakeFieldState? Extraction)> ChatAsync(
        List<OllamaChatMessage> history,
        CancellationToken ct)
    {
        // Prepend the system prompt; do not store it in the session's History to avoid
        // persisting the full prompt text in every cache entry (memory efficiency; no PHI in prompt).
        var messages = new List<OllamaChatMessage>(capacity: history.Count + 1)
        {
            new() { Role = "system", Content = _systemPrompt }
        };
        messages.AddRange(history);

        var requestBody = new OllamaApiRequest
        {
            Model    = ModelName,
            Messages = messages,
            Stream   = false,
        };

        var json    = JsonSerializer.Serialize(requestBody, OllamaJsonContext.Default.OllamaApiRequest);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.PostAsync("/api/chat", content, ct);
        }
        catch (HttpRequestException ex)
        {
            // Network-level failure (Docker service unreachable) — map to typed exception (Edge: 503)
            _logger.LogWarning("Ollama service unreachable: {ErrorMessage}", ex.Message);
            throw new IntakeAiUnavailableException(
                "AI intake is temporarily unavailable. You can use the manual form instead.", ex);
        }

        if (response.StatusCode == HttpStatusCode.ServiceUnavailable)
        {
            // Ollama model not yet loaded or overloaded — preserve session, return typed exception (Edge)
            _logger.LogWarning(
                "Ollama returned HTTP 503 — model not loaded or service unavailable");
            throw new IntakeAiUnavailableException();
        }

        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(ct);
        var ollamaResp   = JsonSerializer.Deserialize(responseJson, OllamaJsonContext.Default.OllamaApiResponse);

        var rawContent = ollamaResp?.Message?.Content ?? string.Empty;

        // Parse the [EXTRACTION:{...}] block that the model embeds per system prompt instructions (AIR-002)
        var (replyText, extraction) = ParseReply(rawContent);

        return (replyText, extraction);
    }

    // ── Private helpers ─────────────────────────────────────────────────────────────────────────

    private (string ReplyText, IntakeFieldState? Extraction) ParseReply(string rawContent)
    {
        var match = ExtractionRegex.Match(rawContent);
        if (!match.Success)
        {
            // Model omitted the extraction block on this turn — return full content as reply
            return (rawContent.Trim(), null);
        }

        // Strip the extraction block from the natural-language reply shown to the patient
        var replyText = rawContent[..match.Index].Trim();

        IntakeFieldState? extraction = null;
        try
        {
            extraction = JsonSerializer.Deserialize(
                match.Groups[1].Value,
                OllamaJsonContext.Default.IntakeFieldState);
        }
        catch (JsonException)
        {
            // Malformed extraction block — log structurally (no PHI) and proceed with null extraction
            _logger.LogWarning("IntakeAiClient: malformed extraction JSON in model reply — skipping field update");
        }

        return (replyText, extraction);
    }

    /// <summary>
    /// Loads the system prompt from the embedded .propel prompt file at startup.
    /// Falls back to a minimal built-in prompt when the file is absent (dev/test scenarios).
    /// The prompt contains no PHI and is safe to load once at startup (OWASP A02).
    /// </summary>
    private static string LoadSystemPrompt()
    {
        // Locate the prompt file relative to the assembly location (works in Docker container
        // where the file is copied alongside the published binary, and in local development).
        var assemblyDir  = AppContext.BaseDirectory;
        var candidates   = new[]
        {
            Path.Combine(assemblyDir, "prompts", "intake-dialogue-system-prompt.md"),
            // Dev layout: src/api/bin/Debug/net8.0/ → climb 4 levels to repo root
            Path.Combine(assemblyDir, "..", "..", "..", "..", ".propel", "context", "ai", "prompts", "intake-dialogue-system-prompt.md"),
        };

        foreach (var path in candidates)
        {
            var normalised = Path.GetFullPath(path);
            if (File.Exists(normalised))
                return File.ReadAllText(normalised);
        }

        // Minimal fallback — sufficient to exercise the collection loop in tests
        return
            "You are a clinical intake assistant. Collect demographics, medical history, " +
            "current medications, allergies, and chief complaint through friendly conversation. " +
            "After each patient message append: " +
            "[EXTRACTION:{\"demographics\":null,\"medicalHistory\":null,\"medications\":null,\"allergies\":null,\"chiefComplaint\":null}]";
    }
}

// ── Ollama wire types ────────────────────────────────────────────────────────────────────────────

internal sealed class OllamaApiRequest
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    [JsonPropertyName("messages")]
    public List<OllamaChatMessage> Messages { get; set; } = new();

    [JsonPropertyName("stream")]
    public bool Stream { get; set; }
}

internal sealed class OllamaApiResponse
{
    [JsonPropertyName("message")]
    public OllamaChatMessage? Message { get; set; }
}

// Source-generated JSON context — avoids reflection for serialisation/deserialisation (performance; AOT safety)
[JsonSerializable(typeof(OllamaApiRequest))]
[JsonSerializable(typeof(OllamaApiResponse))]
[JsonSerializable(typeof(IntakeFieldState))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OllamaJsonContext : JsonSerializerContext { }
