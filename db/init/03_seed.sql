-- TASK-013: Seed data for development and testing.
-- Run after EF Core migrations have been applied.
-- This script is idempotent (uses ON CONFLICT DO NOTHING).

-- ─── Admin user ─────────────────────────────────────────────────────────────
-- Default admin account for first-run setup.
-- Password hash: "Admin@upacip1" — CHANGE IMMEDIATELY in production.
-- BCrypt hash generated with work factor 12.
INSERT INTO app_users (
    id, email, password_hash, full_name, role, is_active, created_at
) VALUES (
    '00000000-0000-0000-0000-000000000001',
    'harsha@clinic.org',
    '$2a$12$PlaceholderHashReplaceWithBCryptHashOfActualPassword',
    'Harsha Admin',
    'Admin',
    true,
    NOW()
) ON CONFLICT (email) DO NOTHING;

-- ─── Sample Staff users ──────────────────────────────────────────────────────
INSERT INTO app_users (id, email, password_hash, full_name, role, is_active, created_at)
VALUES
    ('00000000-0000-0000-0000-000000000002', 'pawan@clinic.org',
     '$2a$12$PlaceholderHashReplaceWithBCryptHashOfActualPassword', 'Pawan Staff',
     'Staff', true, NOW()),
    ('00000000-0000-0000-0000-000000000003', 'babu@clinic.org',
     '$2a$12$PlaceholderHashReplaceWithBCryptHashOfActualPassword', 'Babu Staff',
     'Staff', true, NOW())
ON CONFLICT (email) DO NOTHING;

-- ─── InsuranceRecord seed data ───────────────────────────────────────────────
-- Used by the insurance pre-check soft-validation feature (TASK-040 / US-017).
-- member_id_pattern is a regex matched case-insensitively against the member ID provided.
INSERT INTO insurance_records (id, provider_name, member_id_pattern, is_active, created_at)
VALUES
    (gen_random_uuid(), 'BlueCross BlueShield', '^[A-Z]{3}[0-9]{9}$', true, NOW()),
    (gen_random_uuid(), 'Aetna',                '^W[0-9]{9}$',          true, NOW()),
    (gen_random_uuid(), 'UnitedHealthcare',     '^[0-9]{10}$',          true, NOW()),
    (gen_random_uuid(), 'Cigna',                '^U[0-9]{8}$',          true, NOW()),
    (gen_random_uuid(), 'Humana',               '^H[0-9]{8}[A-Z]$',     true, NOW()),
    (gen_random_uuid(), 'Medicaid',             '^[0-9]{10,12}$',       true, NOW()),
    (gen_random_uuid(), 'Medicare',             '^[1-9][A-Z]{2}[0-9]{9}$', true, NOW()),
    (gen_random_uuid(), 'Kaiser Permanente',    '^KP[0-9]{8}$',         true, NOW()),
    (gen_random_uuid(), 'Anthem',               '^ANT[0-9]{8}$',        true, NOW()),
    (gen_random_uuid(), 'Centene',              '^CTN[0-9]{7}$',        true, NOW())
ON CONFLICT DO NOTHING;

-- ─── Sample appointment slots (next 7 days) ──────────────────────────────────
-- Generates 4 slots per day at 9am, 10am, 2pm, 3pm for the next 7 days.
INSERT INTO appointment_slots (id, scheduled_at, duration_minutes, status, created_at, updated_at)
SELECT
    gen_random_uuid(),
    (NOW()::date + day_offset + slot_time)::timestamp AT TIME ZONE 'UTC',
    30,
    'Available',
    NOW(),
    NOW()
FROM
    generate_series(1, 7) AS day_offset,
    (VALUES
        (INTERVAL '9 hours'),
        (INTERVAL '10 hours'),
        (INTERVAL '14 hours'),
        (INTERVAL '15 hours')
    ) AS slots(slot_time)
ON CONFLICT DO NOTHING;
