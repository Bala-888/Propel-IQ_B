namespace Api.Features.Documents;

/// <summary>
/// Splits a document text string into overlapping token windows using whitespace tokenisation.
///
/// <para>
/// <b>Window</b>: 500 tokens. <b>Overlap</b>: 50 tokens. Advance stride = 450 tokens (AC-002; AIR-003, AIR-004).
/// </para>
///
/// <para>
/// <b>Overlap invariant</b>: for every consecutive pair of chunks,
/// <c>chunks[i].EndToken - chunks[i+1].StartToken == overlap</c>. This holds because
/// <c>EndToken</c> is exclusive (past-the-end): <c>EndToken[i] = start + windowSize</c>,
/// <c>StartToken[i+1] = start + windowSize - overlap</c>, so the difference is always
/// <c>overlap</c> regardless of document length (AC-002; AIR-004; checklist).
/// </para>
///
/// <para>
/// <b>Memory</b>: <c>string.Split</c> produces one array allocation per call; chunks are
/// built with <see cref="string.Join"/> on the slice — no additional <c>StringBuilder</c>
/// allocations per chunk. The last chunk may contain fewer than <c>windowSize</c> tokens
/// (when the remaining words are exhausted before <c>start + windowSize</c>).
/// </para>
/// </summary>
internal static class SlidingWindowChunker
{
    /// <summary>
    /// A single window-sized chunk of text with its token position metadata.
    ///
    /// <para>
    /// <b><see cref="EndToken"/> is exclusive</b> (past-the-end): it equals
    /// <c>StartToken + windowSize</c>, NOT the actual last token index.
    /// This enables the overlap invariant: <c>EndToken[i] - StartToken[i+1] == overlap</c> (AC-002).
    /// </para>
    /// </summary>
    /// <param name="Content">Space-joined words for this window.</param>
    /// <param name="TokenCount">Number of actual tokens in <see cref="Content"/> (≤ windowSize).</param>
    /// <param name="StartToken">Inclusive 0-based start token index in the word array.</param>
    /// <param name="EndToken">Exclusive past-the-end token index (<c>StartToken + windowSize</c>; AC-002).</param>
    internal sealed record TextChunk(string Content, int TokenCount, int StartToken, int EndToken);

    /// <summary>
    /// Splits <paramref name="text"/> into overlapping fixed-size windows.
    /// </summary>
    /// <param name="text">Full extracted document text (whitespace-separated words).</param>
    /// <param name="windowSize">Maximum tokens per chunk (default: 500; AIR-003).</param>
    /// <param name="overlap">Token overlap between consecutive chunks (default: 50; AIR-004).</param>
    /// <returns>
    /// Read-only list of <see cref="TextChunk"/> records in document order.
    /// Returns an empty list when <paramref name="text"/> contains no non-whitespace tokens.
    /// </returns>
    internal static IReadOnlyList<TextChunk> Chunk(
        string text,
        int    windowSize = 500,
        int    overlap    = 50)
    {
        var words  = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var chunks = new List<TextChunk>();

        int start = 0;
        while (start < words.Length)
        {
            // end is clamped to words.Length so the last slice is never out-of-range.
            // EndToken uses start + windowSize (unclamped) to preserve the overlap invariant:
            //   EndToken[i] - StartToken[i+1]
            //   = (start + windowSize) - (start + windowSize - overlap)
            //   = overlap  ✓  (AC-002; AIR-004; checklist)
            int end   = Math.Min(start + windowSize, words.Length);
            var slice = words[start..end];

            chunks.Add(new TextChunk(
                Content:    string.Join(" ", slice),
                TokenCount: slice.Length,
                StartToken: start,
                EndToken:   start + windowSize)); // exclusive past-the-end (AC-002; checklist)

            start += windowSize - overlap; // advance by 450 tokens (500 - 50)
        }

        return chunks;
    }
}
