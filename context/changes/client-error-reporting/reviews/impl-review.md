<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Report swallowed fetch errors in the client API wrapper

- **Plan**: context/changes/client-error-reporting/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1
- **Date**: 2026-10-11
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Future tests must repeat the stub-then-reimport sequence

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: `web/app/api/client.test.ts`, `web/app/api/auth.test.ts`
- **Detail**: Each test independently does `vi.stubGlobal("fetch", ...)` → `vi.spyOn(console, "error")` → `vi.resetModules()` → dynamic `import()`, because `openapi-fetch`'s `createClient()` captures `globalThis.fetch` once at module-eval time. If a future test in either file skips this sequence (e.g. relies on a shared `beforeEach` that doesn't reset modules), it will silently observe whichever `fetch` stub a prior test last installed. The in-file comments already explain why, so this is a note to keep that explanation prominent, not a current defect.
- **Fix**: No code change needed now; keep the explanatory comment next to the pattern as these test files grow.
- **Decision**: PENDING

### F2 — console.error spy still prints to the real console during test runs

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: `web/app/api/client.test.ts`, `web/app/api/auth.test.ts`
- **Detail**: `vi.spyOn(console, "error")` has no `.mockImplementation`, so each matched call still prints to the real console during `npm test`. Harmless (it's actually confirming the reporting call fires), just noisy output.
- **Fix**: Optionally add `.mockImplementation(() => {})` to the spy to silence console output during test runs.
- **Decision**: PENDING

## Supporting evidence

- **Plan drift**: all 4 planned changes (reportError helper, generateMap wiring, register/login/logout wiring, unit tests) verified MATCH against the plan's Intent/Contract, with exact file:line citations. No MISSING items. No EXTRA items — the generic `{kind:"http"}` unexpected-status branches and `web/app/root.tsx` were confirmed untouched, per the plan's "What We're NOT Doing".
- **Safety**: no PII/secrets flow into `reportError` at any of the 4 call sites — only a static route label and the caught exception. `reportError` cannot throw (label is always a literal; error is passed as a separate arg, not interpolated).
- **Pattern compliance**: the 4 catch-block edits are uniform (`catch {` → `catch (error) {` + one `reportError(...)` call); new test files match `web/app/map/tileset.test.ts`'s existing Vitest conventions.
- **Success criteria**: `npm run typecheck` clean; `npm test` 8/8 files, 78/78 tests pass (both Chromium and Firefox), independently re-run twice during this review in addition to the subagent's own run. Manual devtools-offline check confirmed by the user ("it's working").
- **Scope note (not a finding)**: `CLAUDE.md` (M3L5 skill sync) and `context/audits/observability/2026-10-11_login-generate.md` were bundled into the phase 1 commit at the user's explicit choice ("Stage all") during the dirty-path prompt, not introduced by the implementation itself.
