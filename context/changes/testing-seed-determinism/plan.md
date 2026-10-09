# Seed determinism (risk #2) Implementation Plan

## Overview

Rollout Phase 2 of `context/foundation/test-plan.md`. Risk #2: the same seed and parameters must give the same map for a DM, across requests, processes and web re-renders, so a map they liked can be reproduced. The generator is already deterministic by construction (research: `context/changes/testing-seed-determinism/research.md`). What is missing is tests that would fail if that stopped being true across processes, across endpoint calls, and across interleaved web renders. This plan adds those tests and fills the §6.2 cookbook entry. No production code changes.

## Current State Analysis

- **Generator**: `BspGenerator.Generate` creates a fresh `Prng` per call (`api/Maps/BspGenerator.cs:64`); corridor ties are broken by an explicit key (`:445-449`). No `System.Random`, clock or unordered iteration reaches the output.
- **Same-process test**: `api.Tests/MapGeneratorTests.cs:89-101` generates twice per seed (1–200) in one process and compares `Cells` and `Rooms`. It cannot detect drift between processes.
- **Cross-run reference**: only `api.Tests/MapFixtureTests.cs` against five committed grids in `fixtures/grids/` (three default-size seeds, one three-room map, one Huge boss map).
- **Endpoint**: `POST /api/maps/generate` (`api/Maps/MapEndpoints.cs:48-54`). An omitted seed is drawn from the OS CSPRNG (`:118-123`) and echoed. No test sends the same explicit seed twice and compares bodies. `MapEndpointTests` builds a fresh `ApiFactory` per test (`api.Tests/MapEndpointTests.cs:21-27`), and the generate policy allows 10 requests per minute per account (`api.Tests/MapEndpointTests.cs:72-83` asserts the eleventh is rejected), so a test may issue up to 10 requests.
- **Web**: `drawOps` builds `mulberry32(map.seed)` inside each call (`web/app/map/tileset.ts:70`); its only consumer is the cracked-floor choice (`:81`). The existing determinism tests in `web/app/map/tileset.test.ts:169-183` are near self-comparisons, and no test interleaves two seeds.
- **Helpers**: `grid(rows, seed)` in `tileset.test.ts:15` builds a `MapGrid`; `FindRepoRoot()` in `MapFixtureTests.cs:65` locates the repo root.

## Desired End State

- The API unit suite fails if a seed's grid differs between this process and a separate `dotnet test` process, for seeds 1–200 in skirmish and Huge boss at 6 rooms.
- The API integration suite fails if two explicit-seed requests return different `cells`, `rooms` or `parameters`, or if the echoed seed of an omitted-seed request does not reproduce the same `cells` and `rooms` when sent back.
- The web unit suite fails if `drawOps` for seed A changes after an intervening call for seed B, for each pinned fixture.
- §6.2 of `test-plan.md` describes the new determinism tests and their run commands; §3 Phase 2 is `complete`.

### Key Discoveries:

- The generator has no process-global state, so cross-process drift can only come from the runtime (for example string-hash randomisation), not from our code. The child-process check is the only test that can see that class; a same-process test cannot. (`api/Maps/Prng.cs:3-4`, `api/Maps/BspGenerator.cs:445-449`.)
- `ApiFactory` supplies the database and configuration the endpoint needs; the endpoint round-trip test needs no new fixture (`api.Tests/MapEndpointTests.cs:21-27`).
- `lessons.md` ("Seeded code breaks ties explicitly") requires a same-seed check to catch runtime-order drift; the child-process check follows that rule.

## What We're NOT Doing

- No new production code and no change to the generator, the PRNG, the endpoint contract or the fixtures in `fixtures/grids/`.
- No re-render test at the `renderMap` level (`web/app/map/render.test.ts`) and no Playwright visual re-render: the pure `drawOps` interleave test covers the property at millisecond cost (user decision).
- No wider pinned fixture set: the cross-run check uses a child process, not more committed seeds (user decision).
- No change to `api.Tests/MapGeneratorTests.cs` `SeedCount` (200) or the Phase 1 sweep (`SweepSeedCount`, 1000).
- No e2e test: the determinism property is covered by the API and web unit and integration layers (user scope).
- No check of seed resolution on the web client (`web/app/api/client.ts:45`); the omitted-seed round trip is checked through the API only.
- No restart or DB-level test: map generation has no persisted state (research).

## Implementation Approach

Five stages, cheapest signal first where there is no dependency. Stage 1 is the only infrastructure: the child-process harness the cross-run test needs. Stages 2–4 are independent of each other and each depends only on Stage 1 for Stage 2. Stage 5 documents the result. The oracle in every stage is the rule in `AGENTS.md` ("the same seed and parameters give the same grid") and the PRD FR-005 (regeneration with the same parameters is a new seed; the DM must be able to reproduce a map they liked), never the current output.

## Phase 1: Cross-run harness (setup)

### Overview

Infrastructure for Phase 2. A test-only entry point that, when a guard environment variable is set, writes grids for a fixed list of seeds and parameters to a file, plus a helper that runs it in a separate `dotnet test` process and reads the file back. No assertions about map content here.

### Changes Required:

#### 1. Child-run entry point

**File**: `api.Tests/MapDeterminismTests.cs` (new)

**Intent**: Add a test that does nothing unless `DETERMINISM_EMIT_PATH` is set; when set, it writes the `cells` and `rooms` of every combination in the Phase 2 matrix to that path as JSON. The guard keeps it out of normal runs.

**Contract**: Environment variable `DETERMINISM_EMIT_PATH` (absolute file path). Output: JSON keyed by `<seed>-<bossSize|skirmish>`, values `{cells, rooms}` in the same serializer settings as `api.Tests/MapFixtureTests.cs`. Uses `BspGenerator.Generate` directly, not the endpoint.

#### 2. Child-process helper

**File**: `api.Tests/Infrastructure/ChildRun.cs` (new)

**Intent**: Run `dotnet test api.Tests --no-build --filter "FullyQualifiedName~MapDeterminismTests"` with `DETERMINISM_EMIT_PATH` set, wait for it, and return the parsed JSON. Locate the repo root with the same approach as `MapFixtureTests.FindRepoRoot`.

**Contract**: `Task<JsonDocument> EmitGridsInChildProcessAsync()`. Fails the test with the child's exit code and stderr when the child fails. `--no-build` is required so the child runs the assembly the parent already built, not a second build. The child must not spawn a further child; the guard variable is the only trigger.

### Success Criteria:

#### Automated Verification:

- The harness test compiles and does nothing without the variable: `dotnet test api.Tests --filter "FullyQualifiedName~MapDeterminismTests"` passes.
- Running the helper once produces a JSON file with one entry per matrix combination: a harness test asserts the number of top-level keys equals the number of (seed, parameter) pairs the matrix defines (seeds 1–200 for skirmish plus Huge boss, 400 entries).
- Lint and build with no new warnings: `dotnet build api`.

#### Manual Verification:

- The child run's wall-clock time is recorded in the change notes, so the cost of the cross-run test is known before CI accepts it.

## Phase 2: API unit cross-run determinism

### Overview

Assert that a grid produced in a separate process matches the grid produced in this process, for each seed and parameter pair the harness emits.

### Changes Required:

#### 1. Cross-run assertion

**File**: `api.Tests/MapDeterminismTests.cs`

**Intent**: Add a `[Fact]` that runs the harness, generates the same matrix in this process, and asserts equality of `cells` and `rooms` for every seed 1–200 in skirmish and Huge boss at 6 rooms.

**Contract**: Behaviour asserted: for each seed and parameter pair, the child process and this process return equal `cells` and `rooms`. Oracle: `AGENTS.md` determinism rule. Uses `MapSize.DefaultRoomCount` and the `Parameters(...)` helper already in `MapGeneratorTests`, so the matrix matches the existing same-process test.

### Success Criteria:

#### Automated Verification:

- The cross-run fact passes: `dotnet test api.Tests --filter "FullyQualifiedName~MapDeterminismTests"`.
- The same-process determinism test still passes: `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"`.
- A deliberate mismatch fails: with the child seed list shifted by one, the fact fails; revert the shift afterwards (verified once locally, not committed).

#### Manual Verification:

- The added CI time for this fact is recorded and accepted, or the matrix is reduced in a follow-up.

## Phase 3: API integration endpoint round-trip

### Overview

Assert at the HTTP layer that the seed a DM sees is the one that reproduces their map.

### Changes Required:

#### 1. Endpoint determinism tests

**File**: `api.Tests/MapEndpointTests.cs`

**Intent**: Add two facts. The first sends the same explicit seed twice and compares the two bodies. The second sends a body without a seed, reads the echoed `seed`, sends `{seed: echoed}` with the same other fields, and compares the two bodies.

**Contract**: Behaviour asserted: equal `cells`, `rooms` and `parameters` for equal inputs, and equal `cells` and `rooms` when a server-drawn seed is echoed and sent back. Each fact uses one `ApiFactory` and one logged-in client, issues at most 4 requests, and stays under the 10-per-minute generate limit. Uses the existing `CreateLoggedInClientAsync` helper.

### Success Criteria:

#### Automated Verification:

- The new endpoint facts pass: `dotnet test api.Tests --filter "FullyQualifiedName~MapEndpointTests"`.
- The existing rate-limit fact still passes: `The_eleventh_generate_within_a_minute_returns_429_with_retry_after` in the same run.
- The omitted-seed round trip fails when the echoed seed is wrong: a local check that changes the echoed seed by one fails the fact (not committed).

#### Manual Verification:

- The omitted-seed response is inspected once by hand to confirm `seed` is a plain JSON number in `uint` range.

## Phase 4: Web unit interleaved determinism

### Overview

Assert that the web's per-call PRNG keeps the seed-derived variant stable when other seeds are rendered in between.

### Changes Required:

#### 1. Interleaved drawOps test

**File**: `web/app/map/tileset.test.ts`

**Intent**: Add one test that, for each fixture in the file, calls `drawOps` with seed A, then seed B on the same cells, then seed A again, and asserts the two seed-A results are equal. The existing tests at `:169-183` are kept unchanged.

**Contract**: Behaviour asserted: `drawOps(map)` depends only on the map's cells and seed, not on earlier calls. Oracle: `AGENTS.md` web rule (variant randomness derived from the response seed). Pure: no canvas, no atlas, no network. Uses the existing `grid(rows, seed)` helper (`:15`).

### Success Criteria:

#### Automated Verification:

- The new test passes: `cd web && npm test -- tileset`.
- The whole web unit suite still passes: `cd web && npm test`.
- A deliberate shared-state change fails the test: a local module-level PRNG in `tileset.ts` makes the seed-A equality fail (verified once, not committed).

#### Manual Verification:

- None; the test is pure and has no visual output.

## Phase 5: Cookbook and §3 status

### Overview

Record the three layers in `test-plan.md` so the next contributor can add a determinism test without rediscovering the harness.

### Changes Required:

#### 1. §6.2 cookbook entry

**File**: `context/foundation/test-plan.md`

**Intent**: Replace the `TBD — see §3 Phase 2` text in §6.2 with the location, naming, reference test and run command for the three layers, and the child-process harness as the cross-run mechanism.

**Contract**: Sections in the same style as §6.1 (Location, Naming, Reference test, Run). Reference tests: `MapDeterminismTests` (cross-run), `The_same_seed_and_parameters_give_identical_maps` (same process), the Phase 3 endpoint facts, and the Phase 4 interleave test. Run commands: the three `dotnet test` / `npm test` lines above.

#### 2. §3 status and §5 gate

**File**: `context/foundation/test-plan.md`

**Intent**: Set the Phase 2 row to `complete` with its change folder, and mark the §5 determinism gate as wired by this phase. Also correct Phase 1's row from `change opened` to `complete` (its change is implemented and closed out).

**Contract**: Status values from the fixed vocabulary in §3 only. The Phase 1 correction is a stale-status fix from the closeout commit `1c5717f`, not new work.

### Success Criteria:

#### Automated Verification:

- No `TBD` remains in §6.2: `grep -n "TBD" context/foundation/test-plan.md` shows only §6.3–§6.5.
- The §3 Phase 1 and Phase 2 rows show only fixed status values (`change opened`, `complete`, …): `grep -n "^| [12] |" context/foundation/test-plan.md`.

#### Manual Verification:

- A new contributor reads §6.2 and can name the file and command for each layer without opening the code.

## Testing Strategy

### Unit Tests:

- `MapDeterminismTests`: cross-run equality over seeds 1–200 for skirmish and Huge boss at 6 rooms.
- `MapGeneratorTests`: existing same-process determinism is kept.
- `tileset.test.ts`: interleaved `drawOps` equality for each fixture.

### Integration Tests:

- `MapEndpointTests`: explicit-seed equality, and the omitted-seed echo round trip.

### Manual Testing Steps:

1. Run the three commands in §6.2 locally and record the wall-clock times of the cross-run fact.
2. Change one tie-break key locally and confirm the cross-run and endpoint facts both fail (revert afterwards).

## Performance Considerations

- The cross-run fact starts a second `dotnet test` process with `--no-build`. Expected cost: process start plus the matrix generation, which the Phase 1 sweep measurements bound. The measured time is recorded in Phase 1's manual criterion and decides whether the seed count or the matrix is reduced.
- The endpoint facts issue at most 4 requests each, within the 10-per-minute account limit.

## Migration Notes

None. No data, API contract or fixture changes.

## References

- Related research: `context/changes/testing-seed-determinism/research.md`
- Test plan: `context/foundation/test-plan.md` §2 (risk #2), §3 (Phase 2), §5 (determinism gate), §6.2 (cookbook)
- Same-process test: `api.Tests/MapGeneratorTests.cs:89-101`
- Generator PRNG and tie-break: `api/Maps/BspGenerator.cs:64`, `api/Maps/Prng.cs:3-24`, `api/Maps/BspGenerator.cs:445-449`
- Endpoint: `api/Maps/MapEndpoints.cs:48-54`, `:118-123`
- Web PRNG: `web/app/map/tileset.ts:70-81`, `web/app/map/prng.ts:3-4`
- Existing web determinism tests: `web/app/map/tileset.test.ts:169-183`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Cross-run harness (setup)

#### Automated

- [x] 1.1 Harness test compiles and is inert without `DETERMINISM_EMIT_PATH`: `dotnet test api.Tests --filter "FullyQualifiedName~MapDeterminismTests"` — 90ced18
- [x] 1.2 Child run writes one entry per matrix combination: harness key-count assertion — 90ced18
- [x] 1.3 API builds with no new warnings: `dotnet build api` — 90ced18

#### Manual

- [x] 1.4 Child-run wall-clock time recorded in the change notes — 90ced18

### Phase 2: API unit cross-run determinism

#### Automated

- [x] 2.1 Cross-run fact passes for seeds 1–200, skirmish and Huge at 6 rooms: `dotnet test api.Tests --filter "FullyQualifiedName~MapDeterminismTests"` — 90ced18
- [x] 2.2 Same-process determinism still passes: `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"` — 90ced18
- [x] 2.3 Shifted seed list fails the fact (local check, reverted) — 90ced18

#### Manual

- [x] 2.4 Added CI time for the cross-run fact recorded and accepted — 90ced18

### Phase 3: API integration endpoint round-trip

#### Automated

- [ ] 3.1 Endpoint determinism facts pass: `dotnet test api.Tests --filter "FullyQualifiedName~MapEndpointTests"`
- [ ] 3.2 Rate-limit fact still passes in the same run
- [ ] 3.3 Wrong echoed seed fails the round-trip fact (local check, reverted)

#### Manual

- [ ] 3.4 Omitted-seed response `seed` checked by hand as a `uint`-range JSON number

### Phase 4: Web unit interleaved determinism

#### Automated

- [ ] 4.1 Interleaved drawOps test passes: `cd web && npm test -- tileset`
- [ ] 4.2 Whole web unit suite passes: `cd web && npm test`
- [ ] 4.3 Module-level PRNG fails the interleave test (local check, reverted)

#### Manual

- [ ] 4.4 None (pure test; no visual output)

### Phase 5: Cookbook and §3 status

#### Automated

- [ ] 5.1 No TBD left in §6.2: `grep -n "TBD" context/foundation/test-plan.md`
- [ ] 5.2 §3 rows use fixed status values only

#### Manual

- [ ] 5.3 New contributor can name the file and command for each layer from §6.2
