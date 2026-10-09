<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Seed determinism (risk #2)

- **Plan**: context/changes/testing-seed-determinism/plan.md
- **Scope**: Phase 2 of 5
- **Reviewed phases**: 2
- **Date**: 2026-10-10
- **Verdict**: NEEDS ATTENTION
- **Findings**: 1 critical, 1 warning, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | FAIL |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | FAIL |

## Findings

### F1 — Cross-run harness fails deterministically in CI (Release-only build)

- **Severity**: ❌ CRITICAL
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality / Success Criteria
- **Location**: api.Tests/Infrastructure/ChildRun.cs:25-29
- **Detail**: `ChildRun.EmitMatrixAsync` launches the child as `dotnet test api.Tests --no-build --filter ...` with no `-c`/`--configuration`, so it defaults to `Debug`. CI's only test step is `dotnet test api.Tests -c Release` (.github/workflows/ci.yml:33) — it never produces a `Debug` build. Reproduced locally by moving `api.Tests/bin/Debug` aside and running `dotnet test api.Tests -c Release --filter "FullyQualifiedName~MapDeterminismTests"`: both `Child_run_writes_one_entry_per_matrix_pair` and `Cross_run_matches_this_process_for_every_matrix_pair` fail with `Child run failed with exit code 1` / `The argument .../bin/Debug/net10.0/BattleMapGenerator.Api.Tests.dll is invalid`. This will break every CI run on this branch; it passes locally only because a dev's own `dotnet test` (no `-c`) also defaults to Debug, masking the bug. Restored the moved folder afterward — no residual change.
- **Fix**: Pass `-c Release` explicitly in `ChildRun.cs`'s `ArgumentList` (after `"api.Tests"`, before `"--no-build"`), matching ci.yml's configuration. Add a one-line comment noting it must track ci.yml's configuration.
- **Decision**: FIXED — added `-c Release` to `ChildRun.cs`'s `ArgumentList`. Re-verified by moving `api.Tests/bin/Debug` aside and running `dotnet test api.Tests -c Release --filter "FullyQualifiedName~MapDeterminismTests"` (3/3 passed), then restoring Debug and confirming `dotnet build api.Tests` + `dotnet test --no-build` still passes (3/3).

### F2 — No timeout on the child `dotnet test` process

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api.Tests/Infrastructure/ChildRun.cs:34-37
- **Detail**: `await process.WaitForExitAsync()` has no timeout or cancellation. A wedged `dotnet test` child (MSBuild lock contention, a stuck test host) hangs this harness indefinitely; the only backstop is ci.yml's 20-minute job timeout, which then kills the whole job rather than failing this one fact with a clear message.
- **Fix A ⭐ Recommended**: Add a bounded timeout (e.g. 60s, well above the ~1-2s measured child run) via `WaitForExitAsync(cts.Token)` and `process.Kill(entireProcessTree: true)` on timeout, throwing a clear "child run timed out" error.
  - Strength: Turns a silent 20-minute CI stall into a fast, diagnosable failure.
  - Tradeoff: One more constant to tune; a too-tight timeout risks flakiness on a slow CI runner.
  - Confidence: MED — the measured cost (~0.9s per Phase 1 notes) leaves generous headroom at 60s, but CI runner variance isn't measured.
  - Blind spot: Haven't observed actual CI runner timing for this harness yet (only local).
- **Fix B**: Leave as-is and rely on the job-level timeout.
  - Strength: No new code; the 20-minute ceiling is a real, if coarse, backstop.
  - Tradeoff: A hang still burns the full 20 minutes and fails with a generic job-timeout error, not a pointed one.
  - Confidence: HIGH — this is simply the current behavior.
  - Blind spot: None significant.
- **Decision**: FIXED via Fix A — added a 60s `CancellationTokenSource` around `WaitForExitAsync`, with `process.Kill(entireProcessTree: true)` and a clear error message on expiry. Verified the kill path fires correctly by temporarily setting the timeout to 1ms (both facts failed fast with "Child run did not exit within 00:00:00.0010000; killed."), then reverted to 60s and confirmed the normal run still passes (3/3).

### F3 — `FindRepoRoot()` duplicated across two test files

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/Infrastructure/ChildRun.cs:56-66 (mirrors api.Tests/MapFixtureTests.cs:65-75)
- **Detail**: `ChildRun.FindRepoRoot()` is a byte-for-byte copy of `MapFixtureTests.FindRepoRoot()`. Correct and consistent today, but now exists in two places to keep in sync.
- **Fix**: Not blocking; a future cleanup could extract a shared test-infrastructure helper. No action needed now.
- **Decision**: FIXED — extracted `api.Tests/Infrastructure/RepoRoot.cs` (`RepoRoot.Find()`) and updated both `ChildRun.cs` and `MapFixtureTests.cs` to use it, removing both private copies. Verified: `dotnet build api.Tests` clean, `dotnet test api.Tests --no-build --filter "FullyQualifiedName~MapDeterminismTests|FullyQualifiedName~MapFixtureTests"` → 8/8 passed.

### F4 — `Emit_matrix_for_child_process` is an assertion-free `[Fact]`

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/MapDeterminismTests.cs:33-44
- **Detail**: This fact is inert by design (guarded by `DETERMINISM_EMIT_PATH`) and always green in a normal run — intentional per the Phase 1 plan text, just worth knowing it adds a no-op entry to test-count metrics.
- **Fix**: None needed; documented behavior.
- **Decision**: FIXED + ACCEPTED-AS-RULE: "Guard-gated harness facts should read as infrastructure, not test coverage" (context/foundation/lessons.md). Added a clarifying comment to `Emit_matrix_for_child_process` marking it as a harness entry point, not test coverage. Verified: `dotnet build api.Tests` clean, 3/3 `MapDeterminismTests` pass.

## Notes

- Plan-drift sub-agent verdict: MATCH on all five planned-change checks (fact exists and matches intent, matrix matches `MapGeneratorTests`' same-process test, `DeepEquals` used for real content comparison, no production-code change, `MapGeneratorTests.cs` seed counts untouched). The two harness facts from Phase 1 (`Emit_matrix_for_child_process`, `Child_run_writes_one_entry_per_matrix_pair`) are necessary scaffolding for the one fact Phase 2's plan text names, not unplanned scope creep.
- Automated success criteria 2.1 and 2.2 (from plan.md) pass locally in Debug (`dotnet test api.Tests --no-build --filter "FullyQualifiedName~MapDeterminismTests"` → 3/3; `...~MapGeneratorTests` → 75/75) but **2.1 does not hold in CI's actual Release-only build** per F1 — the plan's success criterion as written doesn't specify a configuration, and the implementation silently depends on one that CI doesn't produce.
