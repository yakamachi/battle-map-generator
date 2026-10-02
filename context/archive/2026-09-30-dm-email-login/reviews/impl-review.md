<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: DM Email Login (S-04) Implementation Plan

- **Plan**: context/changes/dm-email-login/plan.md
- **Scope**: Full plan (4 of 4 phases)
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-02
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 0 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Context

This review covers the squash-merged commit `d6adc22` (PR #14) against `context/changes/dm-email-login/plan.md`'s four phases. All four phases already have individual phase reviews (`reviews/impl-review-phase-1.md` through `-4.md`); this is the comprehensive closeout sweep across all of them together, plus the state left by the branch's own merge-with-main commit (`da4764a`), which folded in S-03 (`encounter-parameters`, merged first).

Two sub-agents did the legwork: one traced every plan-listed file against its actual contract and hunted for undocumented scope creep; one re-screened the auth/session code for security, reliability and pattern issues against `context/foundation/lessons.md`'s accepted rules. Findings below are the union, deduplicated.

## Findings

### F1 — Stale cross-reference in web/AGENTS.md

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/AGENTS.md (## UI section)
- **Detail**: The doc still reads: "The generate request with encounter parameters is `app/api/maps.ts` (`generateMapWith`). It lives apart from `app/api/client.ts` only while S-04 changes that file; merge them once S-04 has landed." That merge already happened — `web/app/api/maps.ts` was deleted and `generateMap` now lives in `client.ts` (done in the branch's `da4764a` merge-with-main commit, confirmed by `git show` — the file no longer exists on disk and `home.tsx` now imports `generateMap` from `~/api/client`). The sentence is dangling and will mislead the next agent into looking for a file that isn't there.
- **Fix**: Delete the stale paragraph (or replace it with one line noting the merge is done and auth calls vs. map calls split between `app/api/auth.ts` and `app/api/client.ts`).
- **Decision**: FIXED — removed the stale paragraph from web/AGENTS.md's ## UI section.

### F2 — No test pins a bounded failure time for login/register against an unreachable database

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality (Reliability)
- **Location**: api/Program.cs:67 (`options.UseSqlServer(sql => sql.EnableRetryOnFailure())`); api.Tests/AuthEndpointTests.cs
- **Detail**: `EnableRetryOnFailure()`'s default policy (6 attempts, exponential backoff up to ~30s) applies to every `AppDbContext` query, including register/login, which do need the database. The plan's "Performance Considerations" already accepts that a login during an Azure SQL auto-pause resume can wait roughly a minute (up to three), and the UI already degrades gracefully for it (`login.tsx`/`register.tsx` show a network/HTTP error). But unlike the anonymous, database-free paths — which `AuthEndpointTests.cs` pins to answer in under 5 seconds against an unreachable connection string — there is no equivalent test proving register/login actually fail (or succeed) within a bounded time rather than hanging past whatever the retry math computes. It's accepted behavior, not a bug, but it's currently undemonstrated by a test the way the rest of this change's DB-touch boundary is.
- **Fix A ⭐ Recommended**: Add one `AuthEndpointTests` case (unreachable-DB factory, same shape as the existing one for `me`/`logout`) asserting register and login each return within a fixed upper bound (e.g. under 10s, since retry/backoff is deterministic) rather than only asserting the database-free paths.
  - Strength: Matches the existing test pattern in this exact file; closes the asymmetry between the two kinds of DB-touching paths without changing any production code.
  - Tradeoff: The bound has to track whatever `EnableRetryOnFailure()`'s defaults compute, so it's a little brittle to a future tuning of retry options.
  - Confidence: MED — reasonable given the existing pattern, but the exact retry timing isn't something I measured directly.
  - Blind spot: Haven't verified EF Core's exact default backoff schedule for this provider/version to pick a safe bound.
- **Fix B**: Accept as-is — the plan already documents and the UI already handles the slow-DB case; leave it undemonstrated by tests since it's an operational characteristic, not a contract.
  - Strength: No extra work; nothing here is actually broken.
  - Tradeoff: The DB-touch boundary stays partially untested (database-free paths pinned, database-needing paths not), and a future change could silently make login hang much longer without any test catching it.
  - Confidence: MED — reasonable if the team is comfortable treating "slow but eventually correct" as out of test scope.
  - Blind spot: None significant.
- **Decision**: FIXED via Fix A — added `Register_and_login_fail_within_a_bounded_time_with_an_unreachable_database` to `api.Tests/AuthEndpointTests.cs`. Measured result: both fail in ~1s (a refused TCP connection isn't classified as a transient SQL error, so `EnableRetryOnFailure` doesn't retry it at all) — asserted at a generous 30s bound to stay non-flaky while still catching a real hang. Note: TestServer rethrows the unhandled exception to the caller instead of turning it into a 500 the way real Kestrel hosting does (no exception-handling middleware is registered), so the test pins the bounded time and a failure outcome, not a specific status code.

### F3 — home.tsx and the merge-with-main commit (mechanical, not a contract violation)

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: web/app/routes/home.tsx; branch commit `da4764a` ("merge: resolve conflicts with main (encounter-parameters, S-03)")
- **Detail**: The plan's "What We're NOT Doing" lists "No edits to ... `web/app/routes/home.tsx`" with no carve-out, and Phase 4's own success criteria checked `git diff --exit-code main -- web/app/routes/home.tsx` (true at the time, before S-03 had merged). After S-03 merged and this branch merged main into itself, folding `web/app/api/maps.ts` into `client.ts` required a one-line import/rename update in `home.tsx` (`generateMapWith` from `~/api/maps` → `generateMap` from `~/api/client`; no logic change). This is exactly what the plan's own "Migration Notes" anticipated ("reapply the client.ts edits from phase 4 around its changes"), and it was done transparently in a clearly-labeled merge commit — but it means the literal rule "no edits to home.tsx" wasn't kept to the letter, and nothing in the closeout record currently says so.
- **Fix**: No code change needed. Note in the closeout PR description (or change.md) that `home.tsx`'s only post-phase-4 diff is this mechanical merge-driven rename, so a future reader doesn't mistake Phase 4's "no diff" success criterion as still true against current `main`.
- **Decision**: FIXED — added a closeout note to `context/changes/dm-email-login/change.md` explaining the merge-driven `home.tsx` diff.

## Automated Verification (re-run during this review)

- ✅ `dotnet build api` — succeeds, OpenAPI document regenerates cleanly
- ✅ `git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts` — no drift
- ✅ `dotnet test api.Tests -c Release` — 156/156 passed
- ✅ `dotnet ef migrations has-pending-model-changes --project api` — no pending model changes
- ✅ `npm --prefix web run typecheck` — clean
- ✅ `npm --prefix web run ui:scan` — 12 files, no hardcoded design values
- ✅ `git diff 5376327..d6adc22 -- api/Maps/MapEndpoints.cs` — only `.RequireAuthorization()` + reworded comment, `Generate` handler untouched
- ✅ `git diff 5376327..d6adc22 -- e2e/tests/map.spec.ts` — zero diff
- Not re-run this pass (unchanged since phase 4's own green run, per Progress log): e2e in Chromium/Firefox, the visual gate in Docker. No code touching those paths has changed since.

## Manual Verification

Per Progress section, phase 1–4 manual items are all checked `[x]` except the three deploy-only checks (4.11–4.13), which the plan itself defers to the closeout pull request. Not rubber-stamped — each has a commit SHA and the automated evidence above is consistent with them.
