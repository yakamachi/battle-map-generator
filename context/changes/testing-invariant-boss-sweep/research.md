---
date: 2026-10-05T15:46:47+02:00
researcher: Karol Mitek
git_commit: bdc8a70
branch: main
repository: battle-map-generator
topic: "Invariant and boss-arena sweep: what the generator's test suite already proves for risks #1 and #6, and what is missing"
tags: [research, codebase, map-generator, BspGenerator, MapGeneratorTests, boss-arena, test-plan]
status: complete
last_updated: 2026-10-05
last_updated_by: Karol Mitek
---

# Research: Invariant and boss-arena sweep

**Date**: 2026-10-05T15:46:47+02:00
**Researcher**: Karol Mitek
**Git Commit**: bdc8a70 (working tree also has uncommitted `CLAUDE.md` edit and untracked `context/foundation/test-plan.md`; neither touches `api/`)
**Branch**: main
**Repository**: battle-map-generator

## Research Question

For rollout Phase 1 of `context/foundation/test-plan.md` ("Invariant and boss-arena sweep"), covering risks #1 and #6: what does the generator's existing test suite already prove, what does the plan's own current-state claim say about it, and which parts of the intended sweep are still unproven? The oracle is the PRD invariants, not current output.

## Summary

Phase 1 is smaller than the test plan assumes. The plan's §2 challenge for risk #1 says the fixtures "pin three seeds plus two special maps". That is true of the fixtures, but `api.Tests/MapGeneratorTests.cs` already runs a parametrized sweep: all 44 combinations of room count (2–12) × encounter (skirmish, or boss with Large, Huge, Gargantuan) over seeds 1–200, asserting the PRD-level invariants, not fixed output. That suite passes on the current code (80/80 generator and fixture tests green, 7 s).

Against the intent of this phase, the remaining gaps are:

1. **Seed coverage is a sample.** Seeds 1–200 per combination. Seeds beyond 200 are never generated. Nothing in the code suggests a failure mode that only appears above seed 200, but none is ruled out either.
2. **"Boss arena above every other room" is checked by area only.** A room may be larger than the arena in one dimension and still pass. The PRD wording ("wyraźnie większy", clearly larger) does not settle whether area or both dimensions is the intended oracle. This is a product choice for `/10x-plan`, not a code fact.
3. **"No half-cells" has no explicit assertion**, and under the current data model it cannot occur: the grid is an integer-indexed array of enum values. The closest real check (rooms staying inside the map) is already in the suite, through out-of-bounds reads returning `Void`.
4. **The 60×60 size cap is no longer a question.** The largest valid map (12 rooms, Gargantuan boss) is 54×30 by the size formula, inside the cap.

Risk #6's floor requirement (Large 8×8, Huge 10×10, Gargantuan 12×12) is enforced by construction in the generator and asserted in the suite. The PRD floors and `MapSize.MinArenaSide` agree.

## Detailed Findings

### The existing sweep (risk #1)

- The parameter surface: room count 2–12 (`api/Maps/MapModels.cs:100-101`), encounter skirmish or boss, boss size `Large`/`Huge`/`Gargantuan` (`MapModels.cs:26-31`). Width and height are checked against 14–60 (`api/Maps/BspGenerator.cs:48-57`, `MapModels.cs:93-98`).
- `api.Tests/MapGeneratorTests.cs:9-24` builds 11 × 4 = 44 combinations. `:7` sets `SeedCount = 200`. `Every_guarantee_holds_for_consecutive_seeds` (`:33-60`) runs each combination over seeds 1–200 and asserts:
  - the dimensions equal `MapSize.For`, and `Cells.Length == width * height` (`:47-49`);
  - the room count equals the request (`:50`);
  - edges are not walkable (`:162-174`);
  - all walkable cells form one connected region (`:176-196`);
  - every walkable cell is enclosed by walls (`:198-212`);
  - every room's floor is the expected kind and rooms do not overlap (`:214-232`);
  - every room has a door on its ring (`:258-272`);
  - doors are straight-through (`:274-287`);
  - corridors are one cell wide, with no 2×2 corridor block (`:289-300`);
  - arena invariants (`:236-256`, detailed below).
- The oracle is the invariant set, not stored output. Fixed output is checked only in `api.Tests/MapFixtureTests.cs` (seeds 1, 42, 20260925, plus a 3-room map and a Huge boss map).
- Determinism is checked separately (`MapGeneratorTests.cs:85-99`), and is out of this phase's scope (test plan Phase 2).

### Reachability and bounds

- "Every room reachable": `AssertWalkableCellsAreConnected` (`MapGeneratorTests.cs:176-196`) checks that all walkable cells form one component. Each room's floor is walkable (`:225` with `grid.At` in `:314-315`), so every room is reached through that component. `Connect` joins every leaf into one tree (`api/Maps/BspGenerator.cs:283-313`), which is why the property holds by construction.
- "Stays inside bounds": `Grid.At` maps out-of-range coordinates to `Void` (`MapGeneratorTests.cs:314-315`). A room that extends past the map edge therefore fails the floor-kind assertion at `:225`. This was read from the test code; the sweep has not been run against a deliberately broken generator to show it fails.

### Half-cells (intent item)

- `MapModels.cs:45-46`: cells are a row-major `CellKind[]`, index `y * Width + x`. Room rectangles are integer `(X, Y, Width, Height)` (`MapModels.cs:40`). A half-cell has no representation, so there is nothing to assert beyond the length and bounds checks above.

### Boss arena (risk #6)

- Floors: `MapSize.MinArenaSide` returns 8, 10, 12 for Large, Huge, Gargantuan (`MapModels.cs:116-122`). These match PRD Business Logic (Duży 2×2 → 8×8, Ogromny 3×3 → 10×10, Gigantyczny 4×4+ → 12×12).
- Placement (`api/Maps/BspGenerator.cs:70-79`): the arena is placed first, in a strip of width `side + 4`. The arena's floor is never trimmed (`PlaceRoom` gets `maxArea = int.MaxValue` at `:76`), and its minimum side is `side` (`:256`, `:261`). The reserved leaf width is `side + 2 * RoomMargin` (`:101`, `RoomMargin = 2`), so the arena's floor is at least `side` in both dimensions by construction.
- Other rooms are capped: `maxRoomArea = arenaArea - 1` (`:78`), and `PlaceRoom` trims the longer side until the room's area is at most the cap (`:264-276`). This caps area only.
- The suite asserts exactly one arena, floor ≥ side in both dimensions, arena cell count equal to arena area, and each other room's area strictly less than the arena's (`MapGeneratorTests.cs:236-256`). Area is the only size comparison.
- Consequence for the oracle (inference, not run): the area rule allows a room whose one side is longer than the arena's floor. Example: a Large boss arena at its floor (8×8 = 64) permits a 3×14 room (area 42, no trimming because 42 ≤ 63). `seed-42-boss-huge.json` has a 3×14 room next to a 10×15 arena. That fixture is consistent with either rule.
- Size cap: `MapSize.For` gives width `W(rooms) + side` and height `max(H(rooms), side + 4)` (`MapModels.cs:126-142`). For 12 rooms with Gargantuan: `42 + 12 = 54` by `max(30, 16) = 30`, so 54×30. The cap is 60×60 (`MapModels.cs:93-94`). The largest valid case is inside the cap.

### Current results

- `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests|FullyQualifiedName~MapFixtureTests"` on bdc8a70: 80 passed, 0 failed, 7 s. The build also regenerated `api/BattleMapGenerator.Api.json`, which `git status` shows as unchanged.
- No valid combination in seeds 1–200 threw (the sweep would surface an exception as a failure). The generator's throw sites are `BspGenerator.cs:104` (arena does not fit the width), `:163` (map too small for the room count) and `:428` (no corridor could join two rooms, documented as unreachable by construction). The current tests exercise only the size and room-count guards with invalid input (`MapGeneratorTests.cs:117-140`); none exercises `:428` directly.

## Code References

- `api/Maps/BspGenerator.cs:32-43` - parameter entry point, boss-size requirement
- `api/Maps/BspGenerator.cs:45-92` - size validation, arena placement, room placement, connection
- `api/Maps/BspGenerator.cs:98-147` - `ReserveArena`: strip width `side + 4`, arena floor ≥ side
- `api/Maps/BspGenerator.cs:256-281` - `PlaceRoom`: minimum side, area trim against `maxArea`
- `api/Maps/MapModels.cs:87-143` - `MapSize`: limits, size table, `MinArenaSide`, `For`
- `api.Tests/MapGeneratorTests.cs:9-24` - the 44 parameter combinations
- `api.Tests/MapGeneratorTests.cs:33-60` - the sweep over seeds 1–200
- `api.Tests/MapGeneratorTests.cs:236-256` - arena assertions (floor, single arena, area-only size comparison)
- `api.Tests/MapGeneratorTests.cs:305-332` - `Grid`, out-of-bounds reads as `Void`
- `api.Tests/MapFixtureTests.cs:15-39` - fixture seeds and the two special maps
- `fixtures/grids/seed-42-boss-huge.json` - Huge boss map: arena 10×15, one 3×14 room

## Architecture Insights

- Invariants are enforced in two places: by construction in the generator (margins, leaf sizes, arena reservation, area trim) and by the test suite as an external oracle. The suite does not depend on the generator's internals, only on the grid and the room list.
- The generator is deterministic from the seed (`Prng` in `api/Maps/Prng.cs:1-25`), so a seed sample is a fixed set of maps, not random testing. More seeds means more maps, not different distributions.

## Historical Context (from prior changes)

- `context/archive/2026-09-30-encounter-parameters/research.md:35` - the 60×60 cap "does not look like a blocker" for 12 rooms plus a 12×12 arena, an inference from the algorithm's shape. **Supported now**, by the formula at `MapModels.cs:126-142`: the largest valid map is 54×30. The archived note's claim that seeds 1–1000 fit 10–20 rooms into 40×30 was not re-verified here.
- `context/foundation/test-plan.md` §2, risk #1 "must challenge": "the fixtures already cover the algorithm — they pin three seeds plus two special maps". **Partially contradicted.** The fixture description is accurate (`MapFixtureTests.cs:15-39`), but the claim that the fixtures are the algorithm's coverage is not: the sweep at `MapGeneratorTests.cs:33-60` already covers 44 combinations over 200 seeds.
- `context/foundation/test-plan.md` §2, risk #6 "must challenge": "none of the pinned fixtures use the largest combination" — **supported** (the fixtures use 6 rooms + Huge and 6 rooms default). **Contradicted in part**: the sweep already includes 12 rooms + Gargantuan (`MapGeneratorTests.cs:9-24`).
- `context/foundation/test-plan.md` §2 cheapest-layer column for risk #6 says the arena size check is "shared with risk #1's sweep". **Supported**: the arena assertions are inside the same theory (`MapGeneratorTests.cs:58`).

## Related Research

- `context/archive/2026-09-30-encounter-parameters/research.md` - the encounter-parameters research that introduced the size table and the arena rules.

## Open Questions

1. **Oracle for "size above every other room."** Area only (what the suite checks now), or area plus each dimension ≤ arena's? The PRD says "wyraźnie większy od pozostałych" (clearly larger). A stricter rule could fail on the current generator and would then need either an algorithm change or a relaxed rule. This is a product decision: the user or the PRD owner should settle it before `/10x-plan` writes the assertion.
2. **Seed budget for the sweep.** The current 200 is a choice, not a derived number. Options include raising the count in the same theory, or a separate, longer sweep behind an opt-in flag. The cost is unmeasured: 80 tests take 7 s, but the sweep's share of that was not timed separately. `/10x-plan` should time one candidate count before committing to one.
3. **Whether Phase 1 adds any assertion beyond the existing suite** once questions 1 and 2 are answered. The honest scope may be "extend the existing theory" rather than "create a new sweep".
4. **No half-cells assertion.** Decide whether to add a named check that documents the structural guarantee, or to note it as covered by the length and bounds checks.
