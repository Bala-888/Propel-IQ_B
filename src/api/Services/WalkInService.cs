using System.Security.Cryptography;
using System.Text.Json;
using Api.Data;
using Api.Data.Entities;
using Api.DTOs;
using Api.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// Implements <see cref="IWalkInService"/> for <c>POST /walkins</c>.
/// Three execution paths are selected by the fields present in the request:
/// booking-only, account-creation, and link-existing (AC-001, AC-002, AC-004).
/// </summary>
public sealed class WalkInService : IWalkInService
{
    // Character set for temporary passwords: no ambiguous chars (I, l, 0, O) (AC-002; OWASP A02)
    private const string TempPasswordChars =
        "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnpqrstuvwxyz23456789!@#$%^&*";

    private readonly AppDbContext  _db;
    private readonly IAuditLogger  _auditLogger;
    private readonly IEmailSender  _emailSender;
    private readonly ILogger<WalkInService> _logger;

    public WalkInService(
        AppDbContext db,
        IAuditLogger auditLogger,
        IEmailSender emailSender,
        ILogger<WalkInService> logger)
    {
        _db          = db;
        _auditLogger = auditLogger;
        _emailSender = emailSender;
        _logger      = logger;
    }

    /// <inheritdoc />
    public async Task<CreateWalkInResponse> CreateAsync(
        CreateWalkInRequest request,
        string actorId,
        CancellationToken ct = default)
    {
        // Path 3: link-existing takes precedence over createAccount (AC-004 Yes path).
        if (request.LinkExistingAccountId.HasValue)
            return await LinkExistingAsync(request, actorId, ct);

        // Path 2: create new account + booking (AC-002).
        if (request.CreateAccount)
            return await CreateWithAccountAsync(request, actorId, ct);

        // Path 1: booking-only — no patient account (AC-001).
        return await CreateBookingOnlyAsync(request, actorId, ct);
    }

    // ── Path 1 — Booking only (AC-001) ──────────────────────────────────────────────────────────

    private async Task<CreateWalkInResponse> CreateBookingOnlyAsync(
        CreateWalkInRequest request,
        string actorId,
        CancellationToken ct)
    {
        var booking = BuildBooking(request, userId: null);
        _db.WalkInBookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        _auditLogger.Log(actorId, "WalkInCreated", booking.Id.ToString());

        return new CreateWalkInResponse { BookingId = booking.Id };
    }

    // ── Path 2 — Account creation (AC-002) ──────────────────────────────────────────────────────

    private async Task<CreateWalkInResponse> CreateWithAccountAsync(
        CreateWalkInRequest request,
        string actorId,
        CancellationToken ct)
    {
        var email = request.Email!; // validated non-empty by IValidatableObject before this point

        // AC-004: duplicate email check — Username stores the login email for all users.
        // Case-insensitive match; does NOT reveal whether the account is active (OWASP A07).
        // We project to Id only to keep the query minimal and to expose the id to the
        // WalkInsController 409 response so the front desk can use the link-existing flow.
        var emailLower = email.ToLowerInvariant();
        var existingId = await _db.Users
            .Where(u => u.Username.ToLower() == emailLower)
            .Select(u => (int?)u.Id)
            .FirstOrDefaultAsync(ct);

        if (existingId.HasValue)
            throw new DuplicateEmailException(email, existingId.Value);

        // AC-002: generate cryptographically secure 12-char temp password (OWASP A02).
        var tempPassword = GenerateTempPassword(12);
        var passwordHash = BCrypt.Net.BCrypt.HashPassword(tempPassword);

        var newUser = new User
        {
            Username           = email,
            Name               = request.PatientName,
            PasswordHash       = passwordHash,
            Role               = "Patient",
            CreatedAt          = DateTime.UtcNow,
            IsActive           = true,
            MustChangePassword = true,
        };
        _db.Users.Add(newUser);

        var booking = BuildBooking(request, userId: null); // user added below after SaveChanges assigns Id
        _db.WalkInBookings.Add(booking);

        // CRITICAL: commit booking + user in the same transaction BEFORE sending email.
        // Email failure must never roll back a committed booking (Edge: delivery failure; AC-003).
        await _db.SaveChangesAsync(ct);

        // Link booking to the newly created user after SaveChanges has assigned newUser.Id.
        booking.UserId = newUser.Id;
        await _db.SaveChangesAsync(ct);

        _auditLogger.Log(actorId, "WalkInCreated", booking.Id.ToString());

        // AC-003: dispatch credentials email after commit; retry up to 3 times (Edge: delivery failure).
        var emailFailed = await TrySendCredentialsEmailAsync(
            email, request.PatientName, tempPassword, booking.Id, ct);

        return new CreateWalkInResponse
        {
            BookingId             = booking.Id,
            UserId                = newUser.Id,
            CredentialsEmailFailed = emailFailed,
        };
    }

    // ── Path 3 — Link existing account (AC-004 Yes path) ───────────────────────────────────────

    private async Task<CreateWalkInResponse> LinkExistingAsync(
        CreateWalkInRequest request,
        string actorId,
        CancellationToken ct)
    {
        var existingUserId = request.LinkExistingAccountId!.Value;

        // Verify the referenced user exists (guard against stale UI data).
        var userExists = await _db.Users.AnyAsync(u => u.Id == existingUserId, ct);
        if (!userExists)
        {
            // Caller (controller) translates this to 404.
            throw new KeyNotFoundException($"User {existingUserId} not found");
        }

        var booking = BuildBooking(request, userId: existingUserId);
        _db.WalkInBookings.Add(booking);
        await _db.SaveChangesAsync(ct);

        // Audit: record both booking ID and linked patient ID for traceability (OWASP A09).
        var details = System.Text.Json.JsonSerializer.Serialize(
            new { walkInBookingId = booking.Id, linkedUserId = existingUserId },
            new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = null });

        _auditLogger.Log(actorId, "WalkInAccountLinked", booking.Id.ToString(), details);

        return new CreateWalkInResponse
        {
            BookingId = booking.Id,
            UserId    = existingUserId,
        };
    }

    // ── Helpers ─────────────────────────────────────────────────────────────────────────────────

    private static WalkInBooking BuildBooking(CreateWalkInRequest request, int? userId) =>
        new()
        {
            PatientName = request.PatientName,
            DateOfBirth = request.DateOfBirth.ToString("yyyy-MM-dd"),
            CreatedAt   = DateTime.UtcNow,
            UserId      = userId,
        };

    /// <summary>
    /// Attempts to send a credentials email up to 3 times.
    /// Returns <c>true</c> if all attempts fail, <c>false</c> if one succeeds.
    /// No <see cref="Task.Delay"/> between retries — the 30-second AC-003 budget must not
    /// be consumed by artificial wait time; retries are purely for transient SMTP faults (AC-003).
    /// <see cref="OperationCanceledException"/> is always re-thrown to honour cancellation (OWASP A05).
    /// </summary>
    private async Task<bool> TrySendCredentialsEmailAsync(
        string email,
        string patientName,
        string tempPassword,
        int bookingId,
        CancellationToken ct)
    {
        var body =
            $"Hello {patientName},\n\n" +
            "A walk-in appointment has been booked for you at UPACIP.\n\n" +
            $"Email:              {email}\n" +
            $"Temporary password: {tempPassword}\n\n" +
            "Please sign in and change your password at your earliest convenience.\n\n" +
            "If you did not visit our clinic, please contact us immediately.";

        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                await _emailSender.SendEmailAsync(
                    to:      email,
                    subject: "Your UPACIP account — temporary password",
                    body:    body,
                    ct:      ct);
                return false; // success — no failure flag
            }
            catch (OperationCanceledException)
            {
                throw; // never swallow cancellation
            }
            catch (Exception ex)
            {
                if (attempt == 2)
                {
                    // All 3 attempts exhausted — log and signal the failure flag (OWASP A09).
                    _logger.LogError(
                        ex,
                        "Credentials email failed after 3 attempts for bookingId={BookingId}",
                        bookingId);
                    return true;
                }
                // attempt < 2: fall through to next iteration (retry)
            }
        }

        return false; // unreachable — loop exits via return statements above
    }

    /// <summary>
    /// Generates a cryptographically random password of <paramref name="length"/> characters
    /// from <see cref="TempPasswordChars"/>. Uses <see cref="RandomNumberGenerator.Fill"/>
    /// — not <see cref="Random"/> (OWASP A02; AC-002).
    /// </summary>
    private static string GenerateTempPassword(int length)
    {
        var bytes = new byte[length];
        RandomNumberGenerator.Fill(bytes);
        return new string(bytes.Select(b => TempPasswordChars[b % TempPasswordChars.Length]).ToArray());
    }
}
