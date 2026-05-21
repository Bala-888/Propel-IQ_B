namespace Api.Features.Documents;

/// <summary>
/// The result of a successful AES-256-GCM document encryption operation.
/// </summary>
/// <param name="CipherBytes">
/// Encrypted file bytes in the format <c>[nonce(12) || ciphertext || tag(16)]</c>.
/// Written atomically to blob storage — plaintext is never persisted (AC-003; OWASP A02).
/// </param>
/// <param name="WrappedKey">
/// Per-document AES-256 key wrapped (AES-256-GCM encrypted) with the platform master key.
/// Format: <c>[nonce(12) || encryptedKey(32) || tag(16)]</c> = 60 bytes, stored as <c>bytea</c> (AC-003).
/// </param>
public sealed record EncryptedDocument(byte[] CipherBytes, byte[] WrappedKey);

/// <summary>
/// Encrypts document bytes using AES-256-GCM with a fresh per-document random key and nonce.
/// The per-document key is itself wrapped (AES-256-GCM encrypted) using the platform master key
/// sourced from <c>DOCUMENT_MASTER_KEY</c> environment variable (AC-003; OWASP A02).
/// </summary>
public interface IDocumentEncryptionService
{
    /// <summary>
    /// Encrypts the full content of <paramref name="plaintext"/> with AES-256-GCM.
    /// A fresh 32-byte key and 12-byte nonce are generated via <c>RandomNumberGenerator</c>
    /// for every invocation — keys are never reused across documents (AC-003; OWASP A02).
    /// </summary>
    /// <param name="plaintext">Readable file stream positioned at byte 0. Must be seeked to start before call.</param>
    /// <returns>Cipher bytes and the wrapped per-document key.</returns>
    EncryptedDocument Encrypt(Stream plaintext);

    /// <summary>
    /// Decrypts the AES-256-GCM cipher blob at <paramref name="blobPath"/> using the
    /// per-document key unwrapped from <paramref name="wrappedKey"/>.
    ///
    /// <para>
    /// The returned <see cref="MemoryStream"/> is positioned at byte 0 and backed by an
    /// exposable internal buffer — callers <b>must</b> zero-fill the buffer via
    /// <c>Array.Clear(stream.GetBuffer(), 0, (int)stream.Length)</c> immediately after use
    /// to remove plaintext from the managed heap (AC-001; OWASP A02).
    /// Plaintext is never written to disk at any point in this method.
    /// </para>
    /// </summary>
    /// <param name="blobPath">Absolute path to the encrypted cipher blob on disk.</param>
    /// <param name="wrappedKey">
    /// 60-byte wrapped key in the format <c>[wrapNonce(12) || encryptedKey(32) || tag(16)]</c>
    /// produced by <see cref="Encrypt"/> (AC-001; OWASP A02).
    /// </param>
    /// <param name="ct">Cancellation token for the async file read.</param>
    /// <returns>Seeked-to-zero <see cref="MemoryStream"/> containing the plaintext bytes.</returns>
    Task<MemoryStream> DecryptAsync(string blobPath, byte[] wrappedKey, CancellationToken ct = default);
}
