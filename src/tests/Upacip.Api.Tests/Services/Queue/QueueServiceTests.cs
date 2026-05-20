namespace Upacip.Api.Tests.Services.Queue;

/// <summary>
/// Test plan for EP-006: Staff Queue, Walk-in Management, and Dashboards.
/// Stories: US-025 (walk-in + SignalR), US-026 (mark arrived idempotency),
///          US-027 (staff queue dashboard), US-028 (KPI metrics), US-029 (patient RBAC).
///
/// Implementation target: Sprint 4.
/// </summary>
public sealed class QueueServiceTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // US-025 — Walk-in Booking Creation
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-QUEUE-001: POST /walkins with Staff JWT returns 201 and booking created")]
    public void CreateWalkIn_StaffJwt_Returns201()
        => throw new NotImplementedException("Sprint 4 — WalkInController");

    [Fact(DisplayName = "TC-QUEUE-002: POST /walkins with Patient JWT returns 403 Forbidden")]
    public void CreateWalkIn_PatientJwt_Returns403()
        => throw new NotImplementedException("Sprint 4 — RBAC enforcement");

    [Fact(DisplayName = "TC-QUEUE-003: Walk-in booking has status=WalkIn")]
    public void CreateWalkIn_BookingStatusIsWalkIn()
        => throw new NotImplementedException("Sprint 4");

    [Fact(DisplayName = "TC-QUEUE-004: Walk-in creation writes WALKIN_CREATED audit log entry")]
    public void CreateWalkIn_WritesAuditLog()
        => throw new NotImplementedException("Sprint 4");

    // ═══════════════════════════════════════════════════════════════════════
    // US-026 — Mark Patient as Arrived (Idempotency)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-QUEUE-005: PATCH /bookings/{id}/status Arrived sets arrivedAt timestamp")]
    public void MarkArrived_SetsArrivedAt()
        => throw new NotImplementedException("Sprint 4 — BookingStatusController");

    [Fact(DisplayName = "TC-QUEUE-006: Double-marking same patient as Arrived is idempotent (no duplicate timestamps)")]
    public void MarkArrived_CalledTwice_IsIdempotent()
        => throw new NotImplementedException("Sprint 4 — idempotency check on arrivedAt");

    [Fact(DisplayName = "TC-QUEUE-007: PATCH /bookings/{id}/status Arrived with Patient JWT returns 403")]
    public void MarkArrived_PatientJwt_Returns403()
        => throw new NotImplementedException("Sprint 4 — RBAC");

    [Fact(DisplayName = "TC-QUEUE-008: Marking arrived writes PATIENT_ARRIVED audit log entry")]
    public void MarkArrived_WritesAuditLog()
        => throw new NotImplementedException("Sprint 4");

    // ═══════════════════════════════════════════════════════════════════════
    // US-028 — Admin KPI Metrics
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-QUEUE-009: No-show rate = COUNT(no-shows) / COUNT(total appointments) for current month")]
    public void KpiMetrics_NoShowRate_CalculatedCorrectly()
        => throw new NotImplementedException("Sprint 4 — KpiMetricsService");

    [Fact(DisplayName = "TC-QUEUE-010: AI-Human Agreement Rate = accepted-without-correction / total reviewed")]
    public void KpiMetrics_AiHumanAgreementRate_CalculatedCorrectly()
        => throw new NotImplementedException("Sprint 4");

    [Fact(DisplayName = "TC-QUEUE-011: Critical Conflicts count = total unresolved ConflictFlag records")]
    public void KpiMetrics_CriticalConflicts_CountsUnresolved()
        => throw new NotImplementedException("Sprint 4");

    [Fact(DisplayName = "TC-QUEUE-012: GET /admin/metrics with non-Admin JWT returns 403")]
    public void KpiMetrics_NonAdminJwt_Returns403()
        => throw new NotImplementedException("Sprint 4 — RBAC");

    [Fact(DisplayName = "TC-QUEUE-013: KPI metrics return 'No data yet' placeholder when no records exist")]
    public void KpiMetrics_NoData_ReturnsPlaceholder()
        => throw new NotImplementedException("Sprint 4 — empty state handling");

    // ═══════════════════════════════════════════════════════════════════════
    // US-029 — Patient Cannot Self-Check-In
    // ═══════════════════════════════════════════════════════════════════════

    [Fact(DisplayName = "TC-QUEUE-014: Patient JWT on POST /walkins → 403 and audit UNAUTHORIZED_ACCESS_ATTEMPT")]
    public void PatientJwt_WalkInEndpoint_Returns403AndLogsAudit()
        => throw new NotImplementedException("Sprint 4");

    [Fact(DisplayName = "TC-QUEUE-015: Patient JWT on PATCH /bookings/{id}/status Arrived → 403")]
    public void PatientJwt_MarkArrivedEndpoint_Returns403()
        => throw new NotImplementedException("Sprint 4");
}
