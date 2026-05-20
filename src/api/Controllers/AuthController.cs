using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Api.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Api.Controllers;

[ApiController]
[Route("auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly IAuditLogger _auditLogger;
    private readonly ITokenService _tokenService;
    private readonly ILoginRateLimiter _rateLimiter;

    public AuthController(
        AppDbContext db,
        IAuditLogger auditLogger,
        ITokenService tokenService,
        ILoginRateLimiter rateLimiter)
    {
        _db          = db;
        _auditLogger = auditLogger;
        _tokenService = tokenService;
        _rateLimiter = rateLimiter;
    }

    /// <summary>
    /// Registers a new patient account.
    /// Validates the request, checks for duplicate email, persists User + Patient entities
    /// (PHI encrypted via EF Core value converters), and writes a PatientRegistration audit entry.
    /// Returns HTTP 201 with {userId, role}. No PHI is included in the response (OWASP A02).
    /// </summary>
    [HttpPost("register")]
    public async Task<IActionResult> RegisterAsync([FromBody] RegisterPatientRequest request)
    {
        // AC-003 / Edge: email format — ModelState validated by [ApiController] via
        // InvalidModelStateResponseFactory before reaching this action (OWASP A03: validate at boundary).
        // DOB future guard fires here, after format/required errors, so structural errors fire first.
        if (request.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            return BadRequest(new
            {
                validationErrors = new Dictionary<string, string>
                {
                    ["dateOfBirth"] = "Date of birth cannot be in the future"
                }
            });
        }

        // Normalise empty-string insurance fields to null before any DB write (Edge: insurance optional)
        var insuranceProvider = string.IsNullOrEmpty(request.InsuranceProvider) ? null : request.InsuranceProvider;
        var insuranceId       = string.IsNullOrEmpty(request.InsuranceId)       ? null : request.InsuranceId;

        // AC-002: duplicate email check — EF Core value converters decrypt PHI columns on materialisation.
        // Probabilistic AES-GCM encryption means ciphertext is non-deterministic, so the comparison must
        // occur in memory after decryption. ToListAsync() materialises all rows; value converters run
        // client-side on the returned bytea data.
        // Decision[2026-05-20]: full-scan is acceptable at current volume; add a deterministic
        // email_hash column (indexed) if this becomes a performance concern.
        var patientEmails = await _db.Patients
            .AsNoTracking()
            .Select(p => p.Email)
            .ToListAsync();

        if (patientEmails.Any(e => string.Equals(e, request.Email, StringComparison.OrdinalIgnoreCase)))
        {
            return Conflict(new { error = "An account with this email already exists" });
        }

        // Split full Name into FirstName / LastName for the Patient entity.
        // Decision[2026-05-20]: Patient entity requires separate FirstName/LastName columns.
        // Single-token names are stored as FirstName with LastName = "".
        var nameParts = request.Name.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
        var firstName = nameParts[0];
        var lastName  = nameParts.Length > 1 ? nameParts[1] : string.Empty;

        // AC-001: create User (role = Patient) and Patient entities.
        // Decision[2026-05-20]: User.Username set to Email; PasswordHash = "" — password
        // credential setup is handled by a separate task (login/set-password flow).
        var newUser = new User
        {
            Username     = request.Email,
            Name         = request.Name,  // stored for user management table (us_011/task_002)
            PasswordHash = string.Empty,
            Role         = "Patient",
            CreatedAt    = DateTime.UtcNow
        };
        _db.Users.Add(newUser);

        // EF Core value converters encrypt PHI columns (Email, Phone, DateOfBirth,
        // InsuranceProvider, InsuranceId) automatically on SaveChangesAsync (AC-001; OWASP A02).
        var newPatient = new Patient
        {
            FirstName        = firstName,
            LastName         = lastName,
            Email            = request.Email,
            Phone            = request.Phone,
            DateOfBirth      = request.DateOfBirth.ToString("yyyy-MM-dd"),
            InsuranceProvider = insuranceProvider,
            InsuranceId      = insuranceId,
            CreatedAt        = DateTime.UtcNow
        };
        _db.Patients.Add(newPatient);

        await _db.SaveChangesAsync();

        // AC-001: audit log written after successful SaveChangesAsync — no audit entry for failed
        // registrations (OWASP A09 audit completeness; edge: partial write never audited).
        _auditLogger.Log(newUser.Id.ToString(), "PatientRegistration", newPatient.Id.ToString());

        // AC-001: HTTP 201; response contains only userId + role — no PHI (OWASP A02).
        return CreatedAtAction(
            nameof(RegisterAsync),
            new RegisterPatientResponse { UserId = newUser.Id, Role = "Patient" });
    }

    /// <summary>
    /// Authenticates a patient by email + password and issues a JWT access token + refresh token pair.
    /// Returns HTTP 200 {accessToken, refreshToken, role} on success.
    /// Returns HTTP 401 with identical body for both unknown-email and wrong-password (AC-005; OWASP A07).
    /// </summary>
    [HttpPost("login")]
    public async Task<IActionResult> LoginAsync([FromBody] LoginRequest request)
    {
        // AC-004: rate-limit check is the very first operation — no DB round-trip wasted on a blocked IP.
        // Client IP is available via HttpContext.Connection.RemoteIpAddress; UseForwardedHeaders middleware
        // (already configured in Program.cs) populates this with the real client IP when behind nginx.
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (await _rateLimiter.GetFailCountAsync(ip) >= 5)
        {
            // AC-004: HTTP 429 + Retry-After header (RFC 6585 §4)
            Response.Headers.Append("Retry-After", "900");
            return StatusCode(StatusCodes.Status429TooManyRequests,
                new { error = "Too many failed attempts. Please try again in 15 minutes." });
        }

        // Email lookup: Username stores the registration email (set in RegisterAsync: Username = Email)
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == request.Email.ToLower());

        // AC-005 / OWASP A07: identical 401 message for both branches so callers cannot enumerate accounts
        if (user is null)
        {
            await _rateLimiter.IncrementAsync(ip); // failure-specific counter (AC-004)
            _auditLogger.Log("unknown", "LoginFailure", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // OWASP A02: constant-time BCrypt comparison — never raw string equality
        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            await _rateLimiter.IncrementAsync(ip); // failure-specific counter (AC-004)
            _auditLogger.Log(user.Id.ToString(), "LoginFailure", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // AC-003 / OWASP A07: inactive check occurs AFTER password verification to avoid timing oracle.
        // Return the identical message as invalid-password so callers cannot enumerate account status.
        // Do NOT call IncrementAsync here — account deactivation is administrative, not a credential
        // failure; incrementing would allow blocking a legitimate user by deactivating their account.
        if (!user.IsActive)
        {
            _auditLogger.Log(user.Id.ToString(), "UserDeactivatedLoginAttempt", request.Email);
            return Unauthorized(new { error = "Invalid email or password" });
        }

        // Issue access + refresh token pair; FamilyId is new for every first-issue login
        var accessToken  = _tokenService.GenerateAccessToken(user);
        var refreshToken = _tokenService.GenerateRefreshToken();

        var refreshEntity = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = user.Id,
            Token     = refreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            FamilyId  = Guid.NewGuid(), // new family for every initial login (RFC 6819)
            CreatedAt = DateTime.UtcNow,
        };
        _db.RefreshTokens.Add(refreshEntity);
        await _db.SaveChangesAsync();

        // AC-005: reset the failure counter so a subsequent failed attempt from the same IP
        // starts a fresh count of 1 (not blocked by this session's pre-login failures).
        await _rateLimiter.ResetAsync(ip);

        _auditLogger.Log(user.Id.ToString(), "LoginSuccess", request.Email);

        return Ok(new LoginResponse
        {
            AccessToken  = accessToken,
            RefreshToken = refreshToken,
            Role         = user.Role,
        });
    }

    /// <summary>
    /// Rotates a valid refresh token, issuing a new access + refresh token pair.
    /// Detected reuse (token already revoked) triggers revocation of the entire token family
    /// to invalidate all outstanding sessions (Edge: replay attack; RFC 6819 §5.2.1).
    /// </summary>
    [HttpPost("refresh")]
    public async Task<IActionResult> RefreshAsync([FromBody] RefreshRequest request)
    {
        var existing = await _db.RefreshTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

        if (existing is null)
        {
            return Unauthorized(new { error = "Invalid refresh token" });
        }

        // Edge: replay attack — if the token has already been revoked, an attacker has
        // obtained a previously-used token. Revoke all tokens in the same family (RFC 6819).
        if (existing.IsRevoked)
        {
            var familyTokens = await _db.RefreshTokens
                .Where(rt => rt.FamilyId == existing.FamilyId && !rt.IsRevoked)
                .ToListAsync();
            foreach (var t in familyTokens)
                t.IsRevoked = true;

            await _db.SaveChangesAsync();
            _auditLogger.Log(existing.UserId.ToString(), "RefreshTokenReuse", existing.FamilyId.ToString());
            return Unauthorized(new { error = "Refresh token reuse detected" });
        }

        if (existing.ExpiresAt < DateTime.UtcNow)
        {
            return Unauthorized(new { error = "Refresh token has expired" });
        }

        // Revoke the consumed token and issue a new pair with the same FamilyId (token rotation)
        existing.IsRevoked = true;

        var accessToken      = _tokenService.GenerateAccessToken(existing.User);
        var newRefreshToken  = _tokenService.GenerateRefreshToken();

        var rotatedEntity = new RefreshToken
        {
            Id        = Guid.NewGuid(),
            UserId    = existing.UserId,
            Token     = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(7),
            IsRevoked = false,
            FamilyId  = existing.FamilyId, // same family — rotation, not re-issue (RFC 6819)
            CreatedAt = DateTime.UtcNow,
        };
        _db.RefreshTokens.Add(rotatedEntity);
        await _db.SaveChangesAsync();

        _auditLogger.Log(existing.UserId.ToString(), "RefreshTokenRotated", existing.FamilyId.ToString());

        return Ok(new LoginResponse
        {
            AccessToken  = accessToken,
            RefreshToken = newRefreshToken,
            Role         = existing.User.Role,
        });
    }
}
