---
date: 2026-10-05 22:08 +0200
researcher: Karol Mitek
git_commit: 1c5717f
branch: test/invariant-boss-sweep
repository: battle-map-generator
topic: "Seed determinism: where a seed becomes a grid and a render (risk #2, test-plan Phase 2)"
tags: [research, determinism, seed, prng, testing]
status: complete
last_updated: 2026-10-05
last_updated_by: Karol Mitek
---

# Research: Seed determinism (risk #2, test-plan Phase 2)

**Date**: 2026-10-05 22:08 +0200
**Researcher**: Karol Mitek
**Git Commit**: 1c5717f
**Branch**: test/invariant-boss-sweep
**Repository**: battle-map-generator

## Research Question

Risk #2 (`context/foundation/test-plan.md` §2): the same seed and parameters must reproduce the same grid across requests, restarts or processes, and the same sprite variant on each web re-render. Establish (1) where the seed and PRNG pass through code and where randomness enters the web render; (2) what behaviour would prove protection, derived from `AGENTS.md` and the PRD rather than from the implementation shape; (3) the cheapest test layer per path. Establish what `api.Tests/MapGeneratorTests.cs:89` (`The_same_seed_and_parameters_give_identical_maps`) does and does not cover.

## Summary

- **The generator is deterministic by construction.** Within `api/`, a seed plus parameters reach the grid through one fresh `Prng` per call (SplitMix64) and one explicit tie-break for corridor search. No `System.Random`, clock, GUID or unordered-collection iteration reaches the generated output. Condition: this inspected path only (`api/Maps/`, endpoint to `BspGenerator.Generate`). Sources: `api/Maps/BspGenerator.cs:64`, `api/Maps/Prng.cs:6-24`, `api/Maps/BspGenerator.cs:445-449`.
- **The one nondeterministic input is the server-drawn seed, and it lives in the endpoint.** When the request omits `seed`, the endpoint draws 4 bytes from the OS CSPRNG and echoes the seed back. Condition: `seed` absent from the request body. Source: `api/Maps/MapEndpoints.cs:54`, `:118-123`.
- **The existing same-seed test is a same-process self-comparison.** `The_same_seed_and_parameters_give_identical_maps` calls `BspGenerator.Generate` twice per seed in one process and compares `Cells` and `Rooms`. It covers seeds 1–200 (`SeedCount`), skirmish and Huge boss, 6 rooms. Source: `api.Tests/MapGeneratorTests.cs:7`, `:89-101`. It would not catch a difference between processes, runtime versions or a changed evaluation order that is stable within one process.
- **The cross-run reference is the fixture set, and it is narrow.** `api.Tests/MapFixtureTests.cs` pins five JSON grids: default size for seeds 1, 42 and 20260925; three rooms for seed 42; Huge boss for seed 42 (`fixtures/grids/*.json`). Source: `api.Tests/MapFixtureTests.cs:15-38`, `:46-55`. This is the only check that compares output against something produced in a different run.
- **Endpoint tests never compare two endpoint responses for the same seed.** The closest test compares endpoint output with a direct generator call in the same process. Source: `api.Tests/MapEndpointTests.cs:47`, `:180`. No restart or second-instance test exists for maps (`AuthEndpointTests` and `AccountStoreTests` do restart tests for cookies and Data Protection, not maps).
- **The web variant is seeded per call, not per module.** `drawOps` creates `mulberry32(map.seed)` inside each call (`web/app/map/tileset.ts:70`), and the only consumer is the cracked-floor choice (`tileset.ts:81`, `CRACKED_CHANCE` at `:32`). Re-rendering the same grid and seed therefore gives the same ops regardless of earlier calls, in the inspected path. Source: `web/app/map/prng.ts:3-4`, `web/app/map/tileset.ts:70-81`.
- **The web determinism tests are weak for re-render.** `tileset.test.ts:169-172` compares `drawOps` on a shallow copy with `drawOps` on the original, which is close to self-comparison. `tileset.test.ts:174-183` compares `pieces(1)` with `pieces(1)`, so only its inequality with `pieces(2)` carries signal. Neither test calls `drawOps` for seed A, then seed B, then seed A again, so a module-level PRNG that advances between calls would pass both. `render.test.ts:38-67` renders each fixture once and compares its SHA-256 with `render-baselines.json`; no test renders the same map twice.

## Detailed Findings

### 1. API: seed to grid (condition: `Generate` called in-process with a given seed and parameters)

- Endpoint: a null body becomes `GenerateMapRequest(Seed: null)` (`api/Maps/MapEndpoints.cs:48`); generation runs `BspGenerator.Generate(request.Seed ?? NewSeed(), request.ToParameters())` (`:54`). The seed is `uint?` with range validation (`api/Maps/MapModels.cs:53`, `:65`).
- Parameter defaults: `ToParameters()` fills 6 rooms and skirmish; boss size is dropped unless the encounter is a boss (`api/Maps/MapModels.cs:79-84`).
- Per-call PRNG: `var rng = new Prng(seed);` at `api/Maps/BspGenerator.cs:64`. No static mutable state in `api/Maps/`; the only statics are read-only tables (`SizeByRoomCount`, `Directions`, cost constants).
- PRNG: SplitMix64, state held in the instance (`api/Maps/Prng.cs:6-16`). `NextInt` uses multiply-high (`Math.BigMul`) with no modulo (`:19-24`). Consumption sites are `BspGenerator.cs` lines 110, 130, 132, 194, 212, 261, 262, 278, 279; order is fixed by control flow.
- Tie-break: corridor Dijkstra uses `PriorityQueue<int,long>` (`BspGenerator.cs:333`) with the unique key `(cost << 32) | state` (`:449`). The comment at `:445-448` states the dequeue order is fully defined by this code. This is the explicit tie-break that `lessons.md` (Seeded code breaks ties) requires.
- Nondeterminism scan: no `System.Random`, `DateTime`, `Guid`, `Environment` or `Stopwatch` in `api/Maps/`; no `Dictionary`/`HashSet`/`OrderBy` iteration in `api/Maps/`; room and corridor lists are `List<T>` in insertion order. Scope: `api/Maps/*.cs` only.
- Echo: `GeneratedMap(seed, parameters, width, height, cells, rooms)` returns the resolved seed and parameters (`BspGenerator.cs:91`; `MapModels.cs:46`).
- No caching: no `MemoryCache`, `OutputCache`, `ResponseCache`, `IDistributedCache` or `ConcurrentDictionary` under `api/`. Generation does not touch the database (`MapEndpoints.cs:17`) and writes nothing to disk.

### 2. API: the same-seed test and what it proves

- `api.Tests/MapGeneratorTests.cs:89-101`: for each seed 1–200 and two boss settings (none, Huge), generate twice with the same `parameters` object and compare `Cells` and `Rooms`. Each call builds a fresh `Prng`, so the test shows that the generator carries no state between calls within one process.
- Not compared: `Width`, `Height` and `Parameters` are not compared. Only skirmish with 6 rooms and Huge with 6 rooms are exercised.
- Related: `Prng_repeats_its_sequence_for_a_seed_and_stays_in_range` (`MapGeneratorTests.cs:151`) checks two `Prng(42)` instances, same process.
- `MapFixtureTests` (`api.Tests/MapFixtureTests.cs:15-38`) compares against committed files. Under `UPDATE_FIXTURES=1` it rewrites them instead of asserting (`:46-51`), so the variable must stay unset in CI. Line endings are normalised on the file side only (`:55`).

### 3. API: the endpoint round-trip

- `api.Tests/MapEndpointTests.cs:21-49` compares endpoint output with `BspGenerator.Generate(42, ...)` in the same process (`:47`). `:153-184` and `:187-201` check echoed parameters.
- `api.Tests/MapEndpointTests.cs:54-68` asserts two no-seed draws differ (`NotEqual`, `:67`). It does not assert that the echoed seed is in range or that a server-drawn seed reproduces a grid when sent back.
- No test sends the same explicit seed twice to the endpoint and compares the two JSON bodies.

### 4. Web: render path (condition: `drawOps` / `renderMap` called with a `MapGrid` carrying a seed)

- Randomness source: `mulberry32(map.seed)` in `drawOps` (`web/app/map/tileset.ts:70`), consumed only at `tileset.ts:81` for plain floors (`CRACKED_CHANCE` at `:32`). Boss-arena cells get a fixed piece (`:75-77`).
- PRNG: `web/app/map/prng.ts:3-4`, closure over `seed >>> 0`. Comment at `prng.ts:2` forbids `Math.random`.
- `web/app/map/download.ts:4` uses the seed only for the filename, not the pixels.
- `web/app/api/client.ts:45` sends `seed: seed ?? null`; server-side resolution of a null seed was not read in the web scope.
- `Math.random` does not appear in `web/app`, `web/visual`, `web/e2e` or `e2e/` (grep of the inspected paths; lint enforcement not checked).
- Re-render: `home.tsx:94-107` calls `renderMap` in an effect on each map change; each call re-derives the PRNG, so a re-render has no state carried from the earlier one.

### 5. Web: what the tests prove

- `web/app/map/tileset.test.ts:169-172` (`the same map always gives the same ops`): `drawOps` on a shallow copy versus the original. This shows purity for a given fixture; it is close to self-comparison because the copy is structurally identical.
- `web/app/map/tileset.test.ts:174-183` (`floor variants follow the seed, not the call`): asserts `pieces(1)` equals `pieces(1)` and differs from `pieces(2)`, and that both `tiles` and `tiles_cracked` appear. The inequality with seed 2 is the real signal; the equality with itself is near-trivial.
- `web/app/map/render.test.ts:38-67`: one render per fixture, SHA-256 compared with `render-baselines.json`. This pins the image per fixture but does not test re-render.

## Behaviour that would prove protection (derived from the rules, not the code)

From `AGENTS.md` (root line 21-22; `api/AGENTS.md:35`; `web/AGENTS.md:13`) and the PRD (FR-005: regeneration is a new seed, so the same seed must keep meaning the same map for the DM to reproduce what they liked):

1. **Same seed and parameters, separate generations, same grid.** Holds within a process (already tested) and across processes or runtime versions (tested only through the fixture set, for the listed seeds and sizes).
2. **Same seed on the endpoint twice gives the same JSON body** (except the echoed fields, which are equal too). Not tested.
3. **Same seed and grid on a web re-render gives the same ops and pixels, regardless of interleaved renders.** Partly tested: per-call PRNG is verified by reading; the interleaving property is not tested.
4. **Omitted seed: the echoed seed reproduces the same map when sent back.** Not tested.

## Test layer per path (cheapest first)

| Path | Cheapest layer | Status |
|---|---|---|
| Generator, same process | unit (`MapGeneratorTests`) | exists (`:89`), partial parameter coverage |
| Generator, across runs | unit (`MapFixtureTests`) | exists, 5 pinned grids |
| Endpoint, two requests, same seed | integration (`MapEndpointTests`, one `ApiFactory`) | missing: no two-call comparison |
| Endpoint, server-drawn seed round-trip | integration | missing |
| Web `drawOps`, interleaved calls on two seeds | unit (`tileset.test.ts`, pure, no canvas) | missing: interleaving not tested |
| Web `renderMap`, same map rendered twice | web render test (`render.test.ts`) | missing; costlier (atlas, baselines) |
| Visual Playwright re-render | visual | not needed for this property |

## Historical Context (from prior changes)

- `context/foundation/lessons.md` (Seeded code breaks ties explicitly): the `PriorityQueue` tie-break was a past determinism bug (commit a6819dc moved 40–58 cells per fixture). The current explicit key at `BspGenerator.cs:449` is its fix. Supported by the current code.
- `context/foundation/test-plan.md` §6.2 (determinism cookbook) is `TBD — see §3 Phase 2`; this change fills it.
- `context/changes/testing-invariant-boss-sweep/plan.md` (Phase 1, closed out at `1c5717f`) extended the sweep to 1000 seeds. Its determinism theory (`:89`) was intentionally left at 200 seeds ("the determinism and different-seeds tests keep their own budget"), per the §6.1 cookbook.

## Related Research

- `context/changes/testing-invariant-boss-sweep/research.md` — Phase 1 invariant research (`#1`, `#6`). Not re-verified here.

## Open Questions

1. **Cross-process reproduction beyond the fixtures.** Only pinned fixtures compare across runs. Whether a new test should (a) generate in a child process, or (b) rely on fixtures plus a wider fixture set, is a plan decision.
2. **Interleaving for the web.** Whether to add a pure interleave test (`drawOps(A)`, `drawOps(B)`, `drawOps(A)`) or to replace the self-comparisons at `tileset.test.ts:169-183`, is a plan decision. The research recommends adding it, because it is the direct check for a future module-level PRNG and costs milliseconds.
3. **Endpoint omitted-seed round-trip.** Confirm the desired contract: a server-drawn seed sent back should reproduce the same map. Not stated in `AGENTS.md`; read from the endpoint code and the echo contract.
4. **Null seed on the web.** `client.ts:45` sends `null`; the server resolution path was out of scope for the web worker. Read in the plan phase if the endpoint round-trip test needs it.
