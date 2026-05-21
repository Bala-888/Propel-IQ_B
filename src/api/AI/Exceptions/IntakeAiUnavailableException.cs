namespace Api.AI.Exceptions;

/// <summary>
/// Thrown by <see cref="OllamaIntakeClient"/> when the Ollama inference endpoint returns
/// HTTP 503 (model not loaded / service unavailable) or the Docker service is unreachable.
/// Caught exclusively in <see cref="Controllers.IntakeAiController"/> action methods — must
/// not reach the global exception handler which would return a generic 500 (checklist; OWASP A05).
/// Session state in <see cref="IntakeSessionService"/> is preserved so the patient can resume
/// when the model becomes available (Edge: Ollama model not loaded).
/// </summary>
public sealed class IntakeAiUnavailableException : Exception
{
    public IntakeAiUnavailableException()
        : base("AI intake is temporarily unavailable. You can use the manual form instead.")
    {
    }

    public IntakeAiUnavailableException(string message)
        : base(message)
    {
    }

    public IntakeAiUnavailableException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
