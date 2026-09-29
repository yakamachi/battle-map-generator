# First Map Download Implementation Plan

## Overview

Roadmap slice S-01 (`context/foundation/roadmap.md`). The DM clicks "Generate". The API returns a seeded BSP dungeon (rooms, corridors, walls, doors) as a semantic grid. `web/` draws it in the Kenney *Scribble Dungeons* style as a preview aligned to the grid, and the DM downloads a PNG at exactly 140 px per square that matches the preview, in Chrome and Firefox. The first phase also moves the project to a pull-request workflow: tests gate merges into `main`, and a merge deploys.

## Current State Analysis

- **API** (`api/Program.cs`, 127 lines):
  - `AddOpenApi()` at `:15`, but the document is mapped only in Development (`:57-60`).
  - Identity services are registered but no endpoints exist yet (`:34-36`, "login endpoints come with S-04").
  - The DbContext registration tolerates a missing connection string (`:17-32`).
  - The health probes are at `:84-100`, and the `/api/{**rest}` 404 guard at `:103` comes before the SPA fallback (`:104-107`).
  - There is no generator, PRNG, grid type, rate limiter or JSON enum configuration.
- **API tests:** `api.Tests/` (xUnit, Testcontainers, `ApiFactory`: Testing environment, in-memory config, a stub web root) holds 15 tests from F-01.
- **Web:**
  - `web/react-router.config.ts:6` sets `ssr: false`, and `web/vite.config.ts:12-13` proxies `/api` to `localhost:5108`.
  - The only route renders the starter `<Welcome />` (`web/app/routes/home.tsx`).
  - The scripts are `build`, `dev`, `start` and `typecheck`, with no test tooling.
  - `web/public/` holds only the favicon.
- **CI:** `.github/workflows/deploy.yml` runs on every push to `main`: build web, `Test API`, the migration bundle, publish, then deploy, the firewall rule, migrations and the `live`/`ready` smoke tests. There is no workflow for pull requests, and `main` is unprotected (direct pushes are the norm today).
- **Hosting limits:** App Service F1 allows 60 CPU-min a day and 165 MB of outbound traffic a day; exceeding either returns 403 until midnight UTC (`infrastructure.md:92`). Chromium's canvas area limit is about 16 384² px, roughly 117×117 squares at 140 px.
- **Research:** `context/changes/first-map-download/research.md`, which includes the tileset follow-up and the correction about the SVG sheet.

## Desired End State

- Every change reaches `main` through a pull request. The `CI` workflow runs API tests, web type checks, web rendering tests (Chromium and Firefox), an OpenAPI contract drift check and Playwright e2e (Chromium and Firefox). `main` requires those checks, requires the branch to be up to date, and blocks direct pushes, admins included. A merge triggers `deploy.yml`, which no longer runs tests.
- `POST /api/maps/generate` (optional body `{ seed }`) returns `{ seed, width, height, cells[], rooms[] }`:
  - The map is a 30×20 BSP dungeon, deterministic for a given seed.
  - Cells are the strings `void`, `floor`, `corridor`, `wall` and `door`, stored row by row (`index = y * width + x`).
  - The endpoint is limited to 10 requests per minute per client IP (429 with `Retry-After`) and never touches the database.
- The web home screen has "Generate" and "Download PNG" buttons:
  - The preview is the 140 px-per-square canvas, scaled down with CSS.
  - The download is `battle-map-<seed>.png` at exactly `width×140` by `height×140` px, byte-for-byte the canvas the DM sees.
- Fixture grids in `fixtures/grids/` are pinned by the API tests and rendered by the web tests.
- **How to verify:** a green `CI` run on each phase's pull request, a green deploy after each merge, and a map generated and downloaded on production in both browsers.

### Key Discoveries:

- Build-time OpenAPI generation (`Microsoft.Extensions.ApiDescription.Server` with `OpenApiGenerateDocumentsOnBuild`) **runs `Program.cs` through a mock server**. That is safe here because startup needs no configuration (`api/Program.cs:17-32`). Output is `{ProjectName}.json` in `OpenApiDocumentsDirectory`, and .NET 10 defaults to OpenAPI 3.1 (Context7 `/dotnet/aspnetcore.docs`).
- JSON options set with `ConfigureHttpJsonOptions` also shape the OpenAPI schema, so `JsonStringEnumConverter` turns enums into a string `enum` list. MVC's `AddJsonOptions` would not.
- The rate limiter's default `RejectionStatusCode` is **503**, so it must be set to 429. Partitioning by IP on App Service requires forwarded headers. The documented switch is `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, which enables the middleware and clears `KnownNetworks` and `KnownProxies`.
- The lesson "Only work that needs the database may touch it" (`context/foundation/lessons.md`) applies: generation must work with an unreachable database.
- The lesson "API tests control their own configuration" applies: new endpoint tests use `ApiFactory`.
- Scribble Dungeons is CC0 and top-down, with edge walls, doors and 128 px PNG pieces. Its SVG sheet is not on a grid (research correction).
- Vitest 5.0.2 supports Vite 8 (`@vitest/browser-playwright` 5.0.2). Playwright is 1.63.0, `openapi-typescript` 7.13.0 and `openapi-fetch` 0.17.0; the last two support OpenAPI 3.1.

## What We're NOT Doing

- **Parameters:** no S/M/L sizes and no encounter type (S-03). The size is a fixed 30×20, the request carries only an optional `seed`, and there is no `parameters` object in the response yet. S-03 adds both.
- **Regeneration:** no "regenerate" user experience (S-02). "Generate" always draws a new seed, and the API already accepts a fixed seed, so S-02 stays small.
- **Login:** no login, and generation is public until S-04. The rate limit is the only guard (`roadmap.md:155`).
- **Stale tabs:** no `contractVersion` handling. The risk is accepted for two users (`infrastructure.md:177`), and `index.html` is served `no-cache`.
- **Content:** no decorations, props, stairs, traps or wall variants beyond plain walls and doors, and no boss-arena cells.
- **Scope of storage and output:** no saving maps, no printing across several pages, no server-side rendering, no `canvas-size` library (the 60×60 cap stays far below browser limits).
- **Cleanup:** the leftover SSR dependencies (`@react-router/node`, `@react-router/serve`, `isbot`) are not removed.
- **Reviews:** PRs don't require an approving review (solo project; 0 required approvals).

## Implementation Approach

The contract comes first, as `infrastructure.md:193` advises, and every phase ships through its own PR.

1. Phase 1 sets up the PR gate, so every later phase is tested before it merges.
2. Phase 2 builds the API side end to end: PRNG, generator, fixtures, endpoint, rate limit and the generated OpenAPI document.
3. Phase 3 builds the web client from that document, the Scribble atlas and mapping, and the preview screen with browser rendering tests.
4. Phase 4 adds the PNG download and the Playwright e2e tests in both browsers, then brings the docs up to date.

Work happens on a branch (`first-map-download`), with one PR per phase. Merging a phase deploys it.

## Critical Implementation Details

- **Branch protection order.** GitHub can require only check names it has already seen. Phase 1's PR must run `CI` once before protection is switched on; the owner then enables protection and merges that PR through it. Each later phase that adds a CI job (`contract`, `web-tests`, `e2e`) extends the list of required checks after its first green run.
- **Build-time OpenAPI runs the app.** Anything added to startup must not need configuration or a database while the document is generated. The generated `api/BattleMapGenerator.Api.json` is committed, and CI fails when a fresh build produces a different file, or when regenerating the TypeScript types does.
- **Seed range.** Seeds are uint32 (0–4 294 967 295), so they stay exact as JavaScript numbers. A server-drawn seed comes from `RandomNumberGenerator`, the only randomness outside our own PRNG. The algorithm itself uses the PRNG only.
- **Pixel-exact rendering.** The atlas script pre-renders every piece at exactly 140 px **and** in all 4 rotations. The renderer only ever calls `drawImage` with a 1:1 source and destination at integer multiples of 140, with smoothing off, and never uses `rotate` or `scale`. The output is then a pixel copy of the atlas, apart from alpha blending of layered pieces, which engines may round differently. Baselines are therefore kept per browser; Chromium and Firefox producing the same hash is reported, not required.
- **Preview = export.** One canvas is sized `width×140` by `height×140` in bitmap pixels and scaled for display with CSS. The download is `toBlob` of that same canvas, so no `devicePixelRatio`, zoom or second render path can make the two differ.
- **Fixture updates.** `UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests` rewrites `fixtures/grids/*.json`. Any algorithm change must update the fixtures and the web render baselines in the same commit (`AGENTS.md:18`).

## Phase 1: Pull-Request Workflow and CI Gate

### Overview

Changes reach `main` only through pull requests that pass `CI`. `deploy.yml` stops running tests and still builds, migrates, deploys and smoke-tests.

### Changes Required:

#### 1. CI workflow

**File**: `.github/workflows/ci.yml` (new)

**Intent**: Run the test suite on every pull request to `main`, so that anything merged has passed.

**Contract**:
- Workflow `CI`: `on: pull_request` (branches `[main]`) and `workflow_dispatch`, `permissions: contents: read`, concurrency per PR with `cancel-in-progress: true`.
- Job `api`: `ubuntu-latest`, timeout 20 min, `setup-dotnet` from `global.json`, then `dotnet test api.Tests -c Release`.
- Job `web`: timeout 15 min, Node 24, and in `web/`: `npm ci`, `npm run typecheck`, `npm run build`.
- Later phases add the `contract`, `web-tests` and `e2e` jobs.

#### 2. Deploy without tests

**File**: `.github/workflows/deploy.yml`

**Intent**: Deploy faster. Tests now gate the merge rather than the deploy (user decision; this reverses F-01 Phase 2's "a failing test blocks the deploy").

**Contract**: Remove the `Test API` step and its comment. The build, the migration bundle, the firewall, the migrations, the deploy and both smoke tests stay unchanged, and the trigger stays `push` to `main`. Add a one-line comment that tests run in `ci.yml` and `main` is protected. The `build` job gets `if: github.ref == 'refs/heads/main'`, so a manual `workflow_dispatch` from another branch can't deploy untested code.

#### 3. Branch protection on `main` (owner-approved)

**File**: none (GitHub repository setting)

**Intent**: Make "merged means tested" true: no untested code reaches `main`, not even from the admin.

**Contract**: Once `CI` has run on the Phase 1 PR, set `main`'s protection through `gh api -X PUT repos/<owner>/<repo>/branches/main/protection`:
- required status checks, `strict: true`, contexts `api` and `web`;
- `enforce_admins: true`;
- required pull request reviews with 0 approvals;
- `allow_force_pushes: false`, `allow_deletions: false`.

The owner approves the command before it runs.

**Prerequisite**: the pending planning files (`CLAUDE.md`, `context/foundation/roadmap.md`, `context/changes/first-map-download/`) are committed to `main` before protection is switched on; after that, they too must go through a PR.

#### 4. Documentation of the new flow

**File**: `AGENTS.md` (Deployment, Testing), `context/foundation/infrastructure.md` (CI/CD and preview line `:154`, operational story)

**Intent**: Future agents and readers should know that work goes through PRs, `ci.yml` is the gate and `deploy.yml` deploys without tests.

**Contract**: Update the existing Deployment/Testing wording, and replace the statement that "pull-request builds run tests" with the real setup (`ci.yml`, the required checks, the protection settings). Record the F-01 reversal in one sentence. State that every change, including `context/` docs and the chore commits that close or archive a change, goes through a PR. State also that `ci.yml` deliberately has no `paths`/`paths-ignore` filters: a required check that never reports would block docs-only PRs forever.

### Success Criteria:

#### Automated Verification:

- Both workflows parse and `deploy.yml` no longer runs tests: `python3 -c "import yaml;[yaml.safe_load(open(f)) for f in ['.github/workflows/ci.yml','.github/workflows/deploy.yml']]"` succeeds, and `grep -c "dotnet test" .github/workflows/deploy.yml` prints 0
- API tests still pass locally: `dotnet test api.Tests`
- Web still type-checks and builds: `npm --prefix web run typecheck && npm --prefix web run build`

#### Manual Verification:

- The Phase 1 PR shows the `api` and `web` checks green
- `gh api repos/<owner>/<repo>/branches/main/protection` shows required checks `api` and `web` with `strict: true`, `enforce_admins: true`, 0 required approvals, force pushes and deletions disabled
- After the merge, the `deploy.yml` run is green with no `Test API` step, and the smoke tests pass

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Grid Contract and BSP Generator (API)

### Overview

The API gets a deterministic generator and a public, rate-limited endpoint that returns the semantic grid. Fixed-seed fixtures pin it, and a build-time OpenAPI document describes it.

### Changes Required:

#### 1. Seeded PRNG

**File**: `api/Maps/Prng.cs` (new)

**Intent**: The project's own small deterministic generator. `System.Random` is not allowed (`AGENTS.md:21`, `api/AGENTS.md:12`).

**Contract**: A PCG32 or SplitMix64 implementation seeded from a `uint` seed, exposing `NextInt(minInclusive, maxExclusive)`. It is passed explicitly through the algorithm, with no static or ambient state. The same seed always gives the same sequence.

#### 2. Grid model

**File**: `api/Maps/MapModels.cs` (new)

**Intent**: The semantic contract shared with `web/`: what each cell *is*, never how it looks.

**Contract**:
- `enum CellKind { Void, Floor, Corridor, Wall, Door }`, serialised as camelCase strings.
- `record Room(int X, int Y, int Width, int Height)`.
- `record GeneratedMap(uint Seed, int Width, int Height, CellKind[] Cells, Room[] Rooms)`, where `Cells` is row-major (`index = y * Width + x`).
- `record GenerateMapRequest(uint? Seed)`.
- Constants: default size 30×20 and a maximum of 60×60.

#### 3. BSP generator

**File**: `api/Maps/BspGenerator.cs` (new)

**Intent**: A pure function from `(seed, width, height)` to a playable layout of rooms joined by corridors, walled in and aligned to the grid.

**Contract**: `GeneratedMap Generate(uint seed, int width, int height)`. It throws `ArgumentOutOfRangeException` above 60×60 or below a minimum usable size. It guarantees:
- the edge rows and columns are never walkable (`floor`, `corridor` or `door`);
- all walkable cells are connected by 4-neighbour moves;
- every walkable cell's 8 neighbours are walkable or `wall`, so walls enclose everything;
- rooms don't overlap;
- every room has at least one `door`;
- each `door` has walkable neighbours on exactly two opposite sides and `wall` on the other two, so its orientation is unambiguous;
- the same seed and size produce identical output.

Corridors are 1 cell wide. Internal parameters (minimum leaf size, room margins, corridor shape) are the implementer's choice within these guarantees.

#### 4. Endpoint, JSON options, rate limit

**File**: `api/Maps/MapEndpoints.cs` (new), `api/Program.cs`

**Intent**: Expose generation publicly and cheaply, protected against a stuck client exhausting F1's CPU budget.

**Contract**:
- `POST /api/maps/generate`: the body `GenerateMapRequest` is optional. It returns `TypedResults.Ok<GeneratedMap>` for the default size; the seed is the request's, or a fresh one from `RandomNumberGenerator`.
- The endpoint is registered with `WithName("GenerateMap")` and `RequireRateLimiting("generate")`. It is mapped after the health probes and before the `/api/{**rest}` guard (lesson "Unknown /api routes return 404").
- `ConfigureHttpJsonOptions` adds `JsonStringEnumConverter` with camelCase naming.
- `AddRateLimiter` defines the policy `generate`: a fixed window partitioned by `RemoteIpAddress?.ToString() ?? "unknown"` (TestServer leaves the address null, and a null partition key throws), 10 requests per minute with no queue, answering 429 with a `Retry-After` header. `UseRateLimiter()` is added to the pipeline.
- The endpoint never resolves `AppDbContext`.

#### 5. Build-time OpenAPI document

**File**: `api/BattleMapGenerator.Api.csproj`, `api/BattleMapGenerator.Api.json` (generated, committed)

**Intent**: Make the contract source available at build time for `web/` (`api/AGENTS.md:15`, `infrastructure.md:180`).

**Contract**: Add `Microsoft.Extensions.ApiDescription.Server` 10.0.x (`PrivateAssets=all`) with `OpenApiGenerateDocumentsOnBuild=true` and `OpenApiDocumentsDirectory=$(MSBuildProjectDirectory)`. `dotnet build api` writes `api/BattleMapGenerator.Api.json` with the `GenerateMap` operation and the `CellKind` string enum. `MapOpenApi()` stays Development-only.

#### 6. Fixtures and tests

**File**: `fixtures/grids/seed-1.json`, `fixtures/grids/seed-42.json`, `fixtures/grids/seed-20260925.json` (new); `api.Tests/MapGeneratorTests.cs`, `api.Tests/MapFixtureTests.cs`, `api.Tests/MapEndpointTests.cs` (new)

**Intent**: Pin the guarantees and the exact output for known seeds, and prove the endpoint's security and resource behaviour.

**Contract**:
- `MapGeneratorTests`: every guarantee above holds for 200 consecutive seeds; generation is deterministic; different seeds give different maps; sizes above 60×60 throw.
- `MapFixtureTests`: for each fixture file, generating its seed at the default size yields exactly the serialised JSON (the same serializer options as the endpoint). With `UPDATE_FIXTURES=1` the test rewrites the files instead. It finds the repo root by walking up to `global.json`.
- `MapEndpointTests` (through `ApiFactory`). Limiter counters live in each factory's app, so the 429 test uses its own `ApiFactory`, and no other factory makes more than 10 generate calls (lesson "API tests … never share counted state"):
  - 200 with the expected shape and string cell kinds, echoing the given seed;
  - the 11th request inside a minute returns 429 with `Retry-After`;
  - generation succeeds with an **unreachable** connection string (the database lesson);
  - an unknown `/api/maps/*` path returns 404.
- `api/BattleMapGenerator.Api.http` gains a generate request.

### Success Criteria:

#### Automated Verification:

- API builds and emits the contract: `dotnet build api` succeeds, and `grep -q '"GenerateMap"' api/BattleMapGenerator.Api.json`
- The committed contract is current: `dotnet build api && git diff --exit-code api/BattleMapGenerator.Api.json`
- All API tests pass: `dotnet test api.Tests`

#### Manual Verification:

- Before the merge, `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set as an App Service app setting (owner-approved `az webapp config appsettings set`). It is harmless without the limiter, and otherwise all production traffic would share the front end's IP as one 10/min bucket
- The Phase 2 PR is green and merged; the deploy run is green
- On production, `curl -X POST https://<app>/api/maps/generate -H 'content-type: application/json' -d '{"seed":42}'` returns a 30×20 map identical to `fixtures/grids/seed-42.json`
- On production, 11 rapid calls from one machine give 429 on the 11th; a call from a different network (for example a phone hotspot) in the same minute still gets 200; and 11 calls from one machine, each with a different `X-Forwarded-For: 203.0.113.<i>` header, still give 429 on the 11th (the limit can't be bypassed by spoofing the header; impl-review F2)
- Generation cost is negligible: 1 000 local generations take under 1 s in total (a timing loop recorded in the PR description)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Web Client, Scribble Atlas and Preview

### Overview

`web/` calls the generated client, maps the semantic grid to Scribble pieces, and shows the 140 px render as a preview. Rendering is tested in real Chromium and Firefox against the fixtures.

### Changes Required:

#### 1. Tooling and scripts

**File**: `web/package.json`, `web/package-lock.json`, `web/vitest.config.ts` (new), `web/.gitignore`

**Intent**: Add the contract client, browser test runner and atlas tooling.

**Contract**:
- Dependency `openapi-fetch` 0.17.x.
- Dev dependencies: `openapi-typescript` 7.13.x, `vitest` and `@vitest/browser-playwright` 5.0.x, `playwright`, `sharp` (atlas script only).
- Scripts:
  - `api:types`: `openapi-typescript ../api/BattleMapGenerator.Api.json -o app/api/schema.d.ts`
  - `test`: `vitest run`
  - `atlas`: `node scripts/build-scribble-atlas.mjs`
- Vitest runs in browser mode, headless, with instances `chromium` and `firefox`.
- `vitest.config.ts` is standalone: it doesn't reuse `vite.config.ts`, because the `reactRouter()` plugin doesn't run under Vitest. It sets `server.fs.allow: [".."]`, since the root `fixtures/grids/` is outside the Vite root and `web/` has no workspace marker. Tests load fixtures with `import.meta.glob("../../../fixtures/grids/*.json", { eager: true })`.
- `.gitignore` gains the Vitest and Playwright output directories.

#### 2. Generated types and client

**File**: `web/app/api/schema.d.ts` (generated, committed), `web/app/api/client.ts` (new)

**Intent**: The API contract as compile-time types; drift becomes a type error (`AGENTS.md:15`).

**Contract**: `createClient<paths>({ baseUrl: "" })`, which uses relative `/api` URLs (`web/AGENTS.md`), plus a typed `generateMap(seed?)` helper that returns the map or a typed error (429, network).

#### 3. Scribble atlas

**File**: `web/tileset-src/scribble/*.png` plus `License.txt` (vendored CC0 source pieces, 128 px, only the ones used); `web/scripts/build-scribble-atlas.mjs` (new); `web/app/map/tileset/scribble-atlas.png` and `scribble-atlas.json` (generated, committed)

**Intent**: One hashed atlas image (`web/AGENTS.md:16`) with every piece at exactly 140 px, so the renderer only copies pixels.

**Contract**:
- The script resamples each source piece to 140×140 with a high-quality filter (`sharp`, lanczos3) and emits the 0°, 90°, 180° and 270° rotations of wall and door pieces.
- It packs them on a 140 px grid and writes the JSON index `pieceName@rotation → {x, y}`.
- The atlas PNG carries no colour metadata (no `iCCP`, `gAMA`, `sRGB` or `cHRM` chunks), so no browser colour-manages it.
- The atlas is imported from `app/`, so Vite content-hashes it.
- Pieces used: `tiles`, `tiles_cracked`, `tiles_decorative`, `tile`, `wall`, `wall_corner`, `wall_edge`, `inner_round`, `door_closed`.

#### 4. Map module: PRNG, tileset mapping, renderer

**File**: `web/app/map/prng.ts`, `web/app/map/tileset.ts`, `web/app/map/render.ts` (new)

**Intent**: Turn semantic cells into draw operations (pure and testable), then draw them. This is the edge-wall model from `research.md` ("Model B").

**Contract**:
- `prng.ts`: mulberry32 seeded from the map seed. `Math.random` is not allowed.
- `tileset.ts`: `drawOps(map): DrawOp[]` with `DrawOp = { piece, rotation, cellX, cellY }`, following the rules in `research.md` (Model B):
  - `void` and `wall` cells get no floor; the renderer paints them as blank paper.
  - `floor` cells get `tiles`, with seeded rare `tiles_cracked` or `tiles_decorative` variants.
  - `corridor` cells get `tile`.
  - Each walkable cell gets a `wall` strip, rotated per side, on every side facing a non-walkable cell; `wall_corner` or `wall_edge` where two adjacent sides are closed; `inner_round` for an inner corner (orthogonal sides open, diagonal closed).
  - `door` cells get `door_closed`, rotated along the wall line.
  - The output is deterministic for a given map.
- `render.ts`:
  - `loadAtlas()` returns an `ImageBitmap` created with `{ colorSpaceConversion: "none", premultiplyAlpha: "none" }`.
  - `renderMap(ctx, map, atlas)` sets the canvas to `width×140` by `height×140`, turns smoothing off, fills the paper colour and draws each op 1:1 at `(cellX×140, cellY×140)`.
  - It throws if either dimension exceeds 16 384 px.

#### 5. Home screen

**File**: `web/app/routes/home.tsx`; remove `web/app/welcome/`

**Intent**: A minimal "Generate" flow with the preview.

**Contract**:
- A "Generate" button calls `generateMap()`.
- While loading, the button is disabled.
- On success the map is rendered into a canvas, displayed at `max-width: 100%` with height auto, and its seed is shown.
- On 429 the screen says to wait a minute; on a network or other error it shows a readable message.
- The page `meta` gets a real title.

#### 5a. Rendering rule update

**File**: `web/AGENTS.md` (Map rendering, `:13-14`), `context/foundation/infrastructure.md` (`:32`)

**Intent**: The rule text matches the one-canvas design as soon as the renderer lands, so no agent reads the old "separate preview size" rule.

**Contract**: Render once at 140 px per square into one canvas; the preview is that canvas scaled with CSS; the download is `toBlob` of the same canvas. Replace "shares the render function, not the canvas" at `infrastructure.md:32`.

#### 6. Rendering tests and CI jobs

**File**: `web/app/map/tileset.test.ts`, `web/app/map/render.test.ts` (new); `.github/workflows/ci.yml`

**Intent**: Pin the mapping and the exact image for each fixture in both browsers (`web/AGENTS.md:22`), and fail CI on contract drift.

**Contract**:
- `tileset.test.ts`, on small hand-made grids:
  - door orientation;
  - wall strips on every side facing rock;
  - inner corners;
  - all ops on integer cells;
  - the same map always gives the same ops.
- `render.test.ts`, for each `fixtures/grids/*.json`:
  - the canvas is exactly `width×140` by `height×140`;
  - the SHA-256 of `getImageData` equals the committed value for the current browser in `web/app/map/render-baselines.json` (keyed `fixture → { chromium, firefox }`);
  - the pixels are not all paper;
  - whether the Chromium and Firefox hashes are equal is logged, not asserted.
- With `UPDATE_BASELINES=1`, `render.test.ts` writes the current browser's hashes instead of asserting (through a Vitest browser command, since the page can't write files). Baselines are generated headless, as CI runs them, and updated in the same commit as any fixture or atlas change.
- CI gains a `contract` job: build the API, run `npm run api:types`, then `git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts`.
- CI gains a `web-tests` job: `npm ci`, `npx playwright install --with-deps chromium firefox`, `npm test`.
- Both are added to the required checks after their first green run.

### Success Criteria:

#### Automated Verification:

- Types and build pass: `npm --prefix web run typecheck && npm --prefix web run build`
- Rendering and mapping tests pass in Chromium and Firefox: `npm --prefix web test`
- Generated contract files are current: `dotnet build api && npm --prefix web run api:types && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts`
- The atlas is reproducible: `npm --prefix web run atlas && git diff --exit-code web/app/map/tileset/`

#### Manual Verification:

- Locally (`dotnet run --project api` and `npm --prefix web run dev`), "Generate" shows a 30×20 Scribble map in Chrome and Firefox: walls sit on cell edges, doors cross the wall line in both orientations, corridors and rooms are distinguishable, and nothing is shifted or cut off
- Rapid clicking shows the "wait a minute" message after the 10th map
- The Phase 3 PR shows `api`, `web`, `contract` and `web-tests` green; after the merge, the deploy is green and production shows the same screen

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: PNG Download and End-to-End Tests

### Overview

The DM downloads the preview as a PNG. Playwright proves the whole flow in Chromium and Firefox, and the docs describe the finished slice.

### Changes Required:

#### 1. Download

**File**: `web/app/routes/home.tsx`, `web/app/map/download.ts` (new)

**Intent**: Save exactly what the DM sees (FR-006).

**Contract**:
- `downloadCanvas(canvas, seed)` calls `canvas.toBlob(…, "image/png")`; a `null` blob becomes an error message and never an empty file.
- It creates an object URL and a temporary `<a download="battle-map-<seed>.png">` appended to the document, clicks it, removes it, and revokes the URL on the next tick.
- The button is enabled only while a rendered map is on screen.

#### 2. Playwright e2e

**File**: `web/playwright.config.ts`, `web/e2e/map.spec.ts` (new); `web/package.json` (`@playwright/test` 1.63.x; scripts `e2e:prepare`, which builds the web app and copies `build/client` into `api/wwwroot`, and `e2e`)

**Intent**: Prove generate, preview and download against the real .NET host serving the built SPA, in both required browsers.

**Contract**:
- Projects `chromium` and `firefox`.
- `webServer` runs `dotnet run --project ../api --urls http://localhost:5108`, readiness at `/api/health/live`, with `reuseExistingServer: !process.env.CI`. No database is needed.
- The spec:
  - open `/` and click "Generate";
  - wait for the canvas and assert (in the page) that its bitmap is 4200×2800 and not uniform;
  - click "Download PNG"; the download's `suggestedFilename()` matches `battle-map-\d+\.png`;
  - the saved file's PNG header reports width 4200 and height 2800, and the file is larger than a blank-image threshold.

#### 3. CI e2e job

**File**: `.github/workflows/ci.yml`

**Intent**: Run the e2e tests on every PR.

**Contract**: Job `e2e`: set up .NET and Node, `npm ci`, `npm run e2e:prepare`, `npx playwright install --with-deps chromium firefox`, then `npm run e2e`. On failure it uploads the Playwright report. It is added to the required checks after its first green run.

#### 4. Documentation

**File**: `web/AGENTS.md`, `api/AGENTS.md`, `AGENTS.md`, `web/CLAUDE.md`, `api/CLAUDE.md`, `context/foundation/infrastructure.md`

**Intent**: The rules match what now exists.

**Contract**:
- `web/AGENTS.md` (the rendering rule is already updated in Phase 3): Scribble atlas, CC0, and the script; the mapping lives in `tileset.ts`; the test commands.
- `api/AGENTS.md`: generator location, fixtures and the update command, the rate limit and forwarded headers, the build-time OpenAPI file.
- Root `AGENTS.md`: the `fixtures/grids/` path.
- The two `CLAUDE.md` files: current test commands.
- `infrastructure.md` risk register: mark the mitigations now in place (rate limit, cap, OpenAPI at build time, Playwright in Firefox).

### Success Criteria:

#### Automated Verification:

- E2E passes in both browsers: `npm --prefix web run e2e:prepare && npm --prefix web run e2e`
- Unit and rendering tests still pass: `npm --prefix web test`
- Type check and API tests still pass: `npm --prefix web run typecheck && dotnet test api.Tests`

#### Manual Verification:

- On production in Chrome and in Firefox: Generate, then Download PNG. The file is 4200×2800, matches the preview, and at 100% zoom the walls sit exactly on square edges
- Optional: the PNG imported into Roll20 at 30×20 units lines up with Roll20's grid
- The Phase 4 PR shows `api`, `web`, `contract`, `web-tests` and `e2e` green, and all five are required on `main`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- **C#:** the PRNG's determinism; the generator's guarantees over 200 seeds; the size limits.
- **TypeScript (in real browsers):** the mapping rules on hand-made grids (door orientation, wall strips, inner corners, variant determinism).

### Integration Tests:

- **Fixture pinning:** the API reproduces `fixtures/grids/*.json` exactly; the web renders the same files to a committed pixel hash in Chromium and Firefox.
- **Endpoint:** shape, string enums, seed echo, 429 on the 11th request, works with an unreachable database, and unknown paths return 404.
- **E2E:** Playwright in Chromium and Firefox against the .NET host serving the built SPA: generate, preview, download, then check the PNG's dimensions and that it isn't blank.

### Manual Testing Steps:

1. Generate a few maps locally in both browsers and inspect the walls, doors and alignment.
2. Hit the rate limit and read the message.
3. On production after each merge, generate and download a map in both browsers.
4. Check the rate limit from two networks.

## Performance Considerations

- **Generation:** a 30×20 BSP is microseconds to milliseconds of work. The rate limit (10/min per IP) bounds CPU against F1's 60 CPU-min per day.
- **Payload:** about 600 cells of short strings, a few KB of JSON per map.
- **Atlas:** around 25 pieces at 140 px, several of them in 4 rotations, one PNG of a few hundred KB. It's served content-hashed and immutable (`api/Program.cs:66-81`), so each browser downloads it once, which matters for the 165 MB/day budget.
- **Canvas:** 4200×2800 is about 47 MB of bitmap memory, well within desktop limits. The 60×60 cap (8400 px per side) stays below Chromium's 16 384 px per side.

## Migration Notes

- Database: none. The endpoint doesn't touch it.
- Seeds: a seed's map stays stable only while the algorithm is unchanged (`AGENTS.md:18-19`). Changing the generator means regenerating the fixtures and render baselines in the same commit.
- Rollback: redeploy an earlier commit. Nothing is persisted.

## References

- Research: `context/changes/first-map-download/research.md`, including the tileset follow-up and correction
- Roadmap: `context/foundation/roadmap.md` (S-01, Open Roadmap Question #4)
- Rules: `AGENTS.md`, `api/AGENTS.md`, `web/AGENTS.md`, `context/foundation/lessons.md`
- Risks: `context/foundation/infrastructure.md:92`, `:164`, `:173-180`, `:193`
- Prior change: `context/archive/2026-09-23-account-store-foundation/` (`ApiFactory`, CI patterns)
- Tileset: https://kenney.nl/assets/scribble-dungeons (CC0)

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Pull-Request Workflow and CI Gate

#### Automated

- [x] 1.1 Both workflows parse and `deploy.yml` no longer runs tests — 5c4c76f
- [x] 1.2 API tests still pass locally: `dotnet test api.Tests` — 5c4c76f
- [x] 1.3 Web still type-checks and builds — 5c4c76f

#### Manual

- [x] 1.4 The Phase 1 PR shows the `api` and `web` checks green — 5c4c76f
- [x] 1.5 `main` protection shows required checks `api`/`web` (strict), enforce_admins, 0 approvals, no force pushes or deletions — 5c4c76f
- [x] 1.6 After the merge, the deploy run is green with no `Test API` step, and the smoke tests pass — 2436304

### Phase 2: Grid Contract and BSP Generator (API)

#### Automated

- [x] 2.1 API builds and emits the contract with the `GenerateMap` operation — 58d4ce6
- [x] 2.2 The committed contract is current (`git diff --exit-code api/battle-map-generator-api.json`) — 58d4ce6
- [x] 2.3 All API tests pass: `dotnet test api.Tests` — 58d4ce6

#### Manual

- [x] 2.4 `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` is set on App Service before the merge (owner-approved) — 58d4ce6
- [x] 2.5 The Phase 2 PR is green and merged; the deploy run is green — 87b08be
- [x] 2.6 Production `generate` with seed 42 returns a 30×20 map identical to `fixtures/grids/seed-42.json` — 87b08be
- [x] 2.7 Production rate limit: 429 on the 11th call from one machine, 200 from a different network, and still 429 with spoofed `X-Forwarded-For` headers — 87b08be
- [x] 2.8 1 000 local generations take under 1 s in total (recorded in the PR) — 58d4ce6

### Phase 3: Web Client, Scribble Atlas and Preview

#### Automated

- [x] 3.1 Types and build pass — 554232a
- [x] 3.2 Rendering and mapping tests pass in Chromium and Firefox: `npm --prefix web test` — 554232a
- [x] 3.3 Generated contract files are current (API document and `schema.d.ts`) — 554232a
- [x] 3.4 The atlas is reproducible (`npm run atlas` leaves no diff) — 554232a

#### Manual

- [x] 3.5 Locally, Generate shows a correct, aligned 30×20 Scribble map in Chrome and Firefox — 554232a
- [x] 3.6 Rapid clicking shows the "wait a minute" message after the 10th map — 554232a
- [x] 3.7 The Phase 3 PR shows `api`, `web`, `contract` and `web-tests` green; deploy and production screen OK — 554232a

### Phase 4: PNG Download and End-to-End Tests

#### Automated

- [x] 4.1 E2E passes in both browsers — 4aa5271
- [x] 4.2 Unit and rendering tests still pass: `npm --prefix web test` — 4aa5271
- [x] 4.3 Type check and API tests still pass — 4aa5271

#### Manual

- [x] 4.4 Production in Chrome and Firefox: the downloaded PNG is 4200×2800, matches the preview, and walls sit on square edges — 4aa5271
- [x] 4.5 Optional: the PNG lines up with Roll20's grid at 30×20 units — 4aa5271 (grid lines up; scale in Roll20 was off, deferred)
- [x] 4.6 The Phase 4 PR shows all five checks green, and all five are required on `main` — 4aa5271
