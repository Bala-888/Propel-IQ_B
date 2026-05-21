using System.Text;
using Api.Exceptions;
using Org.BouncyCastle.Bcpg;
using Org.BouncyCastle.Bcpg.OpenPgp;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities.IO;

namespace Api.Services;

/// <summary>
/// PHI field encryption using BouncyCastle PGP symmetric AES-256.
/// Output is compatible with PostgreSQL pgcrypto pgp_sym_encrypt bytea format (DR-001).
/// The encryption key is sourced exclusively from PHI_ENCRYPTION_KEY environment variable
/// and stored as ReadOnlyMemory&lt;byte&gt; — never written to logs, config files, or source (AC-004; OWASP A02).
/// </summary>
public sealed class PhiEncryptionService : IPhiEncryptionService
{
    // Key stored as bytes — never exposed as a string property or written to any output (OWASP A02)
    private readonly ReadOnlyMemory<byte> _keyBytes;

    public PhiEncryptionService()
    {
        var raw = Environment.GetEnvironmentVariable("PHI_ENCRYPTION_KEY");
        if (string.IsNullOrWhiteSpace(raw))
            throw new InvalidOperationException("PHI_ENCRYPTION_KEY environment variable is not set");

        // Defence-in-depth key-length guard — mirrors the Program.cs startup check (Edge: AES key length; OWASP A02).
        // GetByteCount, not raw.Length: a multi-byte UTF-8 string can satisfy a character-count check
        // while providing fewer than 32 bytes of actual key material, silently downgrading to AES-128
        // and violating HIPAA §164.312(a)(2)(iv) (AC-001; checklist: defence-in-depth pair).
        if (Encoding.UTF8.GetByteCount(raw) < 32)
            throw new ArgumentException(
                "PHI_ENCRYPTION_KEY must be at least 32 bytes for AES-256.",
                nameof(raw));

        // Store the key as UTF-8 bytes — prevents easy string inspection in memory dumps
        _keyBytes = Encoding.UTF8.GetBytes(raw);
    }

    /// <inheritdoc />
    public byte[]? Encrypt(string? plaintext)
    {
        if (plaintext is null) return null;

        var data = Encoding.UTF8.GetBytes(plaintext);
        var passphrase = GetPassphrase();
        try
        {
            var encGen = new PgpEncryptedDataGenerator(
                SymmetricKeyAlgorithmTag.Aes256,
                withIntegrityPacket: true,
                new SecureRandom());
            encGen.AddMethod(passphrase, HashAlgorithmTag.Sha256);

            using var output = new MemoryStream();

            // Open encrypted stream with partial-body buffer; encOut must be disposed
            // BEFORE reading output so the final MDC packet is flushed (integrity check)
            using (var encOut = encGen.Open(output, new byte[1 << 16]))
            {
                var litGen = new PgpLiteralDataGenerator();
                using var litOut = litGen.Open(
                    encOut,
                    PgpLiteralData.Binary,
                    PgpLiteralData.Console,
                    data.Length,
                    DateTime.UtcNow);
                litOut.Write(data, 0, data.Length);
            }

            return output.ToArray();
        }
        finally
        {
            // Zero the passphrase char array after use — reduce key material in managed heap
            Array.Clear(passphrase, 0, passphrase.Length);
        }
    }

    /// <inheritdoc />
    public string? Decrypt(byte[]? ciphertext)
    {
        if (ciphertext is null) return null;

        var passphrase = GetPassphrase();
        try
        {
            using var input = new MemoryStream(ciphertext);
            var pgpFactory = new PgpObjectFactory(input);

            // First PGP object may be a marker packet — skip to EncryptedDataList
            var obj = pgpFactory.NextPgpObject();
            var enc = obj is PgpEncryptedDataList list
                ? list
                : (PgpEncryptedDataList)pgpFactory.NextPgpObject();

            var pbe = (PgpPbeEncryptedData)enc[0];

            // GetDataStream decrypts the session key using the passphrase + S2K
            using var clearStream = pbe.GetDataStream(passphrase);
            var plainFactory = new PgpObjectFactory(clearStream);
            var plainObj = plainFactory.NextPgpObject();

            // Handle optional compression layer produced by some PGP implementations
            PgpLiteralData ld;
            if (plainObj is PgpCompressedData cd)
            {
                using var cdStream = cd.GetDataStream();
                ld = (PgpLiteralData)new PgpObjectFactory(cdStream).NextPgpObject();
            }
            else
            {
                ld = (PgpLiteralData)plainObj;
            }

            using var ldStream = ld.GetInputStream();
            return Encoding.UTF8.GetString(Streams.ReadAll(ldStream));
        }
        catch (Exception ex) when (ex is not PhiDecryptionException)
        {
            // Wrap all cipher/format exceptions — raw BouncyCastle messages must not reach API callers (OWASP A09)
            throw new PhiDecryptionException("PHI decryption failed — key rotation required", ex);
        }
        finally
        {
            Array.Clear(passphrase, 0, passphrase.Length);
        }
    }

    // Reconstructs the passphrase char[] from stored bytes on each invocation.
    // Using a method (not a property) avoids accidental property reflection exposure.
    private char[] GetPassphrase() => Encoding.UTF8.GetString(_keyBytes.Span).ToCharArray();
}
