namespace Api.Services;

/// <summary>
/// Contract for PHI field encryption/decryption.
/// Implementations apply AES-256 PGP symmetric encryption compatible with
/// PostgreSQL pgcrypto pgp_sym_encrypt format (DR-001; us_006/AC-001, AC-004).
/// </summary>
public interface IPhiEncryptionService
{
    /// <summary>
    /// Encrypts <paramref name="plaintext"/> to PGP-symmetric bytea ciphertext.
    /// Returns <c>null</c> when <paramref name="plaintext"/> is <c>null</c> — pgp_sym_encrypt is never called with null (Edge: null PHI field).
    /// </summary>
    byte[]? Encrypt(string? plaintext);

    /// <summary>
    /// Decrypts <paramref name="ciphertext"/> and returns the original plaintext string.
    /// Returns <c>null</c> when <paramref name="ciphertext"/> is <c>null</c>.
    /// Throws <see cref="Exceptions.PhiDecryptionException"/> if the key does not match the ciphertext (Edge: key rotation).
    /// </summary>
    string? Decrypt(byte[]? ciphertext);
}
