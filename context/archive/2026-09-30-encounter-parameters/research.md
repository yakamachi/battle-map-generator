---
date: 2026-09-30T16:15:13+02:00
researcher: Claude (Opus 5.5)
git_commit: 2fc7bda142eba3031df5a7447f07afb11a9677cb
branch: feat/encounter-parameters
repository: yakamachi/battle-map-generator
topic: "What must change for S-03 (room count 2–12, encounter type, boss arena), and what do the parallel-work boundaries with S-04 allow?"
tags: [research, codebase, bsp-generator, map-contract, home-form, fixtures, e2e]
status: partial
last_updated: 2026-09-30
last_updated_by: Claude (Opus 5.5)
---

# Research: S-03 encounter parameters inside the S-04 parallel boundaries

**Date**: 2026-09-30T16:15:13+02:00
**Researcher**: Claude (Opus 5.5)
**Git Commit**: 2fc7bda142eba3031df5a7447f07afb11a9677cb
**Branch**: feat/encounter-parameters
**Repository**: yakamachi/battle-map-generator

## Research Question

What does the codebase do today on every path S-03 touches (request, generator, grid contract, form, fixtures, tests), and which of those changes fit inside the parallel-work boundaries recorded in `change.md` (own: Generate handler, request record, `home.tsx`, `fixtures/grids/*.json`; don't touch: `api/Program.cs`, `web/app/api/client.ts`, `web/app/routes.ts`)?

## Summary

- **Requirement** (`context/foundation/prd.md:104`, `prd.md:139-150`): room count 2–12 (default 6); encounter type skirmish or boss fight; a boss fight adds one arena room, clearly larger than the others and marked as the boss arena, with a floor of at least 8×8 / 10×10 / 12×12 for a Large / Huge / Gargantuan boss. The map has exactly the chosen number of rooms and its size follows from the room count, within the size cap.
- **Nothing of this exists yet.** The request carries only `seed` (`api/Maps/MapModels.cs:24`), the handler always generates 30×20 (`api/Maps/MapEndpoints.cs:21-25`), the generator takes a size and not a room count (`api/Maps/BspGenerator.cs:34`), and the grid has five cell kinds with no arena concept (`MapModels.cs:8-15`).
- **The generator does not control the room count.** Measured on seeds 1–1000 at 30×20: 5 rooms in 8 seeds, 6 in 681, 7 in 160, 8 in 151. "Exactly N rooms" needs an algorithm change, not a parameter.
- **The current generator almost never makes an arena-sized room.** On seeds 1–1000, a room with both sides ≥ 8 appears in 36 seeds at 30×20; both sides ≥ 12 appears in 0 seeds at 30×20 and 2 seeds at 60×60. The arena needs its own placement rule.
- **The `client.ts` boundary collides with the form.** `generateMap(seed?)` sends only `{ seed }` (`web/app/api/client.ts:19-24`), and `home.tsx` calls it with no arguments (`web/app/routes/home.tsx:93`). The form cannot send parameters through that function without editing the file this change must not touch. This needs an owner decision (Open Questions 1).
- **New request fields must be optional in OpenAPI**, or the untouched `client.ts` stops type-checking: today `seed` is listed as `required` (`api/BattleMapGenerator.Api.json:93-95`) and the client passes it explicitly. Whether a defaulted record parameter comes out non-required was not verified (Open Questions 2); this is why the status is `partial`.
- **The default request must still produce 30×20.** `e2e/tests/map.spec.ts:5-6` and `:55-56` pin a 4200×2800 canvas and PNG, and that file must pass unchanged.
- **The size cap does not look like a blocker** for 12 rooms plus a 12×12 arena (inference, not measured with a new algorithm): a 12×12 floor needs a 16×16 leaf (`RoomMargin = 2`, `BspGenerator.cs:20`), the other 11 rooms need at least 7×7 leaves (`MinLeaf`, `BspGenerator.cs:16`), and the current generator already fits 10–20 rooms into 40×30 on seeds 1–1000. The cap is 60×60 (`MapModels.cs:32-33`).

## Detailed Findings

### Request and handler (`api/Maps/`, owned)

- `GenerateMapRequest(uint? Seed)` is the whole request (`MapModels.cs:24`). The body is optional (`MapEndpoints.cs:21`); an empty JSON body and `{}` both return 200, and an existing test pins that (`api.Tests/MapEndpointTests.cs:55-70`). New fields must therefore stay optional with defaults.
- The handler returns `Ok<GeneratedMap>` only (`MapEndpoints.cs:21`). There is no validation path: an out-of-range size makes the generator throw `ArgumentOutOfRangeException` (`BspGenerator.cs:36-45`), which would surface as a 500. A room count outside 2–12 needs a 400, which changes the handler's return type and adds a 400 response to the OpenAPI document.
- `GeneratedMap(Seed, Width, Height, Cells, Rooms)` does not echo the parameters (`MapModels.cs:21`), although `api/AGENTS.md` describes the response as "seed, parameters, width, height, …", and S-01's plan deferred a `parameters` object to S-03 (`context/archive/2026-09-25-first-map-download/plan.md:50`).
- The rate limiter is in `api/Program.cs:26-43` (off limits) and is attached by policy name in `MapEndpoints.cs:17`. S-03 needs no change there.
- JSON enum handling is already global: `MapJson.Configure` adds a camelCase string enum converter (`MapModels.cs:43-49`, wired in `Program.cs:20`), so a new request enum (encounter type, boss size) serialises as camelCase strings without touching `Program.cs`.

### Generator (`api/Maps/BspGenerator.cs`)

- Pipeline: `Split` → `PlaceRooms` → `Connect` → `BuildWalls` (`BspGenerator.cs:47-54`).
- Room count comes from where splitting stops: a leaf stops when neither side reaches `2 * MinLeaf` = 14 (`:59-64`), or by a 1-in-4 roll when both sides are ≤ 16 (`:66-69`). Nothing takes a target count.
- Room size is drawn per leaf between half the available space and all of it (`:109-112`), so a room is as large as its leaf allows only by chance.
- Rooms are the record `Room(X, Y, Width, Height)` (`MapModels.cs:18`) and carry no kind.
- Measured on seeds 1–1000 with the current code (scratch probe, not committed):

  | Size | Room counts seen | Largest room seen | Average time per map |
  | --- | --- | --- | --- |
  | 14×14 | 2–4 | 10×10 | 0.09 ms |
  | 30×20 | 5–8 (6 in 681 seeds) | 12×9 | 0.34 ms |
  | 40×30 | 10–20 | 12×12 | 0.43 ms |
  | 60×60 | 31–53 | 12×12 | 1.46 ms |

  Generation cost at the cap is about 1.5 ms per map on this machine, so CPU on the F1 plan is not the constraint for a larger map; PNG size and canvas area are (below).
- Lesson that applies: seeded code breaks ties explicitly (`context/foundation/lessons.md`, "Seeded code breaks ties explicitly"). Choosing which leaf to split next, or which leaf becomes the arena, is exactly such a choice.

### Grid contract and the arena mark

- `CellKind` is `void | floor | corridor | wall | door` (`MapModels.cs:8-15`, `web/app/api/schema.d.ts:28`). Root `AGENTS.md` lists "boss arena" as an example of a cell kind, and `infrastructure.md:31` does the same.
- Two ways to mark the arena exist in the current shapes: a new cell kind, or a kind on the `Room` record. The web renderer reads only `seed`, `width`, `height` and `cells` (`web/app/map/tileset.ts:22`), so only a cell kind reaches the picture without widening `MapGrid`.
- A new walkable cell kind has these consumers, which all treat kinds as closed sets:
  - `isWalkable` and the floor branch in `web/app/map/tileset.ts:52-54` and `:73-85`; the door rotation checks `=== "floor"` (`tileset.ts:131-136`).
  - Test helper `Grid.IsWalkable` (`api.Tests/MapGeneratorTests.cs:220-221`) and `AssertRoomsAreFloorAndDoNotOverlap`, which asserts every room cell is `Floor` and counts `Floor` cells (`MapGeneratorTests.cs:142-160`).
  - The allowed-kinds list in an existing endpoint test (`MapEndpointTests.cs:39`). It stays true for a default (skirmish) request, so it need not be rewritten.
- `web/app/map/tileset.ts` is neither in the owned list nor in the don't-touch list.

### Web form (`web/app/routes/home.tsx`, owned)

- The view has one row: Generate, Download PNG and the `Seed: N` text (`home.tsx:120-130`). There is no form state.
- The e2e test locates `Generate`, `Download PNG`, the single `canvas` and `/^Seed: \d+$/` (`e2e/tests/map.spec.ts:38-59`); the visual tests locate `Generate` with `exact: true` (`web/visual/home.visual.spec.ts:80`). These names must stay.
- Available primitives are only `alert.tsx` and `button.tsx` (`web/app/components/ui/`). Inputs, labels and selects come through `npx shadcn@latest add` with the `app.css` review described in `web/AGENTS.md`; the UI-contract review already noted that shadcn inputs will be rounder than the buttons (`context/archive/2026-09-29-home-view-ui-contract/reviews/impl-review-phase-2.md:74`).
- A new row above the preview changes the height budget: `map-preview-fit` subtracts a fixed `8.5rem` (`web/app/app.css:86-88`), and `home.tsx:114-115` says to update it when adding a row.
- The empty-state text and the default frame assume 30×20 (`web/app/components/map-preview.tsx:23-24`, `:53`).
- Every home screenshot baseline in `web/visual/__screenshots__/` will change with a new row; they are regenerated only through `npm run visual:update` (`web/AGENTS.md`).

### Client boundary (`web/app/api/client.ts`, off limits)

- `generateMap(seed?: number)` builds the body as `{ seed: seed ?? null }` (`client.ts:19-24`). No other export sends a request.
- `GeneratedMap` and `CellKind` are re-exported from the generated schema (`client.ts:4-5`), so new response fields and cell kinds reach `home.tsx` and `tileset.ts` without editing `client.ts`.
- If a regenerated `schema.d.ts` made any new request property required, `client.ts:23` would fail `npm run typecheck` (CI job `web`).
- `schema.d.ts` and `BattleMapGenerator.Api.json` are generated (`dotnet build api`, then `npm run api:types`), and CI fails on drift (`.github/workflows/ci.yml`, job `contract`). Both branches will regenerate them, so after S-04 or S-03 merges, the other branch regenerates rather than merges.

### Fixtures and pinned outputs

- `MapFixtureTests` pins seeds 1, 42 and 20260925 at the default size (`api.Tests/MapFixtureTests.cs:15-35`) and rewrites them with `UPDATE_FIXTURES=1`.
- Fixture consumers: `web/app/map/render.test.ts:18-21` globs every `fixtures/grids/*.json` and pins a pixel hash per browser in `web/app/map/render-baselines.json`; the visual spec reads `seed-42.json` for its `ready` and `loading-regenerate` shots (`home.visual.spec.ts:56`).
- So any change to what the default request generates for a seed moves three fixtures, their six render baselines and the screenshots. A new fixture file (for example a boss map) is picked up by the render test automatically and needs a baseline per browser.
- `MapGeneratorTests` calls `BspGenerator.Generate(seed, width, height)` in four tests (`MapGeneratorTests.cs:23`, `:43-44`, `:57`, `:74`) and an existing endpoint test calls it too (`MapEndpointTests.cs:49`). Keeping that signature compiling (an overload or defaults) keeps the "don't rewrite existing endpoint tests" rule satisfiable.

### End-to-end and canvas limits

- A full e2e run makes 4 generate calls against a limit of 10 per minute per IP, shared by both browser projects (`e2e/AGENTS.md`, `e2e/playwright.config.ts`). A new spec file has 6 calls of headroom in the same minute, before S-04 changes anything about limits.
- The renderer refuses a side over 16384 px (`web/app/map/render.ts:11`, `:28-30`). At the 60×60 cap the canvas is 8400×8400 px, which passes that check; the comment at `render.ts:8-10` defers an area check for larger maps to S-03. 8400×8400 is 70.6 M pixels, under the roughly 16384² px Chromium area limit that comment cites.
- PNG size at larger maps is unmeasured: a 4200×2800 map is about 700–950 KB (`map.spec.ts:7-8`).

## Code References

- `api/Maps/MapModels.cs:8-24` - cell kinds, `Room`, `GeneratedMap`, `GenerateMapRequest`
- `api/Maps/MapModels.cs:26-38` - default 30×20, cap 60×60, minimum 14×14
- `api/Maps/MapEndpoints.cs:21-25` - Generate handler, fixed default size
- `api/Maps/BspGenerator.cs:57-97` - `Split`, where the room count is decided
- `api/Maps/BspGenerator.cs:99-116` - `PlaceRooms`, where room size is drawn
- `api/BattleMapGenerator.Api.json:92-106` - request schema with `seed` required
- `web/app/api/client.ts:19-24` - the only request builder, seed only
- `web/app/routes/home.tsx:89-100`, `:120-130` - `onGenerate` and the controls row
- `web/app/map/tileset.ts:52-54`, `:73-85`, `:124-138` - closed handling of cell kinds
- `web/app/app.css:86-88` - preview height budget
- `api.Tests/MapFixtureTests.cs:15-35` - pinned seeds and the update switch
- `api.Tests/MapGeneratorTests.cs:142-160`, `:220-221` - invariants that assume `Floor`
- `e2e/tests/map.spec.ts:5-6`, `:38-59` - pinned size and locators
- `web/visual/home.visual.spec.ts:56` - `seed-42.json` as the mocked response

## Architecture Insights

- The OpenAPI document is the contract source and is generated, so contract changes are made in the C# records and regenerated; conflicts with S-04 in the two generated files are resolved by regenerating.
- One fixture set feeds three gates (API pin, render hashes, screenshots); a generator change is cheap to make and wide to re-baseline.
- The home view is a single state machine with one canvas; form state is new, and the parameters used for the map on screen are not stored anywhere yet (S-02's regenerate will need them).

## Historical Context (from prior changes)

- `context/archive/2026-09-25-first-map-download/plan.md:50` - S-01 fixed the size at 30×20 and left parameters and a response `parameters` object to S-03. Supported by the current code.
- `context/archive/2026-09-25-first-map-download/reviews/impl-review-phase-3.md:81-83` - the canvas area check was deferred to S-03. Supported (`render.ts:8-10`).
- `context/archive/2026-09-29-home-view-ui-contract/plan.md:99-101` - Input, Label and Select were deliberately not added; S-03 and S-04 add them through the UI rule. Supported (`components/ui/` holds two files).
- `context/archive/2026-09-29-home-view-ui-contract/reviews/impl-review-phase-3.md:86` - adding a row above the frame makes the map scroll unless the height budget is updated. Supported (`app.css:86-88`).
- `context/foundation/roadmap.md:123` - open question whether 12 rooms with a Gargantuan arena fit the size cap. Partial answer above: likely yes by leaf arithmetic; to be confirmed by tests over seeds once the algorithm exists.

## Related Research

- `context/archive/2026-09-25-first-map-download/research.md` - generator, contract and canvas limits for S-01.
- `context/archive/2026-09-29-home-view-ui-contract/research.md` - tokens and components for the home view.

## Open Questions

1. **How does the form send parameters while `client.ts` is off limits?** (owner decision) Options seen in the code: a new module owned by this change (for example `web/app/api/maps.ts`) that `home.tsx` calls; or a small, agreed signature change in `client.ts`. The first keeps the boundary but leaves two request builders until the branches meet, and S-04's changes to `generateMap` (for example handling 401) would not cover the new one.
2. **Does a record parameter with a default value (`int? RoomCount = null`) come out non-required in the build-time OpenAPI document?** Not verified; the probe build was not run. If it stays required, `client.ts` fails type-checking untouched, and the request shape needs another form (for example non-positional properties). First thing to check in implementation.
3. **Arena mark: cell kind, room kind, or both?** `AGENTS.md` points to a cell kind; only a cell kind changes the picture without widening `MapGrid`.
4. **Does the boss arena count toward the room count?** The PRD says both "exactly as many rooms as chosen" and "a boss fight adds one arena room" (`prd.md:145-148`). Roadmap S-03 reads "one of them is the arena" (`roadmap.md:116`).
5. **Room count → map size table.** Only 6 rooms → 30×20 is fixed (by the e2e test). The rest is a planning choice, bounded by 60×60.
6. **Shared files with S-04 that are in neither list:** `web/app/app.css` (height budget, new tokens from `shadcn add`), `web/app/components/ui/` (both slices add inputs), `web/visual/__screenshots__/` and `web/visual/home.visual.spec.ts`, `web/app/map/render-baselines.json`, `web/app/map/tileset.ts`, `api/AGENTS.md` / `web/AGENTS.md`. Who adds the shared input primitives, and who re-baselines screenshots last?
7. **Does the response echo the parameters?** Needed by S-02's regenerate and described in `api/AGENTS.md`, absent in code.

## Owner decisions (2026-09-30)

Answers from the owner to Open Questions 1, 4 and 6:

- **Q1, request path:** S-03 adds a new module it owns (`web/app/api/maps.ts`) with its own typed request, and `home.tsx` calls it. `client.ts` stays untouched; S-04's changes to `generateMap` are folded in when the branches meet.
- **Q4, arena count:** the arena is one of the N chosen rooms (6 rooms with a boss means 6 rooms, one of them the arena).
- **Q6, shared UI files:** S-03 adds the primitives and `app.css` changes it needs and re-baselines the home screenshots; the branch that merges second rebases and regenerates them.

Questions 2, 3, 5 and 7 remain for `/10x-plan`.
