<!-- PLAN-REVIEW-REPORT -->
# Plan Review: First Map Download Implementation Plan

- **Plan**: context/changes/first-map-download/plan.md
- **Mode**: Deep
- **Date**: 2026-09-28
- **Verdict**: REVISE → SOUND after triage
- **Findings**: 0 critical, 4 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | PASS |
| Lean Execution | PASS |
| Architectural Fitness | PASS |
| Blind Spots | WARNING |
| Plan Completeness | WARNING |

## Grounding
9/9 paths ✓, 6/6 symbols ✓ (AddOpenApi :15, MapOpenApi dev-only :57-60, /api 404 guard :103, Test API step deploy.yml:43-44, ApiFactory, launchSettings :5108), brief↔plan ✓, Progress↔Phase ✓ (27 items)

## Findings

### F1 — Rate-limit partition key is null in tests and limiter state is shared

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 2 §4, §6
- **Detail**: TestServer leaves `RemoteIpAddress` null, and a null partition key throws. Limiter counters are per factory, so shared factories make the 429 test depend on test order (lesson "never share counted state").
- **Fix**: Partition key `RemoteIpAddress?.ToString() ?? "unknown"`; the 429 test uses its own ApiFactory; no other factory makes more than 10 calls.
- **Decision**: FIXED

### F2 — One pixel hash for both browsers is fragile and has no update path

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 3 §3, §4, §6; Critical Implementation Details
- **Detail**: Colour management of PNG metadata chunks, alpha rounding of layered pieces, and GPU vs software canvas can all differ. The fallback lived only in the brief, and there was no baseline update command.
- **Fix A ⭐ Recommended**: Per-browser baselines, `UPDATE_BASELINES=1`, atlas without colour chunks, `createImageBitmap` with no colour/alpha conversion; cross-browser equality only logged.
- **Fix B**: One shared hash plus normalisation and an update command.
- **Decision**: FIXED (Fix A)

### F3 — Branch protection changes the workflow for docs and skill commits

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Blind Spots
- **Location**: Phase 1 §3, §4
- **Detail**: With `enforce_admins`, chore, archive and roadmap commits can no longer go straight to main. Path filters on ci.yml would block docs-only PRs forever. The pending planning files must land before protection is switched on.
- **Fix**: Add a prerequisite (commit the planning files first), and document "every change goes through a PR; no path filters".
- **Decision**: FIXED

### F4 — Vitest can't read ../fixtures, and the config must avoid the React Router plugin

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 3 §1, §6
- **Detail**: `server.fs.allow` defaults to `web/`, and reusing vite.config.ts brings in `reactRouter()`.
- **Fix**: Standalone vitest.config.ts, `server.fs.allow: [".."]`, fixtures loaded with `import.meta.glob`.
- **Decision**: FIXED

### F5 — deploy.yml workflow_dispatch can deploy an untested branch

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1 §2
- **Fix**: `if: github.ref == 'refs/heads/main'` on the build job.
- **Decision**: FIXED

### F6 — Rate limit goes live before forwarded headers are set

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 2 manual steps
- **Fix**: Set the app setting before the merge; swap Progress items 2.4 and 2.5.
- **Decision**: FIXED

### F7 — Rendering rule docs contradict the one-canvas decision until Phase 4

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 3 / Phase 4 §4
- **Fix**: New Phase 3 §5a updates web/AGENTS.md:13-14 and infrastructure.md:32.
- **Decision**: FIXED
