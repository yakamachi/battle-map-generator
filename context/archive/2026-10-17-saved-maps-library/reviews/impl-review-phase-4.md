<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Phase 4 of 5
- **Reviewed phases**: 4
- **Date**: 2026-10-18
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Notes:
- **Plan drift:** every planned item in commit aeeb753 is a MATCH.
  - The five steps run in order, with exactly one generate per browser, and the spec deletes what it creates.
  - The commit makes no `web/` or `api/` changes.
  - The disclosed extras are the server-side delete proof (a reload, then the old URL shows "Map not found") and the measured cookie-gate count.
  - Arithmetic: cookie gate 1 + 6 + 6 + 4 + 24 = 41 of 60 per minute; generate 4 + 2 + 2 = 8 of 10.
- **Verified clean:**
  - Radix aria-hiding is handled: each dialog closes before the list is queried.
  - The absence check after reload waits for the list to render.
  - Running in parallel with Chromium and Firefox on one account is safe: titles carry the project name, every locator uses `exact: true`, and nothing counts rows.
  - The seed is compared against the exact value read on home, and line 29 is not tautological.
  - There are no fixed sleeps.
  - The spec follows the patterns of `map.spec.ts` and `parameters.spec.ts`.
- **Success criteria:**
  - 4.1 typecheck clean.
  - 4.2 full run 11/11 in Chromium and Firefox.
  - 4.3 break-check: Delete changed from `ExecuteDeleteAsync` to `CountAsync` (204 without deleting) turned both browsers red at `library.spec.ts:68` (the absence check after reload); the API file was restored.
  - 4.4 after the green run, the only rows for `e2e@example.com` were the two the break-check left; both were deleted by exact title, and the count is now 0.

## Findings

### F1 — Three dialog headings are matched by substring

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: e2e/tests/library.spec.ts:25, 58, 71
- **Detail**: The "Map saved", "Delete map?" and "Map not found" headings are located without `exact: true`, while every row locator and `auth.spec.ts` use exact matching. Nothing is wrong today, but a later heading such as "Map saved to …" would still match.
- **Fix**: Add `exact: true` to the three heading locators.
- **Decision**: FIXED: exact: true on the "Map saved", "Delete map?" and "Map not found" headings; e2e typecheck clean.

### F2 — Repeated interrupted local runs creep toward the 100-map cap

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: e2e/tests/library.spec.ts:10; e2e/AGENTS.md
- **Detail**: An interrupted run leaves at most one row per browser, as the plan accepts. The local compose database persists across runs, while CI starts fresh each time. Enough interrupted local runs would fill the e2e account's 100-map cap, and Save would then fail with 400 for a reason that has nothing to do with the change.
- **Fix**: One sentence in `e2e/AGENTS.md`: if `library.spec.ts` fails at Save with the 100-map message, delete the e2e account's leftover maps or reset with `docker compose down -v`.
- **Decision**: FIXED: e2e/AGENTS.md says the local compose database keeps interrupted runs' rows and how to clear them if Save hits the 100-map cap.

### F3 — Two stale or overlong doc lines

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: e2e/playwright.config.ts:13; context/changes/saved-maps-library/plan.md:37
- **Detail**: The edited comment line in `playwright.config.ts` runs past the wrapping of the lines around it. The plan's Key Discoveries still says 6 of the 10 generate permits per run, and Phase 4 made it 8.
- **Fix**: Reflow the config comment; update plan.md:37 to "8 of the 10 (6 before this change)".
- **Decision**: FIXED: the playwright.config.ts comment is rewrapped to its neighbours' width; plan.md Key Discoveries says 8 of the 10 (6 before this change).
