---
change_id: testing-seed-determinism
title: Seed determinism
status: impl_reviewed
created: 2026-10-05
updated: 2026-10-10
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

Phase 2 impl review, 2026-10-10: fixed a CRITICAL finding (cross-run harness ran `dotnet test --no-build` with no `-c`, defaulting to Debug, while CI only builds Release — reproduced locally and fixed by pinning `-c Release` in `ChildRun.cs`), added a 60s timeout with process-tree kill on the child process (verified the kill path fires with a 1ms timeout, then reverted to 60s), extracted a shared `RepoRoot.Find()` helper used by `ChildRun.cs` and `MapFixtureTests.cs`, and added a clarifying comment on the harness entry-point fact. Lesson recorded in `lessons.md` ("Guard-gated harness facts should read as infrastructure, not test coverage"). Full report: `reviews/impl-review-phase-2.md`.

Phase 3 (API integration endpoint round-trip) measurements, 2026-10-10: both new facts plus the existing rate-limit fact pass together: `dotnet test api.Tests --filter "FullyQualifiedName~MapEndpointTests"` → 36/36 in ~3 s. Break check: resending `echoedSeed + 1` instead of the real echoed seed turns `Generate_without_a_seed_reproduces_the_same_map_when_the_echoed_seed_is_resent` red (`'cells' differed when the echoed seed was resent.`); reverted. The echoed-seed manual check (3.4) is also asserted automatically (`JsonValueKind.Number` + `GetUInt32()` succeeds), stronger than the one-time hand inspection the plan called for.

Phase 3 impl review, 2026-10-10: APPROVED, one OBSERVATION (a comment citing "plan §3.4" instead of behavioral rationale, at odds with every other comment in the file) — fixed, reworded to drop the plan citation. No production code touched, no scope creep. Full report: `reviews/impl-review-phase-3.md`.

Phase 4 (web unit interleaved determinism) measurements, 2026-10-10: `cd web && npm test -- tileset` → 52/52 passed; full `npm test` → 70/70 passed. Break check: making the floor-variant PRNG module-level (shared across calls, ignoring `map.seed`) turns the new interleave test red on every fixture (22 failures across both browsers, e.g. a `tiles_cracked`/`tiles` mismatch after an intervening seed-B call); reverted and confirmed `git diff` on `tileset.ts` is empty. No production code changed in the committed result.

Phase 4 impl review, 2026-10-10: APPROVED, 2 OBSERVATIONS — F1 (new test didn't defensively copy `cells` like its sibling test) fixed; F2 (test's power depends on the fixture having floor cells) accepted as a latent, non-current gap. Full report: `reviews/impl-review-phase-4.md`.

Phase 5 (cookbook and §3 status), 2026-10-10: filled §6.2 of `test-plan.md` with the three determinism layers (API unit cross-run, API integration round-trip, web unit interleave) plus the cross-run mechanism, following §6.1's style. Set §3 Phase 2 to `complete` with its change folder; §5's determinism gate is now `required (wired; ci.yml jobs api, web-tests)` — both jobs already run the new tests, no new CI job needed. `grep -n "TBD" context/foundation/test-plan.md` now shows only §6.3–§6.5 (plus the unrelated, pre-existing §6 intro template line); `grep -n "^| [12] |"` confirms both §3 phase rows use fixed status values. This closes out the plan: all 5 phases complete.
