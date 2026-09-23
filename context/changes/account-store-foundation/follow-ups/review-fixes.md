# Review Fixes: account-store-foundation

Follow-ups queued from `reviews/impl-review-phase-1.md` triage.

## From F1 (key ring preload woke the database on every cold start)

Code fix applied in `api/Program.cs`: the `DataProtectionHostedService` preload is removed, so the key ring loads on the first protect/unprotect call.

- [ ] **Phase 2 test**: in `HealthProbeTests`, pin that startup and `/api/health/live` do not touch the database. For example, start the factory with an unreachable connection string and assert `live` returns 200 quickly. Also assert that `DataProtectionKeys` is still empty after host start plus a `live` call against the container, and that it holds one key after `ready`.
- [ ] **Plan invariant** (fold into plan when Phase 2 starts): only `/api/health/ready`, and after S-04 requests that use Data Protection (auth cookies), may touch the database. App startup and `/api/health/live` never do.

## From F6 (ready has no time limit while the database resumes)

- [ ] **Phase 4 smoke test**: add `--max-time 120` to the `/api/health/ready` curl so a hung request becomes a retry within the 10 × 15 s loop.
