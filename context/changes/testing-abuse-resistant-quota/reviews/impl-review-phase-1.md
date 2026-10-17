<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Abuse-Resistant Quota

- **Plan**: context/changes/testing-abuse-resistant-quota/plan.md
- **Scope**: Phase 1 of 2
- **Reviewed phases**: 1
- **Date**: 2026-10-16
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 2 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Success criteria re-run during review: `dotnet build api` 0 errors, no OpenAPI drift (only the unrelated CLAUDE.md is dirty); `dotnet test api.Tests` 162/162 passed. Manual 1.3 was confirmed by the user in-session.

## Findings

### F1 — Global cap lets a few clients lock everyone out, not recorded as residual risk

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:68-101, context/foundation/infrastructure.md:165
- **Detail**: The ceiling works, but it turns the bounded-cost problem into a cheap availability problem. 5 client IPs at their own 10/min fill the shared `auth` bucket, and then every login, including existing users', gets 429 for the rest of the minute, because register and login share one global bucket. Likewise 5 accounts fill the `generate` bucket for everyone. The plan accepted "same 429 as today", but neither the risk register nor `api/AGENTS.md` names this residual, unlike the SessionCookieGate row, which does record its own ("Residual: many source IPs can still wake the database…").
- **Fix A ⭐ Recommended**: Append a "Residual:" clause to the CPU/bandwidth risk row (and one sentence in api/AGENTS.md) saying that 5 clients/accounts can saturate each 50/min bucket and deny service to everyone for that minute, to be revisited with Phase 4's cost numbers.
  - Strength: Matches the register's existing "Residual:" convention; costs nothing and keeps the plan's decided design.
  - Tradeoff: The lockout stays possible; it's only documented.
  - Confidence: HIGH — it is plain arithmetic on the configured limits.
  - Blind spot: No real traffic data on how close legitimate peak usage gets to 50/min.
- **Fix B**: Give login its own global bucket (or exempt it), so a registration flood can't starve existing users' logins.
  - Strength: Protects the most valuable path (returning users) from a register flood.
  - Tradeoff: Reopens an interview decision ("both generate and auth", one shared policy), needs a policy split in Program.cs and AGENTS.md, and login still hashes a password, so it needs some cap anyway.
  - Confidence: MED — the design change is small but touches the auth policy all tests rely on.
  - Blind spot: Phase 2's register-ceiling test would need to change shape.
- **Decision**: FIXED (Fix A) — residual clause added to the infrastructure.md risk row and to both rate-limit bullets in api/AGENTS.md

### F2 — Plan text claims CreateChained returns the per-key permit; with FixedWindow it doesn't

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: context/changes/testing-abuse-resistant-quota/plan.md:82-83, :100; plan-brief.md:68-69
- **Detail**: The plan and brief state that the chain "correctly releases an already-acquired per-partition permit" when the global limiter refuses. The probe showed that disposing a FixedWindow lease doesn't return the permit mid-window, so a request refused only by the global cap still spends its per-account/per-IP permit. The code comment in Program.cs records the truth; the plan, which is the review ground truth and the input to Phase 2, still says the opposite. The plan also names `PartitionedRateLimiter.CreateChained`; the code uses `RateLimiter.CreateChained` inside the partition factory (a documented minor adaptation).
- **Fix**: Append an "Implementation note (Phase 1)" under Critical Implementation Details correcting both points, and fix the one sentence in plan-brief.md.
- **Decision**: FIXED — implementation note added to plan.md Critical Implementation Details; plan-brief.md architecture paragraph and Mechanism row corrected

### F3 — Progress SHA format differs from the plan's convention

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: context/changes/testing-abuse-resistant-quota/plan.md:286-291
- **Detail**: The rows end with ` (c5944fc)`; the Progress convention line and 186 rows across other changes use ` — c5944fc`.
- **Fix**: Rewrite the three rows to ` — c5944fc`.
- **Decision**: FIXED

### F4 — The rate-limiter comment block is much denser than its neighbours

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/Program.cs:42-57
- **Detail**: A 16-line comment narrates the probe ("Verified (dotnet run against the installed SDK, not assumed)") and repeats AGENTS.md's rationale. The surrounding comments in Program.cs are 2–4 lines and state only constraints.
- **Fix**: Trim it to ~5 lines: the chain plus shared global bucket, why it's built in the partition factory, and the FixedWindow no-give-back trade-off.
- **Decision**: FIXED — comment trimmed to 5 lines
