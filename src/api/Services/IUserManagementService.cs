using Api.DTOs;

namespace Api.Services;

/// <summary>
/// User CRUD operations for the <c>/admin/users</c> endpoints.
/// </summary>
public interface IUserManagementService
{
    /// <summary>
    /// Creates a new user with a bcrypt-hashed temporary password.
    /// </summary>
    /// <param name="request">Validated creation payload (Name, Email, Role).</param>
    /// <param name="actorId">ID of the Admin user performing the action (for audit logging).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The <c>Id</c> of the newly created user.</returns>
    /// <exception cref="Api.Exceptions.DuplicateEmailException">
    /// Thrown when <paramref name="request"/>.Email is already registered.
    /// </exception>
    Task<int> CreateAsync(CreateUserRequest request, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Applies a partial update (role change and/or activation toggle) to an existing user.
    /// </summary>
    /// <param name="id">Target user's integer primary key.</param>
    /// <param name="request">Patch payload (Role?, IsActive?).</param>
    /// <param name="actorId">ID of the Admin user performing the action (for audit logging).</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns><c>true</c> if the user was found and updated; <c>false</c> if not found.</returns>
    Task<bool> PatchAsync(int id, PatchUserRequest request, string actorId, CancellationToken ct = default);

    /// <summary>
    /// Returns all users ordered by creation date ascending.
    /// Projection excludes sensitive fields (password hash, PHI).
    /// </summary>
    Task<IReadOnlyList<Api.DTOs.UserResponse>> GetAllAsync(CancellationToken ct = default);
}
