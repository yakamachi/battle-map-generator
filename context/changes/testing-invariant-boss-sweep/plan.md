# Invariant and boss-arena sweep Implementation Plan

## Overview

Rollout Phase 1 of `context/foundation/test-plan.md` covers risks #1 (an invalid grid for an untested combination) and #6 (a boss arena that is not larger than the other rooms or is below its floor). The research found that the parametrized sweep already exists in `api.Tests/MapGeneratorTests.cs`, asserts the PRD invariants over every room count and boss size, and passes. The remaining gap is coverage: it runs over 200 seeds per combination. This plan raises that to 1000 seeds in the same theory, with no new test file and no change to the generator.

## Current State Analysis

- `MapGeneratorTests.Every_guarantee_holds_for_consecutive_seeds` (`api.Tests/MapGeneratorTests.cs:33-60`) runs 44 combinations (room counts 2–12 × skirmish, Large, Huge, Gargantuan) over seeds 1–200 (`:7`, `:40`). It asserts dimensions, room count, edges, connectivity, wall enclosure, room floors and non-overlap, doors, one-cell corridors and the arena rules (`:236-256`).
- The arena rule is area-only: the arena's floor is at least its side in both dimensions, and every other room's area is strictly smaller (`:253-254`). The generator enforces the same rule by capping other rooms at the arena's area minus one (`api/Maps/BspGenerator.cs:70-78`, `:264-276`).
- The current run is green: 80 generator and fixture tests pass in 7 s (research run, `bdc8a70`).
- `SeedCount` (200) is shared by the sweep, the determinism test (`:91`) and the different-seeds test (`:108`). Changing it globally would also change those tests.
- No valid combination threw in seeds 1–200. The generator's throw sites are `BspGenerator.cs:104`, `:163` and `:428`.

### Key Discoveries:

- The sweep is the test-plan's "required after Phase 1" invariant gate, already wired locally and in CI through `api.Tests` (`test-plan.md` §5).
- A half-cell cannot exist in the data model: cells are an integer-indexed `CellKind[]` (`api/Maps/MapModels.cs:45-46`). The bounds check is the existing test of "rooms stay inside the map" (`MapGeneratorTests.cs:225` with `Grid.At` at `:314-315`).
- The map size cap is not a risk: the largest valid map is 54×30 (`api/Maps/MapModels.cs:126-142`), inside 60×60.
- Area-only arena sizing leaves one case open: a room can be longer than the arena in one dimension (for example 3×14 next to an 8×8 arena). This is accepted in this phase (see "What We're NOT Doing").

## Desired End State

The parametrized sweep runs every valid combination (44) over 1000 seeds, asserts the same PRD invariants and the area-based arena rule, and passes on the current generator. The determinism and different-seeds tests keep their 200-seed sample. `test-plan.md` §6.1 describes how to add an invariant test for the generator, and §3 Phase 1 describes the delivered state.

Verification: the filtered `MapGeneratorTests` run passes, the new run time is recorded in the change's notes, and the full `api.Tests` run passes where the environment supports it.

## What We're NOT Doing

- **No change to the generator or the fixtures.** If a seed in 201–1000 breaks an invariant, this change stops, records the seed, and opens a follow-up change. It does not patch the generator or regenerate fixtures.
- **No both-dimensions arena rule.** The arena stays "larger in area" (user decision, 2026-10-05). The one-dimension gap stays documented in this plan and in §2's risk #6 wording.
- **No separate half-cell assertion.** The data model cannot represent a half-cell, and the existing length and bounds checks cover what it could affect.
- **No new test file, no new theory.** The sweep is extended in place.
- **No change to determinism or different-seeds seed counts** (Phase 2 owns determinism).
- **No CI workflow change.** The sweep already runs as part of `api.Tests` in CI; the change only increases its seed count.

## Implementation Approach

Introduce a separate `SweepSeedCount = 1000` constant for the parametrized sweep and use it only in `Every_guarantee_holds_for_consecutive_seeds`. The other theories keep `SeedCount = 200`, so their behaviour and run time stay the same. Run the filtered test, measure the time, and record it. Then update `test-plan.md` §6.1 with the location, naming, reference test and run command, as the rollout phase requires.

## Phase 1: Extend the invariant sweep to 1000 seeds

### Overview

Raise the sweep's seed count, verify it on all 44 combinations, and record the cookbook entry for invariant tests.

### Changes Required:

#### 1. Sweep seed count

**File**: `api.Tests/MapGeneratorTests.cs`

**Intent**: Give the parametrized sweep 1000 seeds per combination, without changing the seed count of the determinism and different-seeds tests.

**Contract**: Add `private const int SweepSeedCount = 1000;` next to `SeedCount` (`:7`). In `Every_guarantee_holds_for_consecutive_seeds`, change the seed loop bound from `SeedCount` to `SweepSeedCount` (`:40`). No other lines change.

#### 2. Cookbook entry for invariant tests

**File**: `context/foundation/test-plan.md`

**Intent**: Fill §6.1 so a future contributor knows where invariant tests for the generator go and how to run them.

**Contract**: Replace the "TBD — see §3 Phase 1" text in §6.1 with: the location (`api.Tests/MapGeneratorTests.cs`, theory `Every_guarantee_holds_for_consecutive_seeds`), the naming convention (`Every_guarantee_*`, `*_follows_*`), a reference test (the same theory), the run command (`dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"`), and the rule that a new invariant is added as an `Assert*` helper called from that theory, with its oracle taken from the PRD rather than from current output. Also note the seed budget (1000) and the rule that a determinism or fixture change does not belong in this theory.

### Success Criteria:

#### Automated Verification:

- Generator sweep passes with 1000 seeds on all 44 combinations: `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"`
- Fixture tests still pass unchanged: `dotnet test api.Tests --filter "FullyQualifiedName~MapFixtureTests"`
- The full API test suite passes where Docker is available for the integration tests: `dotnet test api.Tests`
- The API builds without new warnings: `dotnet build api`
- `SweepSeedCount` is the only seed bound changed in `MapGeneratorTests.cs` (checked by `git diff --stat` showing that file with a small change set)

#### Manual Verification:

- The measured run time of `MapGeneratorTests` with 1000 seeds is recorded in the change's notes and is acceptable for the CI job that runs `api.Tests` (the threshold is the reviewer's call; the research gave no measured baseline for the sweep alone)
- The §6.1 cookbook entry reads correctly to someone who did not write the sweep
- The one-dimension arena gap is accepted, or a follow-up is opened for it

## Testing Strategy

### Unit Tests:

- The existing sweep (`Every_guarantee_holds_for_consecutive_seeds`) with `SweepSeedCount = 1000` covers the 44 combinations and every existing assertion.
- The determinism and different-seeds theories keep `SeedCount = 200`.

### Integration Tests:

- None added. The full `api.Tests` run is the regression check for the rest of the API.

### Manual Testing Steps:

1. Run the filtered `MapGeneratorTests` and note the total time.
2. If any seed in 201–1000 fails, stop and record the failing seed, combination and assertion in the change's notes; do not change the generator or fixtures.
3. Read the §6.1 cookbook entry as a new contributor would.

## Performance Considerations

The sweep scales with the seed count: 44 combinations × 1000 seeds versus 44 × 200 today. The current 80 tests take 7 s; the sweep's share was not timed separately, so the new run time is unmeasured. The measurement in the manual criteria decides whether a further reduction is needed.

## Migration Notes

None. The change touches no data, API or fixture.

## References

- Related research: `context/changes/testing-invariant-boss-sweep/research.md`
- Test plan: `context/foundation/test-plan.md` §2 (risks #1 and #6), §3 (Phase 1), §6.1 (cookbook entry)
- Existing sweep: `api.Tests/MapGeneratorTests.cs:7`, `:33-60`, `:236-256`
- Generator arena placement and area cap: `api/Maps/BspGenerator.cs:70-78`, `:264-276`
- Size formula and minimum arena floors: `api/Maps/MapModels.cs:116-142`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Extend the invariant sweep to 1000 seeds

#### Automated

- [x] 1.1 Generator sweep passes with 1000 seeds on all 44 combinations: `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"`
- [x] 1.2 Fixture tests still pass unchanged: `dotnet test api.Tests --filter "FullyQualifiedName~MapFixtureTests"`
- [x] 1.3 The full API test suite passes where Docker is available: `dotnet test api.Tests`
- [x] 1.4 The API builds without new warnings: `dotnet build api`
- [x] 1.5 `SweepSeedCount` is the only seed bound changed in `MapGeneratorTests.cs`

#### Manual

- [x] 1.6 The measured run time of `MapGeneratorTests` with 1000 seeds is recorded in the change's notes and accepted for CI
- [x] 1.7 The §6.1 cookbook entry reads correctly to someone who did not write the sweep
- [x] 1.8 The one-dimension arena gap is accepted, or a follow-up is opened for it
