---
date: 2026-10-15T23:25:01+02:00
researcher: Claude Sonnet 5
git_commit: ea5883fe1810d7fda6fdebfcdd6f02fd3307436f
branch: test/seed-determinism
repository: battle-map-generator
topic: "Abuse-resistant quota: is the per-account generate limit a bounded ceiling against scripted multi-account abuse? (test-plan.md §3 Phase 3, risk #3)"
tags: [research, codebase, rate-limiting, auth, generate, api, testing-abuse-resistant-quota]
status: complete
last_updated: 2026-10-15
last_updated_by: Claude Sonnet 5
---

# Research: Abuse-resistant quota (test-plan Phase 3, risk #3)

**Date**: 2026-10-15T23:25:01+02:00
**Researcher**: Claude Sonnet 5
**Git Commit**: ea5883fe1810d7fda6fdebfcdd6f02fd3307436f
**Branch**: test/seed-determinism
**Repository**: battle-map-generator

## Research Question

Risk #3 (`context/foundation/test-plan.md:46`): a scripted client creates accounts and generates maps, bypassing the per-account generation limit, and exhausts the F1 free plan's allowance for everyone. The phase's own "must challenge" line (`test-plan.md:70`): *"the per-account limit already protects the plan" — nothing bounds the number of accounts, so the real ceiling is (accounts per minute) × 10.* What does the code actually enforce today, and what would a scripted multi-account load test need to prove or disprove that?

## Summary

The per-account `generate` limit (10/min, keyed by the Identity user id claim — `api/Program.cs:51-58`) and the per-IP `auth` limit (10/min, shared by register and login — `api/Program.cs:60-67`) are the only two throttles on this path. Under this code, nothing bounds the total number of accounts a client can create, and nothing bounds aggregate generate throughput across accounts: from one client IP, a scripted attacker can register up to 10 accounts per minute (register signs the account in immediately — `api/Auth/AuthEndpoints.cs:62-68` — so no separate login call is even needed), and every account registered this way keeps its own permanent 10/min generate allowance forever after. Each additional IP multiplies the account-creation rate by another 10/min. There is no global limiter (`options.GlobalLimiter` is unset in `api/Program.cs`), no cap on account count, no CAPTCHA, and no email verification (`api/Program.cs`, `api/Auth/AuthEndpoints.cs` — confirmed absent). The test-plan's own anti-pattern note (`test-plan.md:70`) rules out CAPTCHA/email-verification as an acceptable fix, so the real gap — an unbounded, ever-growing pool of accounts each entitled to 10 generate calls/min, with no system-wide ceiling — is an application-level rate-limiting gap, not a missing secondary control.

Existing tests already pin both named policies in isolation (`api.Tests/MapEndpointTests.cs:72` for `generate`; `api.Tests/AuthEndpointTests.cs:247-276` for `auth`), each using a single `ApiFactory` and a single simulated client. Phase 3's job is the combined, multi-account/multi-IP scenario those tests don't cover — and the test infrastructure has no helper for simulating distinct source IPs today; one must be added.

## Detailed Findings

### The `auth` policy (register + login, per client IP)

- `FixedWindowRateLimiter`, `PermitLimit=10`, `Window=1 minute`, `QueueLimit=0` (`api/Program.cs:64-66`).
- Partition key: `httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown"` (`api/Program.cs:61`).
- Register (`api/Auth/AuthEndpoints.cs:23`) and login (`api/Auth/AuthEndpoints.cs:27`) both call `.RequireRateLimiting(AuthRateLimitPolicy)` and share one bucket — stated explicitly in a comment at `api/Auth/AuthEndpoints.cs:17-18`. Logout and `/me` carry no rate limit (`api/Auth/AuthEndpoints.cs:30-35`).
- Behind Azure App Service, the IP is read from `X-Forwarded-For` via the `ASPNETCORE_FORWARDEDHEADERS_ENABLED` app setting; `api/AGENTS.md` records this as "set, verified 2026-09-30." This research pass did not re-verify that App Service's front end trusts only its own hop (i.e., that a client cannot forge an arbitrary `X-Forwarded-For` to rotate its apparent IP for free) — flagged as an open question below.

### The `generate` policy (per account)

- `FixedWindowRateLimiter`, `PermitLimit=10`, `Window=1 minute`, `QueueLimit=0` (`api/Program.cs:55-57`).
- Partition key: `httpContext.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "anonymous"` (`api/Program.cs:52`) — the Identity user id claim, populated from the session cookie. `RequireAuthorization()` on the route (`api/Maps/MapEndpoints.cs:25`) runs first, so the `"anonymous"` fallback is unreachable in practice (comment, `api/Program.cs:36-39`).
- Confirmed by test: `api.Tests/MapEndpointTests.cs:72` (`The_eleventh_generate_within_a_minute_returns_429_with_retry_after`) proves the 11th call in a minute for one account gets 429, and that a second account created on the same factory immediately gets its own full 10 — i.e., the bucket is per-account, with no shared or global budget (lines 72-92, second-account check at 89-91).

### No other cap exists

- No `options.GlobalLimiter` is configured anywhere in `api/Program.cs` — only the two named policies above plus `SessionCookieGate`.
- No cap on total accounts created, no CAPTCHA, no email-confirmation gate: `api/Auth/AuthEndpoints.cs` validates only email format/length and password length (8–128 chars, no complexity — `api/Program.cs:92-96`), then calls `UserManager.CreateAsync` → `SignInManager.SignInAsync` directly (`api/Auth/AuthEndpoints.cs:62-68`). The `EmailConfirmed` column exists (Identity's default schema) but nothing in application code sets `RequireConfirmedAccount`/`RequireConfirmedEmail`.
- The only adjacent control is Identity account lockout (`MaxFailedAccessAttempts=5`, 5-minute lockout — `api/Program.cs:97-98`), which throttles password-guessing against one existing account, not registration or aggregate generate volume.
- `SessionCookieGate` (`api/Auth/SessionCookieGate.cs`) is a separate 60/min-per-IP limiter that only gates requests carrying a session cookie, to protect the Data Protection key-ring reload path from forged cookies (`SessionCookieGate.cs:7-15,18,24-32`). It is unrelated to register/login/generate abuse and registered earlier in the pipeline (`api/Program.cs:184`, before `UseAuthentication()` at line 185).
- Rejection is uniform: HTTP 429 with a `Retry-After` header (seconds, from the lease's `RetryAfter` metadata, default 60s — `api/Program.cs:44-50`); `SessionCookieGate` duplicates the same response shape inline since it runs outside `AddRateLimiter`/`UseRateLimiter` (`SessionCookieGate.cs:50-55`).
- All limiter numbers (10/min, 10/min, 60/min) are hardcoded in `api/Program.cs` and `api/Auth/SessionCookieGate.cs`; no `appsettings*.json` file carries rate-limit configuration (checked `api/appsettings.json`, `api/appsettings.Development.json`; no `appsettings.Production.json` exists in the repo).

### The ceiling, computed

Given the two policies above and no other cap: from **one** client IP, a scripted client can register up to 10 accounts per minute (no login call needed — registration signs in immediately). Each of those accounts keeps a permanent, independent 10/min generate allowance with no expiry and no cap on the number of simultaneously "active" accounts. So steady-state generate throughput is bounded only by **(cumulative accounts ever registered) × 10/min**, and cumulative accounts registered is bounded only by **(elapsed minutes) × 10 × (number of distinct client IPs used)** — both grow without an application-level limit. This is the concrete shape of the test-plan's "must challenge" statement (`test-plan.md:70`), and it is slightly sharper than that line's own framing: the ceiling isn't just "(accounts per minute) × 10" for one minute, it compounds every subsequent minute as the account pool grows.

### Registration/login cost (relevant to how fast the gap can be exploited, not a Phase 4 measurement)

- Password hashing uses ASP.NET Core Identity's default `PasswordHasher<TUser>` (no custom `IPasswordHasher` registered anywhere in `api/`) — PBKDF2-HMAC-SHA256 at the framework default of 100,000 iterations, run once per register call (`api/Auth/AuthEndpoints.cs:62`, via `CreateAsync`) and once per login attempt including failed ones (`api/Auth/AuthEndpoints.cs:90`, via `PasswordSignInAsync`).
- Register costs roughly 3–5 DB round trips (1 insert, 2–4 reads for uniqueness pre-check and claims-principal construction); login costs roughly 2–4 (0–1 write, 2–3 reads) — both estimates are inferred from the Identity calls cited above, not from a SQL trace in this repo (no query-logging test exists to pin exact counts).
- Actual CPU-cost-per-operation on the F1/B1 host (needed to translate the account-creation rate into F1 CPU-minute consumption) is explicitly Phase 4's job (`test-plan.md:86`, "Generation cost budget" — "a one-off F1 measurement"), not Phase 3's. Phase 3's test proves the *rate* ceiling is unbounded; Phase 4 would price what that unbounded rate costs on the real host.

### Existing test coverage and infrastructure gaps for Phase 3

- `api.Tests/MapEndpointTests.cs:72` and `api.Tests/AuthEndpointTests.cs:247-276` each pin one named policy in isolation, using a single `ApiFactory` per test (`api.Tests/Infrastructure/ApiFactory.cs:13`). `ApiFactory.CreateLoggedInClientAsync(string? email = null)` (`ApiFactory.cs:51-58`) already is the "create N distinct accounts" helper Phase 3 needs — call it repeatedly for N accounts, each with its own session-cookie-carrying `HttpClient`.
- **No helper exists to simulate distinct source IPs.** `TestServer`'s `HttpContext.Connection.RemoteIpAddress` is always `null` in-process (comment, `api/Program.cs:40-41`; confirmed at `api.Tests/AuthEndpointTests.cs:11-12`), so every client built from one `ApiFactory` falls into the same `"unknown"` IP partition. A multi-IP scenario needs a new test seam — e.g. a `DelegatingHandler` injecting `X-Forwarded-For` plus enabling forwarded-headers processing in the test host — nothing reusable exists yet.
- Limiter counters live in process memory per `WebApplicationFactory`/`WebApplication` instance, with no external store; a fresh `ApiFactory` means fresh counters regardless of wall-clock time (`api.Tests/MapEndpointTests.cs:13-14`, `api.Tests/AuthEndpointTests.cs:11-12` comments; this is also why every existing rate-limit test builds its own factory and stays under the relevant permit count except the one test designed to exceed it).
- No `TimeProvider`/fake-clock abstraction exists anywhere in `api/` or `api.Tests/` (grep for `TimeProvider|FakeTimeProvider|Clock` found nothing). The fixed-window limiters use real wall-clock time; existing 429 tests only assert the rejection within one window, never window-reset/recovery behavior. A Phase 3 test that wants to show the ceiling compounding across *multiple* one-minute windows (the "every subsequent minute" part of the ceiling above) would either need to run long enough in real time to cross a window boundary, or introduce a `TimeProvider` seam into `AddRateLimiter`/`SessionCookieGate` (none currently wired) — a design decision for `/10x-plan`, not settled by this research.

## Code References

- `api/Program.cs:36-39` — comment: authorization runs before the `generate` limiter, so its `"anonymous"` partition fallback is unreachable.
- `api/Program.cs:40-41` — comment: `RemoteIpAddress` is forwarded via `X-Forwarded-For` behind App Service; `null` in the in-process test host.
- `api/Program.cs:44-50` — shared 429 rejection handling, `Retry-After` header computation.
- `api/Program.cs:51-58` — `generate` policy: 10/min, per account (`ClaimTypes.NameIdentifier`).
- `api/Program.cs:60-67` — `auth` policy: 10/min, per client IP.
- `api/Program.cs:89-101` — Identity options: `RequireUniqueEmail=true`, password length-only rule, lockout (5 attempts / 5 min).
- `api/Program.cs:184-189` — pipeline order: `UseSessionCookieGate()` → `UseAuthentication()` → ... → `UseRateLimiter()`.
- `api/Auth/AuthEndpoints.cs:12,15` — email max length (256) and app-level password max length (128) constants.
- `api/Auth/AuthEndpoints.cs:17-18` — comment: register and login share one per-IP bucket.
- `api/Auth/AuthEndpoints.cs:23,27` — `.RequireRateLimiting(AuthRateLimitPolicy)` on register and login.
- `api/Auth/AuthEndpoints.cs:30-35` — logout and `/me` carry no rate limit.
- `api/Auth/AuthEndpoints.cs:45-68` — register handler: validation, `CreateAsync`, immediate `SignInAsync`.
- `api/Auth/AuthEndpoints.cs:84-90` — login handler: `FindByEmailAsync`, `PasswordSignInAsync`.
- `api/Auth/SessionCookieGate.cs:7-15,18,24-32,50-55` — the separate 60/min-per-IP cookie-read gate (not part of this risk).
- `api/Maps/MapEndpoints.cs:25-26` — `generate` route: `RequireAuthorization()` then `.RequireRateLimiting(GenerateRateLimitPolicy)`.
- `api.Tests/MapEndpointTests.cs:13-14,72-92,98` — existing per-account `generate` 429 test and its isolation proof; anonymous-spends-no-permit test.
- `api.Tests/AuthEndpointTests.cs:11-12,247-276,334-358,385-402` — existing per-IP `auth` 429 test; Data Protection key-ring pin; `SessionCookieGate` 429 test (separate limiter).
- `api.Tests/Infrastructure/ApiFactory.cs:13,31-39,51-60` — test host setup; `CreateLoggedInClientAsync`/`NewEmail` multi-account helper.
- `api/Migrations/20260923154324_InitialAccountStore.cs:200-210` — unique filtered index on `NormalizedUserName`; non-unique index on `NormalizedEmail`.

## Architecture Insights

- Rate limiting is implemented entirely with ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` (named `FixedWindowRateLimiter` policies), not a third-party or distributed limiter — consistent with a single-instance F1/B1 App Service deployment with no Redis or similar shared store. This means any Phase 3 fix (if one is needed) has to work with in-process, per-instance counters; a true cross-request-pattern global cap is still expressible as an additional named `FixedWindowRateLimiter`/`GlobalLimiter` policy in the same style.
- The codebase already treats "per-IP" and "per-account" as orthogonal partition dimensions on two different endpoints (auth vs. generate). The gap is structural: nothing partitions on neither dimension (a true global/system-wide counter), which is exactly what's missing to bound total accounts or total generate throughput.
- The test-plan's own anti-pattern guidance (`test-plan.md:70`) explicitly rules out CAPTCHA and email verification as acceptable fixes, which — combined with the finding that no global limiter exists — points toward a global/aggregate rate-limiting policy (e.g., a system-wide cap on registrations/day or generates/day) as the architecturally consistent fix, not a new verification layer. This is a planning decision, not asserted here as the chosen fix.

## Historical Context (from prior changes)

- `context/changes/testing-seed-determinism/change.md` — Phase 2's impl review fixed a CRITICAL cross-run harness bug (wrong build configuration) and established the "each test builds its own factory" isolation pattern that Phase 3's multi-account test should also follow, per the `api.Tests` comments cited above.
- `context/foundation/lessons.md` ("API tests control their own configuration and never share counted state") — directly applicable: any new Phase 3 test must give itself a fresh `ApiFactory`/database rather than sharing counted state with other tests.
- `context/foundation/infrastructure.md:165` (Risk Register) — records the `generate`/`auth` limits as already "in place" mitigation for the CPU/bandwidth-quota risk, but does not itself compute the account-multiplication ceiling; that gap is this research's main finding.

## Related Research

- None under `context/changes/**/research.md` or `context/archive/**/research.md` address rate-limiting ceilings directly; the closest prior work is the account-store-foundation and dm-email-login changes (referenced in `infrastructure.md`'s risk register) that built the `auth`/`generate` policies and `SessionCookieGate` this research describes, but their own research/plan artifacts were not re-read in this pass (out of scope: this research targets the current code, which already reflects their outcome).

## Open Questions

1. **What is the intended fix shape?** The test-plan asks Phase 3 to "turn the per-account limit into a measured, tested ceiling" but rules out CAPTCHA/email-verification. Whether the fix is a new global limiter (on register, on generate, or both), a cap on total active accounts, or simply a test that documents the current unbounded ceiling as an accepted risk (deferred to Phase 4's cost-budget gate) is a product decision for `/10x-plan`, not settled here.
2. **Is the per-IP `auth` bucket actually hard to bypass, or free to rotate?** `api/AGENTS.md` records the `X-Forwarded-For`/App-Service forwarding setup as "verified 2026-09-30," but this research pass did not re-verify whether Azure App Service's front end trusts only its own hop (making IP rotation require real distinct source IPs) or whether a client-supplied `X-Forwarded-For` could be forged for free. This materially changes how expensive "many distinct IPs" is for a real attacker, though it doesn't change the finding that the *application code* places no ceiling on account count.
3. **Test infrastructure**: a new helper to simulate distinct source IPs (e.g. a `DelegatingHandler` injecting `X-Forwarded-For`, with forwarded-headers processing enabled in the `Testing` environment) does not exist and will need to be added to exercise the per-IP side of this scenario at all; today every in-process test client collapses to one `"unknown"` IP bucket.
4. **Multi-window behavior**: proving the ceiling "compounds every subsequent minute" needs either a real-time test that spans more than one 60-second window, or a new `TimeProvider` seam in `AddRateLimiter`/`SessionCookieGate` (neither exists today). Which approach to take is a planning/implementation decision.
