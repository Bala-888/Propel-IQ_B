using System.Security.Cryptography;
using Org.BouncyCastle.Crypto.Engines;
using Org.BouncyCastle.Crypto.Modes;
using Org.BouncyCastle.Crypto.Parameters;

namespace Api.Features.Documents;

/// <summary>
/// AES-256-GCM document encryption using BouncyCastle <c>GcmBlockCipher</c>.
///
/// <para>
/// Per-document encryption: a fresh 32-byte key and 12-byte nonce are generated via
/// <see cref="RandomNumberGenerator"/> for every call to <see cref="Encrypt"/> — keys are never
/// reused across documents (AC-003; OWASP A02).
/// </para>
/// <para>
/// Key wrapping: the per-document key is itself AES-256-GCM encrypted using the platform master
/// key sourced exclusively from the <c>DOCUMENT_MASTER_KEY</c> environment variable.
/// The master key is never written to logs, config files, or source (AC-003; OWASP A02).
/// </para>
/// <para>
/// Cipher bytes format: <c>[nonce(12) || ciphertext || tag(16)]</c>. Wrapped key format:
/// <c>[wrapNonce(12) || encryptedKey(32) || tag(16)]</c> = 60 bytes stored as bytea (AC-003).
/// </para>
/// </summary>
public sealed class DocumentEncryptionService : IDocumentEncryptionService
{
    private const int KeyBytes  = 32; // AES-256
    private const int NonceBytes = 12; // GCM 96-bit nonce
    private const int TagBits   = 128; // GCM authentication tag length

    // Master key stored as bytes — never exposed as a string or logged (OWASP A02)
    private readonly ReadOnlyMemory<byte> _masterKey;

    public DocumentEncryptionService(IConfiguration configuration)
    {
        var raw = configuration["DOCUMENT_MASTER_KEY"];
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException(
                "DOCUMENT_MASTER_KEY environment variable is not configured");

        var keyBytes = System.Text.Encoding.UTF8.GetBytes(raw);

        // Defence-in-depth key-length guard — a shorter key would silently downgrade protection.
        // GetByteCount (not .Length) because multi-byte UTF-8 characters can satisfy a character-count
        // check while providing fewer actual bytes of key material (OWASP A02; AC-003).
        if (keyBytes.Length < KeyBytes)
            throw new InvalidOperationException(
                "DOCUMENT_MASTER_KEY must be at least 32 bytes for AES-256");

        _masterKey = keyBytes[..KeyBytes];
    }

    /// <inheritdoc />
    public EncryptedDocument Encrypt(Stream plaintext)
    {
        // Fresh per-document key and nonce — never reused (AC-003; OWASP A02; checklist)
        var documentKey = RandomNumberGenerator.GetBytes(KeyBytes);
        var nonce       = RandomNumberGenerator.GetBytes(NonceBytes);

        try
        {
            var cipherBytes = EncryptStream(plaintext, documentKey, nonce);
            var wrappedKey  = WrapKey(documentKey);
            return new EncryptedDocument(cipherBytes, wrappedKey);
        }
        finally
        {
            // Zero per-document key — reduces window where key material is in managed heap
            CryptographicOperations.ZeroMemory(documentKey);
        }
    }

    /// <inheritdoc />
    public async Task<MemoryStream> DecryptAsync(
        string blobPath,
        byte[] wrappedKey,
        CancellationToken ct = default)
    {
        // Read the full cipher blob — format: [nonce(12) || ciphertext+tag]
        var cipherBytes = await File.ReadAllBytesAsync(blobPath, ct);

        // Unwrap per-document key from the 60-byte wrapped key (AC-001; OWASP A02)
        var documentKey = UnwrapKey(wrappedKey);
        byte[] plaintextBytes = Array.Empty<byte>();

        try
        {
            if (cipherBytes.Length < NonceBytes)
                throw new InvalidOperationException(
                    "Cipher blob is shorter than the minimum nonce size; the file may be corrupt.");

            var nonce        = cipherBytes[..NonceBytes];
            var cipherWithTag = cipherBytes[NonceBytes..];

            plaintextBytes = DecryptBytes(cipherWithTag, documentKey, nonce);

            // Use default-constructor MemoryStream so GetBuffer() returns the exposable internal
            // buffer — callers must zero-fill it via Array.Clear after use (AC-001; OWASP A02).
            var ms = new MemoryStream();
            await ms.WriteAsync(plaintextBytes, ct);
            ms.Position = 0;
            return ms;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(documentKey);
            // Zero the intermediate plaintext copy produced by DecryptBytes (OWASP A02)
            if (plaintextBytes.Length > 0)
                CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// Reads <paramref name="plaintext"/> in full and encrypts using AES-256-GCM.
    /// Output format: [nonce(12) || ciphertext || tag(16)].
    /// </summary>
    private static byte[] EncryptStream(Stream plaintext, byte[] key, byte[] nonce)
    {
        // Buffer the stream — GCM requires the full plaintext to produce the authentication tag
        byte[] plaintextBytes;
        using (var ms = new MemoryStream())
        {
            plaintext.CopyTo(ms);
            plaintextBytes = ms.ToArray();
        }

        try
        {
            var encrypted = EncryptBytes(plaintextBytes, key, nonce);

            // Prepend nonce: [nonce(12) || ciphertext+tag]
            var result = new byte[NonceBytes + encrypted.Length];
            nonce.CopyTo(result, 0);
            encrypted.CopyTo(result, NonceBytes);
            return result;
        }
        finally
        {
            // Zero plaintext bytes after encryption — best-effort reduction of PHI in heap (OWASP A02)
            CryptographicOperations.ZeroMemory(plaintextBytes);
        }
    }

    /// <summary>
    /// AES-256-GCM encrypt <paramref name="plaintext"/> with <paramref name="key"/> and
    /// <paramref name="nonce"/>. Returns <c>ciphertext+tag</c> (no nonce prefix).
    /// </summary>
    private static byte[] EncryptBytes(byte[] plaintext, byte[] key, byte[] nonce)
    {
        var cipher     = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), TagBits, nonce);
        cipher.Init(forEncryption: true, parameters);

        var outputLen    = cipher.GetOutputSize(plaintext.Length);
        var output       = new byte[outputLen];
        var written      = cipher.ProcessBytes(plaintext, 0, plaintext.Length, output, 0);
        cipher.DoFinal(output, written);
        return output;
    }

    /// <summary>
    /// AES-256-GCM decrypt <paramref name="cipherWithTag"/> with <paramref name="key"/> and
    /// <paramref name="nonce"/>. Returns the plaintext bytes.
    /// </summary>
    private static byte[] DecryptBytes(byte[] cipherWithTag, byte[] key, byte[] nonce)
    {
        var cipher     = new GcmBlockCipher(new AesEngine());
        var parameters = new AeadParameters(new KeyParameter(key), TagBits, nonce);
        cipher.Init(forEncryption: false, parameters);

        var outputLen = cipher.GetOutputSize(cipherWithTag.Length);
        var output    = new byte[outputLen];
        var written   = cipher.ProcessBytes(cipherWithTag, 0, cipherWithTag.Length, output, 0);
        cipher.DoFinal(output, written);
        return output;
    }

    /// <summary>
    /// Wraps (AES-256-GCM encrypts) the per-document key with the platform master key.
    /// Output format: [wrapNonce(12) || encryptedKey(32) || tag(16)] = 60 bytes.
    /// </summary>
    private byte[] WrapKey(byte[] documentKey)
    {
        var wrapNonce      = RandomNumberGenerator.GetBytes(NonceBytes);
        var masterKeyArray = _masterKey.ToArray();
        try
        {
            var encryptedKey = EncryptBytes(documentKey, masterKeyArray, wrapNonce);

            // [wrapNonce(12) || encryptedKey+tag]
            var result = new byte[NonceBytes + encryptedKey.Length];
            wrapNonce.CopyTo(result, 0);
            encryptedKey.CopyTo(result, NonceBytes);
            return result;
        }
        finally
        {
            // Zero local copy of master key material after use (OWASP A02)
            CryptographicOperations.ZeroMemory(masterKeyArray);
        }
    }

    /// <summary>
    /// Unwraps (AES-256-GCM decrypts) the 60-byte wrapped key to recover the per-document key.
    /// Input format: [wrapNonce(12) || encryptedKey(32) || tag(16)].
    /// </summary>
    private byte[] UnwrapKey(byte[] wrappedKey)
    {
        var wrapNonce        = wrappedKey[..NonceBytes];
        var encryptedWithTag = wrappedKey[NonceBytes..];

        var masterKeyArray = _masterKey.ToArray();
        try
        {
            return DecryptBytes(encryptedWithTag, masterKeyArray, wrapNonce);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(masterKeyArray);
        }
    }
}
