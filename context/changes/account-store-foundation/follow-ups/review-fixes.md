# Review Fixes: account-store-foundation

Follow-ups queued from `reviews/impl-review-phase-1.md` triage.

## From F1 (key ring preload woke the database on every cold start)

Code fix applied in `api/Program.cs`: the `DataProtectionHostedService` preload is removed, so the key ring loads on the first protect/unprotect call.

- [x] **Phase 2 test** (done in e41f94b): in `HealthProbeTests`, pin that startup and `/api/health/live` do not touch the database. For example, start the factory with an unreachable connection string and assert `live` returns 200 quickly. Also assert that `DataProtectionKeys` is still empty after host start plus a `live` call against the container, and that it holds one key after `ready`.
- [x] **Plan invariant** (recorded as a lesson in `context/foundation/lessons.md`, "Only work that needs the database may touch it; startup and liveness never do", and enforced by `HealthProbeTests`; plan.md phase blocks left unchanged): only `/api/health/ready`, and after S-04 requests that use Data Protection (auth cookies), may touch the database. App startup and `/api/health/live` never do.

## From F6 (ready has no time limit while the database resumes)

- [ ] **Phase 4 smoke test**: add `--max-time 120` to the `/api/health/ready` curl so a hung request becomes a retry within the 10 × 15 s loop.

## From Phase 2 implementation (2026-09-25)

- [ ] **Watch the 5 s startup limit**: `HealthProbeTests.Startup_and_live_succeed_quickly_with_an_unreachable_database` requires startup plus `live` under 5 s. It only catches startup blocking on connection retries (the key-count test is the real "no database at startup" guard), so if it flakes on a slow CI runner, loosen the limit rather than delete the test.
