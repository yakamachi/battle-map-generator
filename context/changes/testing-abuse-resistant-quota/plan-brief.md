# Abuse-Resistant Quota — Plan Brief

> Full plan: `context/changes/testing-abuse-resistant-quota/plan.md`
> Research: `context/changes/testing-abuse-resistant-quota/research.md`

## What & Why

Risk #3 of the test rollout: a scripted client can bypass the per-account `generate`
limit by registering unlimited accounts, since nothing bounds total accounts or
aggregate throughput — the real ceiling today is `(accounts) × 10/min`, not a fixed
number. This plan adds a system-wide ceiling on top of the existing per-account/per-IP
limits and proves it holds no matter how many accounts or source IPs a scripted
attacker uses.

## Starting Point

`generate` is limited to 10/min per account; `auth` (register+login) to 10/min per
client IP — both independent `FixedWindowRateLimiter` policies in `api/Program.cs`.
Each account's generate budget persists forever once created, and nothing caps how
many accounts can exist, so cumulative throughput grows without bound as more
accounts accumulate.

## Desired End State

Both `generate` and `auth` enforce a new 50/min system-wide ceiling in addition to
their per-account/per-IP one. A new integration test drives a simulated multi-account,
multi-IP burst and shows aggregate successful calls on each endpoint cannot exceed 50/
min, regardless of account or IP count.

## Key Decisions Made

| Decision | Choice | Why | Source |
| --- | --- | --- | --- |
| Which endpoints get the new cap | Both `generate` and `auth` | Registration itself costs CPU/DB writes, not just generate — closing only one half leaves the other exploitable | Plan (interview) |
| New threshold | 50/min each (5× the existing per-account/per-IP budget) | Keeps the F1 CPU/bandwidth budget conservative until Phase 4 measures real per-generation cost | Plan (interview) |
| Legitimate-user impact | Same 429 + `Retry-After` as today, no distinguishing response | Zero new response contract, keeps this change entirely inside `api/` | Plan (interview) |
| Test depth | Full scripted-attacker shape: simulated multi-IP registration, then aggregate generate | Directly disproves the test-plan's "(accounts) × 10" framing, not just a narrower proxy | Plan (interview) |
| Mechanism | Chain the existing limiter with one shared global limiter via `RateLimiter.CreateChained` in the partition factory | Native .NET primitive, no new dependency; `options.GlobalLimiter` was rejected because it would also throttle health checks | Plan |
| `X-Forwarded-For` trust/spoofing (research's open question #2) | Left untouched | A true global cap bounds throughput regardless of how many IPs an attacker can fake, so the question stops mattering for this risk | Plan |
| Cross-window compounding (research's open question #4) | No `TimeProvider` seam added | Moot once a global cap exists — it reapplies every window regardless of account count | Plan |

## Scope

**In scope:**
- A new 50/min system-wide rate-limit ceiling chained onto both `generate` and `auth`.
- A test-infrastructure seam to simulate distinct source IPs in `api.Tests`.
- A new integration test proving the ceiling holds under a simulated multi-account,
  multi-IP burst.
- Doc updates: `api/AGENTS.md` and `context/foundation/infrastructure.md`'s risk
  register.

**Out of scope:**
- CAPTCHA, email verification (explicitly ruled out by the test-plan).
- Measuring real CPU/bandwidth cost per generation or tuning the 50/min figure against
  real F1 numbers (Phase 4's job).
- Any `web/` change — the rejection shape is unchanged.
- Hardening `X-Forwarded-For` trust against spoofing.
- A `TimeProvider` seam for cross-window behavior.

## Architecture / Approach

Two named rate-limit policies (`generate`, `auth`) already exist in `api/Program.cs`,
each a single `FixedWindowRateLimiter` partitioned by account or client IP. This plan
chains each one with a second `FixedWindowRateLimiter` partitioned on a single
constant key — effectively one shared bucket per policy, independent of how many
accounts or IPs exist — using `RateLimiter.CreateChained(...)` inside each policy's
partition factory. The per-partition limiter is evaluated first, so today's behavior
is unchanged whenever the global bucket isn't the bottleneck. A request blocked only
by the global ceiling still spends its per-partition permit for that minute, because
fixed-window limiters don't give permits back mid-window; this is an accepted
trade-off (see plan.md's implementation note).

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Global ceiling on generate + register | The chained 50/min global limiter on both policies, named constants, doc updates | Exact `AddPolicy` overload for a pre-built chained limiter needs confirming against the installed .NET 10 API |
| 2. Multi-IP test seam + the abuse-resistant quota test | A simulated-source-IP test helper and the integration test proving the ceiling | Forwarded-headers handling may not activate in the in-process `Testing` host without an explicit `UseForwardedHeaders()` call |

**Prerequisites:** None beyond the current `main`-tracked state of `api/`.
**Estimated effort:** ~1 session across 2 phases — a config change plus one new test
file.

## Open Risks & Assumptions

- The 50/min figure is a provisional, conservative multiple (5×) of the existing
  per-account/per-IP budget, not derived from measured CPU cost — Phase 4
  ("Generation cost budget") may revise it once real F1 numbers exist.
- Whether `ApiFactory`'s in-memory `ASPNETCORE_FORWARDEDHEADERS_ENABLED` config
  activates forwarded-header processing in-process is unverified; Phase 2 may need an
  explicit `app.UseForwardedHeaders(...)` call in `Program.cs` as a fallback (see the
  full plan's Critical Implementation Details).

## Success Criteria (Summary)

- No number of accounts or simulated source IPs can push aggregate `generate` or
  `auth` throughput past 50/min on one app instance — proven by test, not just
  asserted.
- Every existing per-account/per-IP rate-limit test still passes unmodified.
