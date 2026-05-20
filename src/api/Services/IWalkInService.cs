using Api.DTOs;

namespace Api.Services;

/// <summary>
/// Handles walk-in booking creation with optional patient account creation.
/// </summary>
public interface IWalkInService
{
    /// <summary>
    /// Creates a walk-in booking record via one of three execution paths:
    /// <list type="number">
    ///   <item>Booking-only (<see cref="CreateWalkInRequest.CreateAccount"/> = false) — no patient link (AC-001).</item>
    ///   <item>Account-creation (<see cref="CreateWalkInRequest.CreateAccount"/> = true) — creates a
    ///         Patient User, links the booking, dispatches credentials email (AC-002, AC-003).</item>
    ///   <item>Link-existing (<see cref="CreateWalkInRequest.LinkExistingAccountId"/> is set) —
    ///         links the booking to an existing user without creating a new account (AC-004 Yes path).</item>
    /// </list>
    /// </summary>
    /// <param name="request">Validated walk-in request payload.</param>
    /// <param name="actorId">Staff user ID performing the action (for audit logging).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <exception cref="Api.Exceptions.DuplicateEmailException">
    /// Thrown when <see cref="CreateWalkInRequest.Email"/> already belongs to an existing user
    /// and <see cref="CreateWalkInRequest.LinkExistingAccountId"/> is not provided (AC-004).
    /// </exception>
    Task<CreateWalkInResponse> CreateAsync(
        CreateWalkInRequest request,
        string actorId,
        CancellationToken ct = default);
}
