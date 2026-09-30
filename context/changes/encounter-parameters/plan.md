# Encounter Parameters (S-03) Implementation Plan

## Overview

The DM chooses a room count (2–12, default 6) and an encounter type (skirmish or boss fight, with a boss size). The generator builds exactly that many rooms on a map whose size follows the room count, and a boss fight makes one of the rooms a marked arena with a floor of at least 8×8, 10×10 or 12×12. The parameters travel through the generated contract, the renderer draws the arena differently, and the home view gets the form.

The change runs in parallel with S-04 (login) and stays inside the boundaries recorded in `change.md`.

## Current State Analysis

- The request carries only `seed` (`api/Maps/MapModels.cs:24`); the handler always generates 30×20 (`api/Maps/MapEndpoints.cs:21-25`).
- The generator takes a size, not a room count (`api/Maps/BspGenerator.cs:34`). On seeds 1–1000 at 30×20 it produces 5–8 rooms, and 6 in 681 seeds. A room with both sides ≥ 12 appeared in 0 of those seeds (research.md, "Generator").
- The grid has five cell kinds and rooms carry no kind (`MapModels.cs:8-18`). The response does not echo parameters (`MapModels.cs:21`).
- `web/app/api/client.ts:19-24` sends only `{ seed }` and is off limits. `home.tsx` has one row of controls and no form state (`web/app/routes/home.tsx:120-130`).
- Three fixtures pin the default output (`api.Tests/MapFixtureTests.cs:15`); they feed the render hashes (`web/app/map/render.test.ts:18-21`) and the `ready` screenshots (`web/visual/home.visual.spec.ts:56`).
- `e2e/tests/map.spec.ts:5-6` pins a 4200×2800 canvas and PNG for the default request and must pass unchanged.

## Desired End State

- `POST /api/maps/generate` accepts optional `roomCount`, `encounter` and `bossSize` next to `seed`. An empty body or `{}` still returns a 30×20 map with 6 rooms.
- Every generated map has exactly `roomCount` rooms. On a boss fight, exactly one of them has kind `bossArena`, its floor cells are `bossArena` cells, its floor is at least the boss size's minimum on both sides, and its area is larger than every other room's.
- The response echoes the parameters used.
- The home view shows Rooms, Encounter and (for a boss fight) Boss size controls; Generate sends them; the arena is visibly different on the map and in the PNG.
- `api/Program.cs`, `web/app/api/client.ts` and `web/app/routes.ts` are byte-identical to `main`. Existing methods in `api.Tests/MapEndpointTests.cs` and the file `e2e/tests/map.spec.ts` are unchanged and green.

Verify with: all CI jobs green (`api`, `web`, `visual`, `contract`, `web-tests`, e2e), plus `git diff --exit-code main -- api/Program.cs web/app/api/client.ts web/app/routes.ts e2e/tests/map.spec.ts`.

### Key Discoveries:

- `seed` is `required` in the generated schema (`api/BattleMapGenerator.Api.json:93-95`) and `client.ts:23` passes only `seed`. A new request property that comes out `required` breaks `npm run typecheck` in a file this change may not edit.
- An existing endpoint test calls `BspGenerator.Generate(42, MapSize.DefaultWidth, MapSize.DefaultHeight)` and compares it to the default response (`api.Tests/MapEndpointTests.cs:49`). That three-argument call must keep compiling and must equal the default request's output.
- JSON enums are already camelCase strings globally (`MapModels.cs:43-49`, wired at `Program.cs:20`), so new enums need no `Program.cs` change.
- Cell kinds are handled as closed sets in `web/app/map/tileset.ts:52-54`, `:73-85`, `:131-136` and in `api.Tests/MapGeneratorTests.cs:142-160`, `:220-221`.
- The preview height budget is a fixed `8.5rem` (`web/app/app.css:86-88`) that must grow with a new row (`home.tsx:114-115`).
- Seeded code must break ties in our own code (`context/foundation/lessons.md`, "Seeded code breaks ties explicitly").

## What We're NOT Doing

- No edits to `api/Program.cs`, `web/app/api/client.ts` or `web/app/routes.ts`. No rewriting of existing methods in `api.Tests/MapEndpointTests.cs`. No edits to `e2e/tests/map.spec.ts`.
- No hand-merging of `BattleMapGenerator.Api.json` or `schema.d.ts`; both are regenerated.
- No regenerate button and no "same parameters, new seed" flow (S-02).
- No login, no per-account limits, no change to the rate limiter (S-04).
- No new tileset art and no atlas rebuild; the arena uses a piece already in the atlas.
- No boss-specific layout rules beyond the arena (no throne, no special corridors), no loops (FR-007), no decorations.
- No raising of the 60×60 cap.
- No saving of parameters between visits.

## Implementation Approach

Work from the contract outward, keeping every CI gate green at the end of each phase.

1. **API first.** Add the parameters to the request and response, make the generator target an exact room count and place the arena, and re-pin the default fixtures. The web side only follows with regenerated types and re-baselined hashes and screenshots.
2. **Then the picture.** Teach the renderer the new cell kind and pin a boss fixture.
3. **Then the form.** A new request module owned by this change, the controls, and the screenshots.
4. **Then end to end and docs.**

**Size table** (room count → width × height in squares), keeping roughly 100 squares per room as today's 30×20 for 6:

| Rooms | 2 | 3 | 4 | 5 | 6 | 7 | 8 | 9 | 10 | 11 | 12 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Size | 20×14 | 24×14 | 24×18 | 28×18 | 30×20 | 32×22 | 34×24 | 36×26 | 38×28 | 40×28 | 42×30 |

A boss fight widens the map by the arena's minimum floor side (8, 10 or 12) and raises the height to at least that side plus 4 (the arena's leaf). The largest map is therefore 12 rooms with a Gargantuan boss: 54×30, inside the 60×60 cap, 7560×4200 px on the canvas.

The table values are the contract. If the Phase 1 seed tests show a row cannot always hold its rooms, change that row in Phase 1, keep 6 → 30×20, and record the change in this plan's Phase 1 block.

## Critical Implementation Details

- **Check OpenAPI optionality before anything else in Phase 1.** Add the request fields, run `dotnet build api` and `npm run api:types`, and confirm the new properties are not in `required` and `npm run typecheck` passes with `client.ts` untouched. If a defaulted positional record parameter still comes out required, declare the new fields as non-positional optional properties instead. Do not proceed until this holds.
- **Default output must be one thing.** The default request, `Generate(seed, 30, 20)` and the fixture tests must all produce the same map for a seed, or `MapEndpointTests.cs:49` fails.
- **Merge order with S-04.** Whichever branch merges second rebases, regenerates `BattleMapGenerator.Api.json` and `schema.d.ts`, reruns `npm run visual:update`, and folds S-04's changes to `generateMap` (for example 401 handling) into `web/app/api/maps.ts`.

## Phase 1: Contract and generator

### Overview

The API accepts and echoes the parameters, the generator builds exactly N rooms on a table-sized map with an optional arena, and the default fixtures, render hashes and affected screenshots are re-pinned.

### Changes Required:

#### 1. Request, response and parameter types

**File**: `api/Maps/MapModels.cs`

**Intent**: Carry the encounter parameters in the request, echo them in the response, and give the grid the arena concept. Keep every new request field optional so an empty body, `{}` and the untouched `client.ts` keep working.

**Contract**:
- `CellKind` gains `BossArena` (JSON `bossArena`), appended after `Door`.
- New enums `EncounterType { Skirmish, Boss }`, `BossSize { Large, Huge, Gargantuan }` and `RoomKind { Room, BossArena }`.
- `Room` gains `Kind`.
- New record `MapParameters(int RoomCount, EncounterType Encounter, BossSize? BossSize)`; `GeneratedMap` gains `Parameters`.
- `GenerateMapRequest` gains optional `RoomCount`, `Encounter`, `BossSize`; none may appear in the schema's `required` list.
- `MapSize` gains the room-count limits (2, 12, default 6), the size table and the boss growth rule from "Implementation Approach"; `DefaultWidth`/`DefaultHeight` stay 30×20 and equal the table's row for 6.

#### 2. Generate handler

**File**: `api/Maps/MapEndpoints.cs`

**Intent**: Resolve defaults, validate, and call the generator with parameters. Invalid input is the client's error, not a 500.

**Contract**:
- Defaults: `roomCount` 6, `encounter` skirmish.
- 400 with a validation problem body naming the field when `roomCount` is outside 2–12, or when `encounter` is `boss` and `bossSize` is missing.
- `bossSize` sent with a skirmish is ignored and echoed as `null`.
- The return type gains the 400 result, so the OpenAPI document lists it. The route, its name and `RequireRateLimiting` stay as they are. Generation still never touches the database.

#### 3. Exact room count and arena placement

**File**: `api/Maps/BspGenerator.cs`

**Intent**: Replace "split until leaves are small, stop at random" with "split until there are exactly N leaves", and reserve one leaf large enough for the arena on a boss fight. All existing guarantees (connectivity, walls, doors, one-cell corridors) keep holding by the same construction.

**Contract**:
- `Generate(uint seed, MapParameters parameters)` takes the size from the table.
- `Generate(uint seed, int width, int height, int roomCount = default 6, BossSize? bossSize = null)` stays callable with three arguments; it throws `ArgumentOutOfRangeException` for a size outside the limits and when the size cannot hold the requested rooms.
- The leaf to split next, the arena leaf and any choice among equals use an explicit tie-break in our code (for example by area, then by position), never a library's ordering.
- The arena's floor is at least the boss minimum on both sides and its area exceeds every other room's; other rooms are limited where needed to keep that true.
- Arena floor cells are `BossArena`; its room has `Kind = BossArena`. Corridor and door rules treat arena floor like room floor.

#### 4. Generator and endpoint tests

**File**: `api.Tests/MapGeneratorTests.cs`, `api.Tests/MapEndpointTests.cs`

**Intent**: Pin the new guarantees over many seeds and the new request behaviour, without rewriting the existing endpoint tests.

**Contract**:
- `MapGeneratorTests`: the invariant theory runs over parameter combinations instead of raw sizes: every room count 2–12 for a skirmish and for each boss size, 200 seeds each. It asserts the existing invariants plus: exactly N rooms; map size equals the table; on a boss fight exactly one `BossArena` room meeting the minimum and larger than every other room, with all its cells `BossArena`; on a skirmish no `BossArena` cell or room. `Grid.IsWalkable` and the room-floor assertion learn the new kind. Determinism and "different seeds differ" tests also cover a boss case.
- `MapEndpointTests`: new methods only — parameters are applied and echoed; default request echoes 6 / skirmish / null; `roomCount` 1 and 13 return 400; boss without `bossSize` returns 400; an unknown enum string returns 400; skirmish with `bossSize` returns 200 with `bossSize` null. Each builds its own factory, as the file's header comment requires.

#### 5. Fixtures, generated contract and web baselines

**File**: `fixtures/grids/seed-1.json`, `seed-42.json`, `seed-20260925.json`, `api/BattleMapGenerator.Api.json`, `web/app/api/schema.d.ts`, `web/app/map/render-baselines.json`, `web/visual/__screenshots__/`

**Intent**: The algorithm change moves every seed's map and the response gains fields, so everything pinned to the old output is regenerated in this commit.

**Contract**: fixtures via `UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests`; OpenAPI via `dotnet build api`; types via `npm run api:types`; render hashes via `UPDATE_BASELINES=1 npm test`; screenshots via `npm run visual:update`. No hand edits.

### Success Criteria:

#### Automated Verification:

- New request properties are absent from `required` in `api/BattleMapGenerator.Api.json`
- API tests pass: `dotnet test api.Tests`
- No contract drift after `dotnet build api` and `npm run api:types`: `git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts`
- Web type-check and build pass with `client.ts` untouched: `npm run typecheck && npm run build` in `web/`
- Web rendering tests pass: `npm test` in `web/`
- Visual gate passes: `npm run visual:docker` in `web/`
- Existing end-to-end test passes unchanged: `npm run build:app && npm test` in `e2e/`
- Boundary files are identical to main: `git diff --exit-code main -- api/Program.cs web/app/api/client.ts web/app/routes.ts e2e/tests/map.spec.ts`

#### Manual Verification:

- The three regenerated default fixtures, viewed in the running app, look like sensible 6-room maps
- A boss request sent by hand (12 rooms, Gargantuan) returns a 54×30 map in well under a second

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review encounter-parameters phase 1` before Phase 2. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Arena in the picture

### Overview

The renderer draws `bossArena` cells as a visibly different floor, and a boss fixture pins both the grid and its image.

### Changes Required:

#### 1. Cell-to-sprite mapping

**File**: `web/app/map/tileset.ts`, `web/app/map/tileset.test.ts`

**Intent**: Treat the arena as walkable room floor for walls, corners and doors, and give it its own look from the existing atlas.

**Contract**:
- `bossArena` is walkable and counts as room floor wherever `floor` does today, including the door-bar rotation.
- Every `bossArena` cell draws the `tiles_decorative` piece.
- Plain `floor` cells no longer draw `tiles_decorative` as a random variant; the cracked variant stays, still derived from the seed.
- Tests cover: arena cells map to the decorative piece; walls and doors around an arena match those around a plain room of the same shape; no plain floor cell gets the decorative piece.

#### 2. Boss fixture

**File**: `api.Tests/MapFixtureTests.cs`, `fixtures/grids/seed-42-boss-huge.json`, `web/app/map/render-baselines.json`

**Intent**: Pin one boss map end to end: the API's grid and the image both browsers draw from it.

**Contract**: a second fixture theory pins seed 42, 6 rooms, boss fight, Huge (40×20) under that file name. The existing default theory is unchanged. The render test picks the file up through its glob and needs a hash per browser.

#### 3. Re-baseline what the mapping change moves

**File**: `web/app/map/render-baselines.json`, `web/visual/__screenshots__/`

**Intent**: Removing the decorative variant from plain floors changes the default fixtures' images.

**Contract**: regenerated with `UPDATE_BASELINES=1 npm test` and `npm run visual:update`.

### Success Criteria:

#### Automated Verification:

- API tests pass, including the boss fixture: `dotnet test api.Tests`
- Web mapping and rendering tests pass in both browsers: `npm test` in `web/`
- Web type-check passes: `npm run typecheck` in `web/`
- Visual gate passes: `npm run visual:docker` in `web/`
- Atlas is unchanged: `npm run atlas && git diff --exit-code app/map/tileset/` in `web/`

#### Manual Verification:

- In a rendered boss map the arena is clearly distinguishable from the other rooms at preview size

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review encounter-parameters phase 2` before Phase 3.

---

## Phase 3: Parameters form

### Overview

The DM sets the parameters in the home view and Generate sends them.

### Changes Required:

#### 1. Request module owned by this change

**File**: `web/app/api/maps.ts` (new)

**Intent**: Send the parameters without editing `client.ts`. It mirrors `generateMap`'s result and error handling so `home.tsx` needs no new error cases.

**Contract**: exports a function taking `{ roomCount, encounter, bossSize? , seed? }` and returning the existing `GenerateResult` type (imported as a type from `client.ts`). 429 maps to `rate-limited`, a thrown fetch to `network`, anything else non-2xx (including 400) to `http` with its status. A header comment says it is to be merged with `client.ts`'s `generateMap` once S-04 has landed.

#### 2. Form primitives

**File**: `web/app/components/ui/label.tsx`, `web/app/components/ui/native-select.tsx` (new), `web/app/app.css`

**Intent**: Add the two primitives the form needs through the UI rule in `web/AGENTS.md`, styled by tokens only.

**Contract**: added with `npx shadcn@latest add label native-select`, or copied by hand the way `button.tsx` was if the registry lacks one. Their radius matches the buttons. Any `.dark` block or `@custom-variant` the CLI writes is removed; any new variable is added to both `:root` and the dark media block with its contrast recorded in `context/changes/encounter-parameters/tokens.md`.

#### 3. The form

**File**: `web/app/routes/home.tsx`

**Intent**: Three labelled controls above the buttons, with state that feeds the request.

**Contract**:
- "Rooms": a select of 2–12, default 6. "Encounter": "Skirmish" (default) or "Boss fight". "Boss size": "Large", "Huge", "Gargantuan", default Large, rendered only when the encounter is a boss fight.
- Generate calls the new module with the current values; controls are disabled while busy.
- The button names `Generate` and `Download PNG`, the single canvas and the `Seed: N` text stay exactly as they are.
- A default-state Generate sends a request whose result is the same 30×20 map shape as today.
- Views rules hold: token classes and primitives only (`npm run ui:scan`).

#### 4. Preview frame and canvas guard

**File**: `web/app/app.css`, `web/app/components/map-preview.tsx`, `web/app/map/render.ts`, `web/app/map/render.test.ts`

**Intent**: Keep the map on screen without scrolling now that a row was added and sizes vary, and close the area check S-01 deferred to S-03.

**Contract**:
- `map-preview-fit`'s height budget grows by the form row's height, on desktop and mobile.
- The empty-state text no longer names a fixed map size.
- `renderMap` also refuses a canvas whose area exceeds a named limit under Chromium's cap; a test covers it. The largest S-03 map (7560×4200 px) passes.

#### 5. Screenshots

**File**: `web/visual/home.visual.spec.ts`, `web/visual/__screenshots__/`

**Intent**: Pin the form in every existing state and add the boss states.

**Contract**: existing tests are kept; new tests are added for the form with "Boss fight" selected (boss size visible) and for a ready boss map using `seed-42-boss-huge.json`. Baselines regenerated with `npm run visual:update`.

### Success Criteria:

#### Automated Verification:

- Type-check and build pass: `npm run typecheck && npm run build` in `web/`
- UI scan passes: `npm run ui:scan` in `web/`
- Web tests pass, including the area guard: `npm test` in `web/`
- Visual gate passes with the new baselines: `npm run visual:docker` in `web/`
- Existing end-to-end test passes unchanged: `npm run build:app && npm test` in `e2e/`
- Boundary files are identical to main: `git diff --exit-code main -- api/Program.cs web/app/api/client.ts web/app/routes.ts e2e/tests/map.spec.ts`

#### Manual Verification:

- On desktop and at 390 px wide, the form, buttons and the whole preview fit without the map scrolling, in light and dark themes
- Keyboard only: every control is reachable, shows visible focus, and Boss size appears and disappears with the encounter type
- A 12-room Gargantuan boss map renders and downloads in Chrome and Firefox

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review encounter-parameters phase 3` before Phase 4.

---

## Phase 4: End to end and docs

### Overview

The parameters flow is tested against the real host in both browsers, and the agent docs describe the new contract.

### Changes Required:

#### 1. New end-to-end spec

**File**: `e2e/tests/parameters.spec.ts` (new)

**Intent**: Prove the whole path with non-default parameters, within the rate limit shared with `map.spec.ts`.

**Contract**: one test, one generate call per browser (6 calls per full run in total, limit 10 per minute). It selects 8 rooms, "Boss fight" and "Huge" by label, clicks Generate, and asserts: the observed (not mocked) response has 8 rooms with exactly one of kind `bossArena` and echoes the parameters; the canvas is 44×24 squares at 140 px (6160×3360); the downloaded PNG has the same dimensions. Locators are by role, label and visible text.

#### 2. Agent docs

**File**: `api/AGENTS.md`, `web/AGENTS.md`, `e2e/AGENTS.md`

**Intent**: Keep the rules next to the code true.

**Contract**: `api/AGENTS.md` — the request parameters, validation, the size table's location, the arena mark, and the second fixture theory. `web/AGENTS.md` — `bossArena` mapping, `app/api/maps.ts` and its planned merge into `client.ts`, the new primitives. `e2e/AGENTS.md` — the call count ("today 6 calls") and the new spec.

### Success Criteria:

#### Automated Verification:

- End-to-end tests pass in Chromium and Firefox: `npm run build:app && npm test` in `e2e/`
- End-to-end type-check passes: `npm run typecheck` in `e2e/`
- `e2e/tests/map.spec.ts` is identical to main: `git diff --exit-code main -- e2e/tests/map.spec.ts`
- Full API and web suites still pass: `dotnet test api.Tests` and `npm test` in `web/`

#### Manual Verification:

- After the pull request's CI run, all required checks are green
- The three docs read correctly against the code

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human. Then run `/10x-impl-review encounter-parameters phase 4`.

---

## Testing Strategy

### Unit Tests:

- Generator invariants for every room count 2–12 × {skirmish, Large, Huge, Gargantuan} × 200 seeds: exact room count, table size, arena minimum and dominance, plus all existing guarantees.
- Determinism and distinctness for skirmish and boss.
- Three-argument `Generate` still rejects sizes outside the limits.
- `drawOps` mapping for `bossArena` and the removed decorative variant.
- Canvas area guard.

### Integration Tests:

- Endpoint: parameters applied and echoed, defaults, each 400 case, ignored `bossSize`.
- Fixtures: three default grids and one boss grid, each drawn and hashed in both browsers.
- Visual: every existing home state with the form, plus the two boss states.
- End to end: default flow unchanged; boss flow with 8 rooms and a Huge boss.

### Manual Testing Steps:

1. Generate with defaults; confirm a 6-room 30×20 map.
2. Set 2 rooms, then 12 rooms; confirm the counts and that the preview stays on screen.
3. Choose Boss fight with each boss size; confirm one visibly different, largest room.
4. Download a 12-room Gargantuan map in Chrome and Firefox and open the PNG.
5. Repeat step 3 at 390 px width and in dark mode.

## Performance Considerations

- Generation at 60×60 measured about 1.5 ms per map with the current algorithm; the largest S-03 map is 54×30. The new splitting is no heavier than the old. CPU on the F1 plan is not the constraint.
- The largest canvas is 7560×4200 px (31.8 M pixels), under the 16384 px side cap and the area cap. Its PNG is larger than today's 700–950 KB; it is produced in the browser and never crosses the hosting plan's bandwidth.
- The boss fixture is 40×20 rather than the largest map, to keep pixel hashing in the render tests fast.

## Migration Notes

- No stored data. A seed now produces a different map than before this change, which the project's rules allow; fixtures are re-pinned deliberately.
- Merge with S-04: see "Merge order with S-04" above.

## References

- Related research: `context/changes/encounter-parameters/research.md`
- Requirement: `context/foundation/prd.md:104`, `prd.md:139-150`; roadmap `context/foundation/roadmap.md:114-125`
- Fixture pattern: `api.Tests/MapFixtureTests.cs:15-35`
- Request builder to mirror: `web/app/api/client.ts:19-34`
- Primitive pattern: `web/app/components/ui/button.tsx`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Contract and generator

#### Automated

- [x] 1.1 New request properties are absent from `required` in `api/BattleMapGenerator.Api.json` — 8b348ba
- [x] 1.2 API tests pass: `dotnet test api.Tests` — 8b348ba
- [x] 1.3 No contract drift after `dotnet build api` and `npm run api:types` — 8b348ba
- [x] 1.4 Web type-check and build pass with `client.ts` untouched — 8b348ba
- [x] 1.5 Web rendering tests pass: `npm test` in `web/` — 8b348ba
- [x] 1.6 Visual gate passes: `npm run visual:docker` in `web/` — 8b348ba
- [x] 1.7 Existing end-to-end test passes unchanged — 8b348ba
- [x] 1.8 Boundary files are identical to main — 8b348ba

#### Manual

- [x] 1.9 The three regenerated default fixtures, viewed in the running app, look like sensible 6-room maps — 8b348ba
- [x] 1.10 A boss request sent by hand (12 rooms, Gargantuan) returns a 54×30 map in well under a second — 8b348ba

### Phase 2: Arena in the picture

#### Automated

- [x] 2.1 API tests pass, including the boss fixture: `dotnet test api.Tests` — 11b46d2
- [x] 2.2 Web mapping and rendering tests pass in both browsers: `npm test` in `web/` — 11b46d2
- [x] 2.3 Web type-check passes: `npm run typecheck` in `web/` — 11b46d2
- [x] 2.4 Visual gate passes: `npm run visual:docker` in `web/` — 11b46d2
- [x] 2.5 Atlas is unchanged — 11b46d2

#### Manual

- [x] 2.6 In a rendered boss map the arena is clearly distinguishable from the other rooms at preview size

### Phase 3: Parameters form

#### Automated

- [x] 3.1 Type-check and build pass: `npm run typecheck && npm run build` in `web/`
- [x] 3.2 UI scan passes: `npm run ui:scan` in `web/`
- [x] 3.3 Web tests pass, including the area guard: `npm test` in `web/`
- [x] 3.4 Visual gate passes with the new baselines: `npm run visual:docker` in `web/`
- [x] 3.5 Existing end-to-end test passes unchanged
- [x] 3.6 Boundary files are identical to main

#### Manual

- [x] 3.7 On desktop and at 390 px wide, the form, buttons and the whole preview fit without the map scrolling, in light and dark themes
- [x] 3.8 Keyboard only: every control is reachable, shows visible focus, and Boss size appears and disappears with the encounter type
- [x] 3.9 A 12-room Gargantuan boss map renders and downloads in Chrome and Firefox

### Phase 4: End to end and docs

#### Automated

- [ ] 4.1 End-to-end tests pass in Chromium and Firefox
- [ ] 4.2 End-to-end type-check passes: `npm run typecheck` in `e2e/`
- [ ] 4.3 `e2e/tests/map.spec.ts` is identical to main
- [ ] 4.4 Full API and web suites still pass

#### Manual

- [ ] 4.5 After the pull request's CI run, all required checks are green
- [ ] 4.6 The three docs read correctly against the code
