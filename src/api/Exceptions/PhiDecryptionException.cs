namespace Api.Exceptions;

/// <summary>
/// Raised when PHI ciphertext cannot be decrypted — indicates key mismatch or key rotation.
/// Mapped to HTTP 503 by the exception middleware (Edge: key rotation; OWASP A09).
/// Raw cipher details are intentionally not included in the message (OWASP A02).
/// </summary>
public sealed class PhiDecryptionException : Exception
{
    public PhiDecryptionException(string message) : base(message) { }

    public PhiDecryptionException(string message, Exception innerException)
        : base(message, innerException) { }
}
