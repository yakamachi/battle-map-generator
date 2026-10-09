---
change_id: testing-seed-determinism
title: Seed determinism
status: implementing
created: 2026-10-05
updated: 2026-10-05
archived_at: null
---

## Notes

Rollout Phase 2 of context/foundation/test-plan.md ("Seed determinism").
Risks covered: #2 (same seed and parameters must reproduce the same grid across requests and the same sprite variant across re-renders).
Test types planned: unit (repeat generation), integration (two endpoint calls), web render test (same grid and variant on re-render).

Phase 1 (cross-run harness) measurements, 2026-10-05: `Child_run_writes_one_entry_per_matrix_pair` takes about 0.9 s including the child `dotnet test --no-build` process (400 entries). Full `api.Tests` run: 159 passed in 38 s.
Phase 1 break check: writing 10 of 400 entries turns the harness fact red (`Expected: 400`, `Actual: 10`); restored from the staged file.

Phase 2 (API unit cross-run determinism) measurements, 2026-10-10: `MapDeterminismTests` (3 facts, including the new cross-run assertion) passes in about 2 s — the cross-run fact's own child-process cost dominates; the 400-pair comparison itself is negligible. `MapGeneratorTests` still passes (75 tests, 36 s). Accepted as-is; no reduction to the matrix or seed count needed.
Phase 2 break check: shifting this process's generation by one seed (`seed + 1`) while the child process still emits for `seed` turns the cross-run fact red on the first pair (`1-skirmish: cells differ between this process and the child process.`); reverted after confirming.
