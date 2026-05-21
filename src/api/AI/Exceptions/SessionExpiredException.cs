namespace Api.AI.Exceptions;

/// <summary>
/// Thrown by <see cref="Controllers.IntakeAiController.ConfirmAsync"/> when
/// <see cref="IntakeSessionService.GetSessionAsync"/> returns <c>null</c> for the given session ID,
/// indicating the TTL has elapsed (AC-003; Edge: session expired before confirmation).
/// Caught exclusively in the <c>POST /intake/ai/confirm</c> action method — mapped to HTTP 410;
/// must not reach the global exception handler (OWASP A05; checklist).
/// The auto-saved Draft <see cref="Data.Entities.IntakeRecord"/> written by us_016-I remains in
/// the database so the patient's partial data is not lost (Edge: session expired).
/// </summary>
public sealed class SessionExpiredException : Exception
{
    public SessionExpiredException()
        : base("Session expired. Your draft has been saved.")
    {
    }

    public SessionExpiredException(string message)
        : base(message)
    {
    }
}
