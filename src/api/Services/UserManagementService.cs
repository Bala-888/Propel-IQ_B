using System.Security.Cryptography;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Implements <see cref="IUserManagementService"/> for the <c>POST /admin/users</c> and
/// <c>PATCH /admin/users/{id}</c> endpoints.
/// </summary>
public sealed class UserManagementService : IUserManagementService
{
    private static readonly HashSet<string> AllowedRoles = new(StringComparer.Ordinal)
        { "Patient", "Staff", "Admin" };

    // Character set for temporary passwords: no ambiguous chars (I, l, 0, O) (AC-001)
    private const string TempPasswordChars =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*";

    private readonly AppDbContext    _db;
    private readonly IAuditLogger    _auditLogger;
    private readonly IEmailSender    _emailSender;

    public UserManagementService(AppDbContext db, IAuditLogger auditLogger, IEmailSender emailSender)
    {
        _db          = db;
        _auditLogger = auditLogger;
        _emailSender = emailSender;
    }

    /// <inheritdoc />
    public async Task<int> CreateAsync(
        CreateUserRequest request,
        string actorId,
        CancellationToken ct = default)
    {
        // AC-005: duplicate email check — Username stores the email in this codebase.
        // AnyAsync avoids materialising the full table; check is case-insensitive (OWASP A07:
        // does not reveal whether the existing account is active or deactivated).
        var emailLower = request.Email.ToLowerInvariant();
        var exists = await _db.Users
            .AnyAsync(u => u.Username.ToLower() == emailLower, ct);

        if (exists)
            throw new DuplicateEmailException(request.Email);

        // AC-001: generate cryptographically random 12-character temporary password.
        var tempPassword = GenerateTempPassword(12);

        // OWASP A02: hash before storing — never persist plaintext.
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        var user = new User
        {
            Username           = request.Email,
            Name               = request.Name,
            PasswordHash       = passwordHash,
            Role               = request.Role,
            CreatedAt          = DateTime.UtcNow,
            IsActive           = true,
            MustChangePassword = true, // credential lifecycle: temp password requires change on first login
        };
        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        // AC-001: audit log after successful DB commit (OWASP A09).
        _auditLogger.Log(actorId, "UserCreated", user.Id.ToString());

        // AC-001: dispatch welcome email — failure is non-fatal; send AFTER DB commit so the
        // account exists even if the email transport is unavailable.
        await _emailSender.SendEmailAsync(
            to:      request.Email,
            subject: "Welcome — your account has been created",
            body:
                $"Hello {request.Name},\n\n" +
                $"Your UPACIP account has been created.\n\n" +
                $"Email:             {request.Email}\n" +
                $"Temporary password: {tempPassword}\n\n" +
                "You will be prompted to change your password on first sign-in.\n\n" +
                "If you did not request this account, please contact your administrator immediately.",
            ct: ct);

        return user.Id;
    }

    /// <inheritdoc />
    public async Task<bool> PatchAsync(
        int id,
        PatchUserRequest request,
        string actorId,
        CancellationToken ct = default)
    {
        var user = await _db.Users.FindAsync(new object[] { id }, ct);
        if (user is null) return false;

        // Apply role change (AC-002)
        if (request.Role is not null)
        {
            var previousRole = user.Role;
            user.Role = request.Role;

            var payload = JsonSerializer.Serialize(
                new { from = previousRole, to = request.Role },
                new JsonSerializerOptions { PropertyNamingPolicy = null });

            _auditLogger.Log(actorId, "RoleChanged", user.Id.ToString(), payload);
        }

        // Apply activation toggle (AC-003; Edge: reactivation)
        if (request.IsActive.HasValue)
        {
            user.IsActive = request.IsActive.Value;

            var actionType = request.IsActive.Value ? "UserReactivated" : "UserDeactivated";
            _auditLogger.Log(actorId, actionType, user.Id.ToString());
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>
    /// Generates a cryptographically random password of <paramref name="length"/> characters
    /// from <see cref="TempPasswordChars"/>. Uses <see cref="RandomNumberGenerator.Fill"/>
    /// — not <see cref="Random"/> (OWASP A02; AC-001).
    /// </summary>
    private static string GenerateTempPassword(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => TempPasswordChars[b % TempPasswordChars.Length]).ToArray());
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Api.DTOs.UserResponse>> GetAllAsync(CancellationToken ct = default)
    {
        return await _db.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .Select(u => new Api.DTOs.UserResponse
            {
                Id        = u.Id,
                Name      = u.Name,
                Email     = u.Username,
                Role      = u.Role,
                IsActive  = u.IsActive,
                CreatedAt = u.CreatedAt,
            })
            .ToListAsync(ct);
    }
}
