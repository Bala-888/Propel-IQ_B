using System.Security.Claims;
using Api.Constants;
using Api.DTOs;
using Api.Exceptions;
using Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Api.Controllers;

/// <summary>
/// Manages user accounts from the admin surface.
/// All routes require the <c>Admin</c> JWT role claim — enforced at the class level so
/// no per-action decoration drift is possible (AC-001, AC-002, AC-003; OWASP A01).
/// </summary>
[ApiController]
[Route("admin/users")]
[Authorize(Roles = Roles.Admin)]
public sealed class AdminUsersController : ControllerBase
{
    private static readonly HashSet<string> AllowedRoles =
        new(StringComparer.Ordinal) { "Patient", "Staff", "Admin" };

    private readonly IUserManagementService _userManagement;

    public AdminUsersController(IUserManagementService userManagement)
    {
        _userManagement = userManagement;
    }

    /// <summary>
    /// Returns all users ordered by creation date — non-sensitive fields only.
    /// </summary>
    [HttpGet]
    public async Task<ActionResult<IEnumerable<UserResponse>>> GetAllAsync()
    {
        var users = await _userManagement.GetAllAsync();
        return Ok(users);
    }

    /// <summary>
    /// Creates a new user with a bcrypt-hashed temporary password and dispatches a welcome email.
    /// Returns HTTP 201 with <c>{"userId": int}</c> on success.
    /// Returns HTTP 409 when the email is already registered (AC-005; OWASP A07).
    /// </summary>
    [HttpPost]
    public async Task<ActionResult<object>> CreateAsync([FromBody] CreateUserRequest request)
    {
        // ModelState validation (Name/Email/Role required + AllowedValues) is handled by
        // [ApiController] + InvalidModelStateResponseFactory before reaching this method.
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";

        try
        {
            var userId = await _userManagement.CreateAsync(request, actorId);
            return StatusCode(StatusCodes.Status201Created, new { userId });
        }
        catch (DuplicateEmailException)
        {
            // AC-005: safe 409 message — does not reveal active/inactive status (OWASP A07).
            return Conflict(new { error = "A user with this email already exists" });
        }
    }

    /// <summary>
    /// Partially updates a user's role and/or activation status.
    /// Returns HTTP 200 on success, HTTP 400 for invalid input, HTTP 404 when the user is not found.
    /// </summary>
    [HttpPatch("{id:int}")]
    public async Task<ActionResult> PatchAsync(int id, [FromBody] PatchUserRequest request)
    {
        // Validate that at least one field is present — an empty PATCH is meaningless.
        if (request.Role is null && request.IsActive is null)
            return BadRequest(new { error = "At least one field (role or isActive) must be provided" });

        // Edge: invalid role — validate before DB access so no partial write occurs (OWASP A03).
        if (request.Role is not null && !AllowedRoles.Contains(request.Role))
            return BadRequest(new { role = "Invalid role value" });

        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("sub")
                   ?? "unknown";

        var found = await _userManagement.PatchAsync(id, request, actorId);
        return found ? Ok() : NotFound(new { error = $"User {id} not found" });
    }
}
