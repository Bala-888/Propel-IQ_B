using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using System.Security.Cryptography;
using System.Text;

namespace Upacip.Api.Infrastructure.Encryption;

/// <summary>
/// EF Core value converter that transparently AES-256-CBC encrypts and decrypts
/// string column values flagged as PHI (Protected Health Information).
///
/// The encryption key is sourced from IConfiguration["Phi:EncryptionKey"]
/// (Base64-encoded 32-byte key). The IV is randomly generated per-value and
/// prepended to the ciphertext (first 16 bytes = IV, rest = ciphertext), then
/// the whole thing is Base64-encoded for storage.
///
/// HIPAA requirement: PHI must be encrypted at rest (AES-256 satisfies §164.312).
/// </summary>
public sealed class AesPhiConverter : ValueConverter<string?, string?>
{
    public AesPhiConverter(byte[] key)
        : base(
            plaintext  => Encrypt(plaintext, key),
            ciphertext => Decrypt(ciphertext, key))
    {
        if (key.Length != 32)
            throw new ArgumentOutOfRangeException(nameof(key), "AES-256 key must be exactly 32 bytes.");
    }

    private static string? Encrypt(string? plaintext, byte[] key)
    {
        if (plaintext is null) return null;

        using var aes = Aes.Create();
        aes.Key = key;
        aes.GenerateIV();

        using var encryptor = aes.CreateEncryptor();
        var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
        var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

        // Prepend IV so Decrypt can extract it without a separate column
        var result = new byte[aes.IV.Length + ciphertextBytes.Length];
        Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
        Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

        return Convert.ToBase64String(result);
    }

    private static string? Decrypt(string? ciphertext, byte[] key)
    {
        if (ciphertext is null) return null;

        var combined = Convert.FromBase64String(ciphertext);

        // First 16 bytes are the IV
        const int ivLength = 16;
        var iv = combined[..ivLength];
        var ciphertextBytes = combined[ivLength..];

        using var aes = Aes.Create();
        aes.Key = key;
        aes.IV = iv;

        using var decryptor = aes.CreateDecryptor();
        var plaintextBytes = decryptor.TransformFinalBlock(ciphertextBytes, 0, ciphertextBytes.Length);
        return Encoding.UTF8.GetString(plaintextBytes);
    }
}
