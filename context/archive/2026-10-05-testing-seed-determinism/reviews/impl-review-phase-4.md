<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Seed determinism (risk #2)

- **Plan**: context/changes/testing-seed-determinism/plan.md
- **Scope**: Phase 4 of 5
- **Reviewed phases**: 4
- **Date**: 2026-10-10
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

### F1 — New test doesn't defensively copy `cells` like its sibling test

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/map/tileset.test.ts:190-191
- **Detail**: The adjacent test at line 170 (`"%s: the same map always gives the same ops"`) defensively copies `{ ...map, cells: [...map.cells] }` before calling `drawOps`. The new test only spreads the top-level object (`{ ...map, seed: N }`), leaving `cells` as the same array reference as the shared `fixtures` object. Functionally harmless — `drawOps` never mutates `cells` — but it diverges from the file's established defensive-copy convention for fixture objects.
- **Fix**: Spread `cells` too: `{ ...map, cells: [...map.cells], seed: N }`, matching line 170's pattern.
- **Decision**: FIXED — added the defensive `cells` copy to both `seedA` and `seedB`. Verified: `cd web && npm test -- tileset` → 52/52 passed.

### F2 — Test's power depends on the fixture containing floor cells

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/map/tileset.test.ts:185-197
- **Detail**: Only `floor` cells exercise the PRNG via `CRACKED_CHANCE` (tileset.ts:80-82); a fixture with no floor cells would pass this test trivially regardless of a real regression. All 5 current fixtures in `fixtures/grids/*.json` contain floor cells, so the test is effective today, but this is a latent gap for a future floor-less fixture.
- **Fix**: None needed now; worth a comment if a floor-less fixture is ever added.
- **Decision**: SKIPPED — accepted as a latent, non-current gap.

## Notes

- Plan-drift sub-agent verdict: MATCH. The new `test.each(Object.entries(fixtures))` block iterates every fixture (not a hand-picked subset), builds seed-A/seed-B variants, calls `drawOps(seedA)` → `drawOps(seedB)` → `drawOps(seedA)`, and asserts deep equality via `toEqual`. Pure (no canvas/atlas/network). `git diff HEAD -- web/app/map/tileset.test.ts` is a pure insertion — the existing tests at lines 169-183 are textually unchanged. `git diff --stat HEAD -- web/app/map/tileset.ts` is empty; `git status --short web/` shows only the test file. No scope creep.
- Safety/pattern sub-agent: no CRITICAL or WARNING findings. Confirmed `drawOps` has no module-level mutable state today (fresh `mulberry32(map.seed)` per call), so the test's premise holds. Assertion is a full deep-equality check on the `DrawOp[]` array, not a weaker check.
- Success criteria verified directly: `cd web && npm test -- tileset` → 52/52; full `cd web && npm test` → 70/70. 4.3 (module-level PRNG break check, 22 failures) and 4.4 (no manual verification needed) were verified by the implementer per `change.md`'s Phase 4 notes and plan.md's checked boxes, with observable evidence (the break check's failure output and the confirmed-empty `tileset.ts` diff after revert).
