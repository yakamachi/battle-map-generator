# Abuse-Resistant Quota Implementation Plan

## Overview

Risk #3 (`context/foundation/test-plan.md:46`): a scripted client can mint unlimited
accounts and bypass the per-account `generate` limit, since nothing bounds the total
number of accounts or the system's aggregate throughput. This plan closes that gap
with a new system-wide rate-limit ceiling on both `generate` and `auth`
(register+login), then proves the ceiling holds under a simulated multi-account,
multi-IP burst.

## Current State Analysis

- `generate` is limited to 10/min **per account** (`ClaimTypes.NameIdentifier` claim),
  `auth` (register+login) to 10/min **per client IP** — both `FixedWindowRateLimiter`
  named policies, `api/Program.cs:51-67`.
- No `options.GlobalLimiter` and no other cap exists: no limit on total accounts, no
  CAPTCHA, no email confirmation (`api/Auth/AuthEndpoints.cs`, confirmed absent).
- Register signs the account in immediately (`AuthEndpoints.cs:62-68`), so no login
  call is even needed to mint a usable account.
- Steady-state generate throughput is bounded only by
  `(cumulative accounts ever registered) × 10/min`, and account creation is bounded
  only by `(elapsed minutes) × 10 × (distinct client IPs)` — both unbounded
  (`research.md`, "The ceiling, computed").
- `SessionCookieGate.cs:24-30` already shows this project's own pattern for building a
  `PartitionedRateLimiter<HttpContext>` by hand outside the simple `AddPolicy` lambda
  form — the same primitive this plan reuses to compose two limiters into one policy.
- Every in-process test client collapses to the same `"unknown"` IP
  (`AuthEndpointTests.cs:11-12`, confirmed by comment); there is no existing seam to
  simulate distinct source IPs in `api.Tests`.

### Key Discoveries:

- `api/Program.cs:36-39,51-67` — the two existing named policies and the comment
  explaining why the `"anonymous"`/`"unknown"` partition fallbacks are unreachable.
- `api/Auth/SessionCookieGate.cs:24-30` — the project's existing pattern for a
  hand-built `PartitionedRateLimiter<HttpContext>`.
- `api.Tests/Infrastructure/ApiFactory.cs:51-58` — `CreateLoggedInClientAsync`, the
  existing multi-account helper this plan's new test reuses.
- `context/foundation/lessons.md` ("API tests control their own configuration and
  never share counted state") — every new test in this plan gets its own fresh
  `ApiFactory`.

## Desired End State

Both `generate` and `auth` enforce a new 50/min **system-wide** ceiling in addition to
their existing per-account/per-IP ceiling. No number of accounts or simulated source
IPs can push aggregate successful calls past 50/min on either endpoint — verified by
a new integration test that drives exactly that scenario.

### Verification

- `dotnet test api.Tests` passes in full, including the two new tests described in
  Phase 2 and every existing rate-limit test unmodified.

## What We're NOT Doing

- Not adding CAPTCHA or email verification — the test-plan's anti-pattern guidance
  rules both out as non-goals (`test-plan.md:70`).
- Not measuring real CPU/bandwidth cost per generation or tuning the 50/min figure
  against actual F1 numbers — that is Phase 4 ("Generation cost budget")'s job.
- Not distinguishing the new global-cap rejection from the existing per-account/per-IP
  one in the response: same 429 + `Retry-After` shape as today, so this change stays
  entirely inside `api/` with no `web/` changes.
- Not changing how `X-Forwarded-For` is trusted in production or hardening against
  IP-spoofing (research's open question #2). Once a true global cap exists, spoofing
  more apparent IPs only buys more accounts faster — aggregate throughput is still
  capped regardless of account or IP count — so that question doesn't need resolving
  for this risk.
- Not introducing a `TimeProvider` seam or testing cross-rate-limit-window compounding
  behavior (research's open question #4). It's moot once a global cap exists: the cap
  re-applies fresh every window regardless of how many accounts exist, so there is no
  "every subsequent minute" compounding left to prove.
- Not persisting rate-limit counters outside process memory (no Redis/shared store),
  consistent with the existing single-instance F1/B1 deployment.

## Implementation Approach

Chain the existing per-partition limiter with a new limiter keyed on a single
constant partition (so it is effectively unpartitioned — one shared bucket for the
whole app instance), using .NET's built-in
`PartitionedRateLimiter.CreateChained(...)`, which requires every chained limiter to
grant a lease before the request proceeds, and correctly releases an already-acquired
lease if a later one in the chain fails. Both named policies (`generate`, `auth`) gain
this treatment; endpoint registration (`.RequireRateLimiting(...)` in
`AuthEndpoints.cs`/`MapEndpoints.cs`) does not change — only `Program.cs` does.

`options.GlobalLimiter` was considered and rejected: it applies to every request that
reaches the rate-limiting middleware, including the mapped health-check endpoints
(`/api/health/live`, `/api/health/ready`), which must never be throttled by traffic
aimed at `generate`/`auth`. Two policy-scoped chained limiters avoid that.

## Critical Implementation Details

**Chain order matters for correct permit accounting.** Put the existing
per-account/per-IP limiter first in the chain, the new global limiter second. This
way, a request already refused by its own per-account/per-IP budget never touches the
global bucket (today's behavior for that case is unchanged), and a request that
passes its own budget but is blocked only by the global ceiling has its per-account/
per-IP permit correctly released by `CreateChained` rather than spent for nothing.

> **Implementation note (Phase 1, from impl-review F2):** two statements above proved
> inaccurate. (1) With `FixedWindowRateLimiter`, `CreateChained` does dispose the
> earlier lease when the global limiter refuses, but disposing a fixed-window lease
> does not give the permit back mid-window. A request refused only by the global
> ceiling still spends one of its own per-account/per-IP permits for that minute.
> This was accepted as a trade-off: it only matters once the shared bucket is already
> full. (2) No `AddPolicy` overload takes a pre-built `PartitionedRateLimiter`, so the
> code uses the non-partitioned `RateLimiter.CreateChained(perKey, global)` inside
> each policy's partition factory, with one shared global `FixedWindowRateLimiter`
> per policy captured by closure (`api/Program.cs`). The chain order is as described
> above.

**Forwarded-header handling for the test seam needs verification before Phase 2's
test can work.** Production relies on the `ASPNETCORE_FORWARDEDHEADERS_ENABLED` app
setting (`api/Program.cs:40-41`, `api/AGENTS.md`) to make
`HttpContext.Connection.RemoteIpAddress` reflect `X-Forwarded-For` — but `Program.cs`
never calls `app.UseForwardedHeaders(...)` explicitly, so it's unverified whether
setting that same config key through `ApiFactory`'s in-memory configuration
(`ApiFactory.cs:35-39`) activates the same behavior in-process. Confirm this with a
throwaway test first (per `lessons.md`, "Triage a .NET native swap against what it
actually does today"). If it does not activate, add an explicit
`app.UseForwardedHeaders(new ForwardedHeadersOptions { ForwardedHeaders =
ForwardedHeaders.XForwardedFor })` call in `Program.cs` (applies uniformly in every
environment, including production) as the fallback, rather than relying on the
undocumented implicit mechanism only for production.

## Phase 1: Global ceiling on generate + register

### Overview

Add the system-wide 50/min ceiling to both named rate-limit policies and document it.

### Changes Required:

#### 1. Chained global limiter

**File**: `api/Program.cs`

**Intent**: Close the unbounded-account loophole by requiring both the existing
per-partition limiter and a new constant-key global limiter to grant a lease, for
both `generate` and `auth`.

**Contract**: `options.AddPolicy(MapEndpoints.GenerateRateLimitPolicy, ...)` and
`options.AddPolicy(AuthEndpoints.AuthRateLimitPolicy, ...)` become chained limiters
via `PartitionedRateLimiter.CreateChained(perPartitionLimiter, globalLimiter)` (order
per "Critical Implementation Details" above). The global limiter for each policy is a
`FixedWindowRateLimiter` partitioned on a single constant key (e.g. `"all"`),
`PermitLimit = 50`, `Window = TimeSpan.FromMinutes(1)`, `QueueLimit = 0` — same shape
as the existing limiters. `options.OnRejected` already reads `Retry-After` from lease
metadata generically, so it needs no changes. Confirm the exact `AddPolicy` overload
that accepts a pre-built `PartitionedRateLimiter<HttpContext>` against the installed
.NET 10 `Microsoft.AspNetCore.RateLimiting` API during implementation (not pinned
here — see `SessionCookieGate.cs:24-30` for this project's existing hand-built
`PartitionedRateLimiter<HttpContext>` pattern if a named-policy lambda can't express
the chain directly).

#### 2. Named constants for the new thresholds

**File**: `api/Maps/MapEndpoints.cs`, `api/Auth/AuthEndpoints.cs`

**Intent**: Give the new global thresholds a named, documented constant next to the
existing policy name, the same way `SessionCookieGate.RequestsPerMinute` is a named
constant tests and docs reference — so Phase 2's test doesn't hardcode `50` twice.

**Contract**: `MapEndpoints.GlobalGenerateLimit = 50` and
`AuthEndpoints.GlobalAuthLimit = 50`.

#### 3. Documentation

**File**: `api/AGENTS.md`

**Intent**: Record the new mitigation next to the numbers it changes, so the next
agent doesn't rediscover this gap.

**Contract**: Update the generate-limit bullet ("Authentication" section's
register/login bullet and the "Map generation contract" section's generate bullet) to
state the added 50/min system-wide cap and why it exists (bounds aggregate throughput
regardless of account or IP count).

**File**: `context/foundation/infrastructure.md`

**Intent**: Reflect the closed loophole in the risk register.

**Contract**: Append to the existing "In place (S-01):" bullet in the "CPU or
bandwidth quota exhausted" risk row.

### Success Criteria:

#### Automated Verification:

- `dotnet test api.Tests` passes with no regressions, in particular
  `MapEndpointTests.The_eleventh_generate_within_a_minute_returns_429_with_retry_after`
  and
  `AuthEndpointTests.The_eleventh_register_or_login_within_a_minute_returns_429_with_retry_after`.
- `dotnet build` succeeds (OpenAPI document regenerates with no unexpected diff — this
  change touches no request/response shape).

#### Manual Verification:

- Run the app locally, register, log in, and call `POST /api/maps/generate` once;
  confirm it still returns 200 — the new chained global limiter hasn't broken the
  ordinary single-account path.

---

## Phase 2: Multi-IP test seam + the abuse-resistant quota test

### Overview

Build the missing test-infrastructure seam for simulating distinct source IPs, then
write the integration test that proves the new ceiling holds regardless of account or
IP count — directly disproving the "(accounts per minute) × 10" ceiling from
`research.md`.

### Changes Required:

#### 1. Simulated source-IP seam

**File**: `api.Tests/Infrastructure/ApiFactory.cs` (or a sibling file in the same
directory)

**Intent**: Let a test simulate requests arriving from many distinct client IPs
against one shared app instance, the way App Service derives the client IP from
`X-Forwarded-For` in production.

**Contract**: A `DelegatingHandler` that sets the `X-Forwarded-For` header to a given
fake address on every outgoing request, plus a factory method (e.g.
`ApiFactory.CreateClientFromIp(string simulatedIp)`) built on
`WebApplicationFactory<Program>.CreateDefaultClient(handler)` so tests can request a
client tagged with a chosen IP. Depends on the forwarded-headers verification in
"Critical Implementation Details" above landing first.

#### 2. The abuse-resistant quota test

**File**: `api.Tests/AbuseResistantQuotaTests.cs` (new file — crosses both `generate`
and `auth`, so it doesn't belong in either existing endpoint test file)

**Intent**: Prove the ceiling is now a fixed number, not a function of account or IP
count.

**Contract**: Two tests against one shared `ApiFactory` each (per `lessons.md`
isolation rule — each test still gets its own factory):
- **Register ceiling**: drive registration attempts from 6 simulated IPs, 10 attempts
  each (60 total — each IP individually stays at or under its own 10/min `auth`
  budget). Assert exactly `AuthEndpoints.GlobalAuthLimit` (50) succeed and the rest
  get 429 — in particular, that the 6th IP's own registrations fail globally despite
  that IP having spent none of its own per-IP budget, proving the shared ceiling, not
  any single IP's budget, is what stops it.
- **Generate ceiling**: register 10 accounts (safely under both the per-IP and new
  global register budgets, e.g. 2 simulated IPs × 5 accounts), log each in, then have
  the 10 accounts collectively attempt 10 generate calls each (100 total — each
  account individually stays at or under its own 10/min `generate` budget). Assert
  exactly `MapEndpoints.GlobalGenerateLimit` (50) succeed across all 10 accounts
  combined and the rest get 429, regardless of which account they came from.

### Success Criteria:

#### Automated Verification:

- `dotnet test api.Tests --filter AbuseResistantQuotaTests` passes.
- `dotnet test api.Tests` (full suite) passes with no regressions.

---

## Testing Strategy

### Integration Tests:

- The two scenarios in Phase 2 are the only new tests this plan adds; both are
  integration tests against the real rate-limiter configuration (the cheapest layer
  that can prove a cross-cutting middleware behavior, consistent with
  `test-plan.md`'s "cost × signal" principle).

### Manual Testing Steps:

1. After Phase 1, register and generate once locally to confirm the ordinary path is
   unaffected.

## References

- Research: `context/changes/testing-abuse-resistant-quota/research.md`
- Rate-limiter configuration: `api/Program.cs:42-68`
- Existing hand-built `PartitionedRateLimiter<HttpContext>` pattern:
  `api/Auth/SessionCookieGate.cs:24-30`
- Existing per-policy 429 tests: `api.Tests/MapEndpointTests.cs:72-92`,
  `api.Tests/AuthEndpointTests.cs:247-276`
- Multi-account test helper: `api.Tests/Infrastructure/ApiFactory.cs:51-58`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles.

### Phase 1: Global ceiling on generate + register

#### Automated

- [x] 1.1 dotnet test api.Tests passes with no regressions — b5f7fcf
- [x] 1.2 dotnet build succeeds with no unexpected OpenAPI diff — b5f7fcf

#### Manual

- [x] 1.3 A single logged-in generate call still returns 200 locally after the change — b5f7fcf

### Phase 2: Multi-IP test seam + the abuse-resistant quota test

#### Automated

- [x] 2.1 dotnet test api.Tests --filter AbuseResistantQuotaTests passes — b5f7fcf
- [x] 2.2 dotnet test api.Tests (full suite) passes with no regressions — b5f7fcf
