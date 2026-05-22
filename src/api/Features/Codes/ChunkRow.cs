namespace Api.Features.Codes;

/// <summary>
/// Internal raw-SQL projection record for the pgvector similarity search result (us_043/AC-001, AC-004).
/// Properties must exactly match the column aliases in <c>SqlQuery&lt;ChunkRow&gt;</c> call
/// (EF Core maps column names to property names case-insensitively; no snake_case convention
/// is applied to raw SQL results).
/// </summary>
public sealed record ChunkRow(
    Guid   ChunkId,
    string ChunkText,
    string OriginalFilename);
