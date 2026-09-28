---
date: 2026-09-25T10:21:45+02:00
researcher: Claude (Opus 5.5) for Karol Mitek
git_commit: 71e4eefd583fe7dcb7a93be5d010c9bb869f021c
branch: main
repository: battle-map-generator
topic: "first-map-download (S-01): what the codebase already provides, what binds the slice, and what is missing, from generator to PNG download"
tags: [research, codebase, api, web, map-generation, rendering, ci, first-map-download]
status: complete
last_updated: 2026-09-25
last_updated_by: Claude (Opus 5.5)
last_updated_note: "Added a tileset follow-up (comparison with links, perspective check, draft semantic-cell to tile mappings); it supersedes the 0x72 recommendation made during planning"
---

# Research: first-map-download (S-01)

**Date**: 2026-09-25T10:21:45+02:00
**Researcher**: Claude (Opus 5.5) for Karol Mitek
**Git Commit**: 71e4eef (uncommitted local changes: `CLAUDE.md`, `context/foundation/roadmap.md`, this folder)
**Branch**: main
**Repository**: battle-map-generator

## Research Question

"Let's dig deep into this slice. It is quite extensive, so let's do proper research before digging in." Roadmap S-01 (`context/foundation/roadmap.md:88-100`): the DM clicks "generate", sees a grid-aligned map of rooms and corridors in one fixed default size and type, and downloads a PNG at 140 px per square that matches the preview, in Chrome and Firefox.

This document covers the **codebase** side: the current state, binding rules, prior decisions and gaps. The rendering-library decision and the browser API reference come from the earlier external research, kept under [External research](#external-research-exa--context7-2026-09-25).

## Summary

- **Both sides start from scratch.** The inspected tree has no generator, PRNG, grid type, generate endpoint, map UI, tileset asset, test project, test runner, Playwright config or CI test step (details and anchors below). S-01 builds all of these.
- **What exists and can be reused:**
  - An API host with a fixed route order: health probes, then the `/api/{**rest}` 404 guard, then the SPA fallback (`api/Program.cs:84-107`).
  - A Vite dev proxy from `/api` to `http://localhost:5108` (`web/vite.config.ts:12-13`).
  - SPA mode (`web/react-router.config.ts:6`).
  - Cache headers, already set so that hashed `/assets` files can be cached for a long time (`api/Program.cs:66-81`).
  - A deploy workflow that builds `web/`, copies it into `wwwroot` and publishes the API (`.github/workflows/deploy.yml:28-45`).
- **Binding rules are fully written down, but the numbers are not.** A seeded PRNG, a semantic-grid JSON contract, a rate limit, a map-size cap, an OpenAPI document at build time, shared fixture grids, the 140 px offscreen export and Playwright in Chromium and Firefox are all required (`AGENTS.md:16`, `AGENTS.md:21`, `api/AGENTS.md:11-15`, `web/AGENTS.md:14-23`). None of the documents fixes the default map size, the cap value, the rate-limit policy or the tileset asset.
- **Three conflicts the plan must resolve:**
  1. F-01 Phase 2 plans to create the same `api.Tests/` project that S-01 needs (`context/changes/account-store-foundation/plan.md:156-158`). Phase 2 has not landed (`plan.md:385-386` unchecked; `api.Tests/` absent).
  2. Both candidate tilesets are 16 px (`context/foundation/shape-notes.md:203-204`). 140 / 16 = 8.75, which is not a whole number, so this collides with the "crisp at 140 px" goal. This is an inference from the external research; see Open Questions.
  3. CI runs no tests on push, and there is no workflow triggered by pull requests (`.github/workflows/deploy.yml:3-6`). Yet `infrastructure.md:154` says pull-request builds run tests.
- **The slice is wide,** as the roadmap itself warns (`roadmap.md:99`). If the plan splits it, the roadmap's rule is to split by "preview" and "download", not by layer.

## Detailed Findings

### API (`api/`)

- **Route order in `api/Program.cs` (full read, 127 lines):**
  - OpenAPI is registered at `:15`, but mapped only inside `IsDevelopment()` (`:57-60`).
  - `UseHttpsRedirection` is at `:62`, static files with cache headers at `:66-81`, and the health probes at `:84` (live) and `:87-100` (ready, gated by a header key).
  - The `/api/{**rest}` 404 guard is at `:103` and `MapFallbackToFile("index.html")` at `:104-107`.
  - A new map endpoint group belongs between the health block (`:100`) and the guard (`:103`), so the guard stays the last `/api` mapping (`context/foundation/lessons.md:26-31`).
- **No auth is wired up.** Identity services are registered at `Program.cs:34-36`, with the comment "login endpoints come with S-04". In `api/`, a grep found no `UseAuthentication`, `UseAuthorization`, `[Authorize]` or `RequireAuthorization`. A new endpoint is therefore public unless S-01 adds gating. The roadmap's default is a request rate limit in S-01, not a login (`roadmap.md:155`).
- **Rate limiting:** a grep for `AddRateLimiter`, `UseRateLimiter` and `RateLimiter` across `api/` and `context/` found no code and no policy sketch. Only the requirement exists (`api/AGENTS.md:14`, `infrastructure.md:164`, `infrastructure.md:193`).
- **OpenAPI at build time:** absent. `api/battle-map-generator-api.csproj:11-19` lists 6 package references, all 10.0.12. `Microsoft.Extensions.ApiDescription.Server` is not among them, and the csproj sets no `OpenApiGenerateDocuments*` property. This gap is flagged at `api/AGENTS.md:15`, `infrastructure.md:131` and `infrastructure.md:180`.
- **JSON settings:** a grep for `JsonSerializerOptions`, `JsonStringEnumConverter`, `ConfigureHttpJsonOptions` and `AddJsonOptions` found nothing in `api/`. Framework defaults apply. That means cell-kind enums would serialize as numbers unless the plan adds a string-enum converter. This is framework-default knowledge, not repo evidence.
- **Tests:**
  - There is no `*.Tests.csproj`, no `.sln` or `.slnx`, and no xUnit, NUnit or MSTest reference anywhere in the repo.
  - `api/CLAUDE.md` says no test project exists yet.
  - `public partial class Program;` (`Program.cs:127`) is there for `WebApplicationFactory`, but nothing uses it.
- **Local dev:**
  - HTTP profile: `http://localhost:5108` (`api/Properties/launchSettings.json:8`).
  - `appsettings.Development.json` holds the SQL Server connection string and the health key, and is kept out of publish (`csproj:24`).
  - `api/battle-map-generator-api.http` has requests only for the health endpoints.
  - SDK `10.0.100` with `rollForward: latestFeature` (`global.json`). The only local tool is `dotnet-ef` 10.0.12 (`.config/dotnet-tools.json`).
- **Inference: the generate endpoint and Playwright may not need SQL Server.** Nothing in S-01 needs the database. The connection string is resolved lazily (`Program.cs:21-32`), the Data Protection hosted service is removed (`Program.cs:47-48`), and `/api/health/live` runs no checks (`Program.cs:84`). The API may therefore start and serve generation without the container. This is not verified by running it; the plan should confirm it before designing CI for Playwright.
- **Stray folder:** `api/context/changes/` exists on disk. It is empty, untracked and outside the 10x context structure. Harmless, but a candidate for cleanup.

### Web (`web/`)

- **SPA mode:** `ssr: false` (`web/react-router.config.ts:6`), with no `prerender` or `basename` setting.
- **Dev proxy:** `/api` goes to `http://localhost:5108` (`web/vite.config.ts:12-13`), which matches the API's HTTP profile. The plugins are `tailwindcss()` and `reactRouter()`, plus `tsconfigPaths` (`vite.config.ts:6-9`).
- **Versions (lockfile):** react and react-dom 19.3.0, react-router and @react-router/* 8.4.0, vite 8.3.0, typescript 5.9.3, tailwindcss 4.3.3.
- **Scripts:** `build`, `dev`, `start` and `typecheck` only (`web/package.json:5-10`). There's no test script.
- **Leftover SSR dependencies** are still installed (`@react-router/node`, `@react-router/serve`, `isbot`; `package.json:12-17`). `web/CLAUDE.md` says `npm start` is a dead SSR leftover.
- **Test and lint tooling:** vitest, jest, playwright, ESLint, Prettier and Biome are absent from `web/package.json` and `web/package-lock.json`, and `web/CLAUDE.md` confirms it.
- **UI:**
  - There is one route, `index("routes/home.tsx")` (`web/app/routes.ts`).
  - `home.tsx` renders the starter `<Welcome />` splash (`web/app/welcome/welcome.tsx`), which the generate screen will replace.
  - The app has no `clientLoader` or `clientAction` yet. The bundled React Router skill names these as the browser-side data exports for this mode (`web/.agents/skills/react-router/references/framework-mode.md:88-91`).
- **TypeScript:** `strict: true`, `verbatimModuleSyntax: true`, alias `~/*` → `./app/*`, types `node` and `vite/client` (`web/tsconfig.json`).
- **Assets:**
  - `web/public/` holds only `favicon.ico`.
  - No png, atlas, sprite or tileset file exists in the repo outside `node_modules`.
  - Files in `public/` are copied without a content hash. A hashed atlas therefore has to be imported from `app/`, the way `welcome.tsx` already imports its SVG logos. That produces `build/client/assets/<name>-<hash>.png` and satisfies `web/AGENTS.md:16`.
- **Contract tooling:** no OpenAPI client generator (openapi-typescript, orval, etc.) and no fixtures directory anywhere in the repo.

### CI, deploy and infrastructure

- **`deploy.yml` (78 lines):**
  - It runs on push to `main` and on `workflow_dispatch` (`:3-6`), on `ubuntu-latest`, with Node 24 (`:22-26`) and .NET from `global.json` (`:35-37`).
  - Web step: `npm ci && npm run typecheck && npm run build` (`:28-33`). Then web is copied into `api/wwwroot` (`:39-42`), the API is published (`:44-45`), and the deploy job runs with a smoke test of `/api/health/live` (`:76-78`).
  - The workflow has no `dotnet test`, no web unit tests and no Playwright step. It's the only file in `.github/workflows/`.
- **F1 quotas:**
  - 60 CPU minutes a day with at most 3 minutes per 5 minutes, and 165 MB of outbound traffic a day. Going over either returns 403 for the whole app until midnight UTC (`infrastructure.md:92`).
  - The app unloads after 20 minutes idle (`infrastructure.md:68`).
  - The file gives no memory figure for F1.
- **Risk-register rows that bind S-01:**
  - Quota: client-side rendering, a rate limiter, a map-size cap, and "measure CPU-seconds per generation locally" (`:164`).
  - Grid-alignment guardrail (`:173`).
  - The preview must match the download, with Playwright checking the download's dimensions and that it isn't blank (`:174`).
  - Firefox canvas export can be blocked or randomized under strict tracking protection (`:175`).
  - Canvas size limits, with the fix "cap map size in the API" (`:179`).
  - No OpenAPI document at build time (`:180`).
- **Sequencing advice:** "Set up the contract before the generator": PRNG, response fields, shared fixture grids, OpenAPI at build time, then the rate limit and size cap (`infrastructure.md:193`).
- **Stale tabs:** send a `contractVersion` in every response and prompt a reload on mismatch (`infrastructure.md:132`). No code exists for this.
- **Ignores:**
  - `web/.gitignore` doesn't ignore `playwright-report/` or `test-results/`.
  - `api/.gitignore:40-41` ignores `wwwroot/` (CI fills it). Its template entries already cover `TestResults/`.
- **Local services:** `compose.yaml` runs only SQL Server, on `127.0.0.1:1433`.

### Product rules and prior decisions

- **PRD scope for S-01:**
  - US-01, FR-003 (BSP rooms and corridors, grid-aligned) and FR-004 (preview), at `prd.md:104-111`.
  - FR-006 (download), at `prd.md:117`.
  - An NFR on generation time: at most 5 minutes, "clearly faster" as the goal (`prd.md:122-124`), plus the Chrome and Firefox NFR (`prd.md:126`).
  - The grid guardrail (`prd.md:65-68`).
- **Wording mismatch:** FR-004 says the preview is shown "jako PNG" ("as PNG", `prd.md:108`). `web/AGENTS.md:14` makes the preview a canvas render and only the download a PNG. The ownership split (`AGENTS.md:12-14`) forbids a PNG rendered by the server. Treat `web/AGENTS.md` as the precise rule.
- **Grid contract:**
  - The response holds the seed, parameters, width, height, a row-major semantic grid and the room list (`api/AGENTS.md:11`).
  - Cell kinds are semantic, for example floor, wall, door, corridor and boss arena (`AGENTS.md:16`).
  - The PRNG is our own SplitMix64 or PCG, passed through the code explicitly (`api/AGENTS.md:12`; `AGENTS.md:21`).
- **Fixed default size and type for S-01:** owned by the user and non-blocking (`roadmap.md:97`). What S, M and L mean is deferred to S-03 (`prd.md:192-196`).
- **Tileset:** "free CC0, e.g. 0x72's 16x16 Dungeon Tileset or Kenney 'Tiny Dungeon'" (`shape-notes.md:203-204`), not decided. The PRD's non-goals allow one tileset in the MVP.
- **Tests stated in the shaping notes:** an API integration test for generation plus Playwright e2e (`shape-notes.md:205-206`). The root `AGENTS.md:30` lists e2e coverage of "login, generate, regenerate and download". Login belongs to S-04 and regeneration to S-02, so S-01's e2e covers generate and download only (inference from the roadmap's slice boundaries).

## Code References

- `api/Program.cs:15`, `:57-60`: OpenAPI registered, and mapped only in Development
- `api/Program.cs:34-36`: Identity services, with no endpoints until S-04
- `api/Program.cs:66-81`: static files, with long-lived cache for `/assets` and `no-cache` for everything else
- `api/Program.cs:84-100`: health probes; the new endpoint group goes after them
- `api/Program.cs:103-107`: the `/api` 404 guard, then the SPA fallback (must stay last)
- `api/Program.cs:127`: `public partial class Program;` for `WebApplicationFactory`
- `api/battle-map-generator-api.csproj:11-19`: package references (no ApiDescription.Server)
- `api/Properties/launchSettings.json:8`: the API's dev URL, `http://localhost:5108`
- `web/react-router.config.ts:6`: `ssr: false`
- `web/vite.config.ts:12-13`: `/api` dev proxy to 5108
- `web/app/routes.ts`, `web/app/routes/home.tsx`, `web/app/welcome/welcome.tsx`: the starter screen that gets replaced
- `.github/workflows/deploy.yml:28-45`: build, copy and publish (no test steps)
- `context/foundation/infrastructure.md:92`, `:164`, `:173-175`, `:179-180`, `:193`: quotas and the S-01 risk rows
- `context/changes/account-store-foundation/plan.md:156-158`: the planned `api.Tests/` (xUnit, Mvc.Testing, Testcontainers.MsSql), earmarked for S-01's tests too

## Architecture Insights

- **Contract-first order** is the documented intent (`infrastructure.md:193`):
  1. PRNG and response DTOs
  2. OpenAPI emitted at build time
  3. Fixture grids
  4. The BSP generator filling the contract
  5. The endpoint, with a rate limit and cap
  6. The web client, render and download
- **Fixture grids are the join point between the two apps.** The API tests pin them, and the web rendering tests use them (`web/AGENTS.md:22`, `api/AGENTS.md:13`). Both must change in the same commit (`AGENTS.md:18`). No shared location exists yet; the plan picks one that both test runners can read.
- **Determinism crosses the contract.** The web side has to derive variant randomness from the response seed (`AGENTS.md:22`), so `web/` needs its own small seeded PRNG in TypeScript as well. The C# PRNG doesn't need to match it bit for bit unless the plan wants that.
- **The F1 quota shapes the endpoint.** Generation should be cheap and bounded, with CPU cost measured locally (`infrastructure.md:164`). The rate limiter protects against a stuck client, not against abuse at scale.
- **Conventions to follow (from F-01):**
  - A plan in phases, where each phase lists automated checks as commands and manual checks as expected observations, and pauses at a manual gate (`account-store-foundation/plan.md:142`, `:202`, `:266`, `:322`).
  - A `## Progress` checklist that records the commit SHA for each step (`plan.md:363-421`).
  - Commits scoped to the change id, e.g. `feat(<change-id>): … (pN)`.
  - Inline comments that say why (`Program.cs`, throughout).
  - Review findings from F-01 worth carrying over: loopback-only dev ports, and no Development-only config in publish (`reviews/impl-review-phase-1.md`).

## Historical Context (from prior changes)

- `context/changes/account-store-foundation/plan.md:154-160`: Phase 2 creates `api.Tests/` as a sibling of `api/`, "so the Web SDK's default globbing does not compile test files into the app", and says "S-01 adds algorithm tests here too". Status: not landed. Checks 2.1-2.3 are unchecked (`plan.md:385-390`), and `api.Tests/` is absent at `71e4eef`.
- `context/changes/account-store-foundation/plan.md:183-189`: CI test gate planned as a step in the `build` job before publish. Not landed.
- `context/changes/deployment/deployment-plan.md`: cache headers are done (`:54`, verified at `:113`). It doesn't mention a rate limit, OpenAPI at build time or a test step.
- `context/foundation/roadmap.md:65`, **Baseline: partly stale.** It cites `GET /api/health` at `api/Program.cs:38`. Current code has `/api/health/live` at `:84` and `/api/health/ready` at `:87`. Its claim "no generator, PRNG or test project" is still accurate at `71e4eef`.
- `context/changes/bootstrap-verification-web/verification.md`: SPA mode was applied by hand. The template's SSR leftovers (including a `Dockerfile`) are unused.

## Related Research

- External research for this change: the section below (Exa and Context7, same day).
- Not applicable: no other `research.md` exists under `context/changes/`, and `context/archive/` doesn't exist.

## Open Questions

**Decisions the user or the plan must make (none is settled in the documents inspected):**

1. **Default map size and encounter type for S-01** (owner: user, `roadmap.md:97`). This needs a width × height in squares. At 140 px, Chromium's area limit allows about 117 × 117 squares (external research).
2. **Map-size cap and rate-limit policy:** the window, the count, and per IP or global. Only the requirement exists (`api/AGENTS.md:14`, `roadmap.md:155`).
3. **Tileset asset and its source tile size.** The candidates are 16 px (`shape-notes.md:203-204`). 16 doesn't divide 140 (8.75×), so nearest-neighbour scaling gives source pixels 8 or 9 px wide in the export. The choices are:
   - accept that,
   - use art whose tile size divides 140 (14, 20, 28, 35, 70 or 140), or
   - pre-scale the atlas.

   The PRD guardrail concerns tiles lining up with the grid (`prd.md:65-68`), which non-integer pixel scaling doesn't break, as long as every tile's destination rectangle is exactly 140 × 140 at an integer offset.
4. **Who creates `api.Tests/`.** S-01 and F-01 Phase 2 both need it, and the two are running in parallel (`roadmap.md:44-45`). One must create it and the other adapt. Testcontainers is needed only by F-01.
5. **A CI test gate:** extend `deploy.yml`, or add a `ci.yml` for pull requests to match `infrastructure.md:154`. Also: whether Playwright in CI needs SQL Server (see the API inference above).
6. **The OpenAPI contract route into `web/`:** emit the document at build time and either generate TypeScript types or hand-write them. No generator is installed. The package and MSBuild property names come from worker knowledge, not verified docs. Check them with Context7 at plan time.
7. **Where fixture grids live** so that both `dotnet test` and the web test runner can read them.
8. **Web test runner for rendering tests.** Jsdom lacks `OffscreenCanvas` and real image decoding, so the options are a browser-based runner or a Node canvas package (external research).
9. **Whether S-01 adds `contractVersion` now** (`infrastructure.md:132`) or defers it.
10. **Scope split:** whether `/10x-plan` keeps S-01 whole or splits it into "preview" and "download" (`roadmap.md:99`).

---

# External research (Exa + Context7, 2026-09-25)

> Preserved as written before the internal research was added. Its "Questions for `/10x-plan`" are merged into the Open Questions above (numbers 3 and 8); the other items still stand as written.

## Rendering and PNG download

### Decision

The `web/` side uses **plain Canvas 2D with no rendering library**. It has one pure render function, the preview and the download both use it, and wall autotiling is our own code.

Rejected alternatives (Exa research, 2026-09-25):

- **PixiJS v8 + `@pixi/tilemap` 5.0.2.** Uses the GPU, which is more power than the MVP needs. The tilemap plugin broke once already: 5.0.2 fixed "Tilemap doesn't render since Pixi v8.7.0". Output that passes through the GPU is harder to pin with pixel-exact fixture tests in Chrome and Firefox.
- **Konva + react-konva.** Every tile would become a scene-graph node, and each layer allocates two canvases. The export size would have to come from `pixelRatio = 140 / previewTile` instead of drawing at 140 px directly. Konva's own docs warn that an export past the browser limit comes back **blank, with no error**.

Why plain canvas fits: it matches `web/AGENTS.md` exactly (a pure `(grid, seed, tileSize)` render, integer positions, smoothing off, `devicePixelRatio` never reaching the export). It adds no runtime dependency and gives deterministic 2D output for the fixture tests.

### Browser API reference (MDN via Context7 `/mdn/content`)

#### Drawing a tile from the atlas: `CanvasRenderingContext2D.drawImage`

```ts
drawImage(image, sx, sy, sWidth, sHeight, dx, dy, dWidth, dHeight) // 9-arg "slice" form
```

- `image`: any `CanvasImageSource`, which includes `HTMLImageElement`, `ImageBitmap`, `HTMLCanvasElement` and `OffscreenCanvas`.
- `s*` is the source rectangle in the atlas. `d*` is the destination on the canvas; set `dWidth`/`dHeight` to `tileSize` to scale.
- Throws `InvalidStateError` if the image has no data yet, or if the canvas or source rectangle has zero width or height. **The atlas must be fully loaded before rendering.**

MDN's static-tilemap example has the same loop shape as ours:

```js
context.drawImage(tileAtlas, (tile - 1) * map.tsize, 0, map.tsize, map.tsize,
                  c * map.tsize, r * map.tsize, map.tsize, map.tsize);
```

#### No seams or blur: `imageSmoothingEnabled`

```ts
ctx.imageSmoothingEnabled = false; // default is true; set it on EVERY context you create
```

- The flag belongs to one context. The preview context and the export context each need it set.
- MDN's crisp-pixel-art guide: pixel art stays sharp only when image pixels map to **whole multiples** of canvas pixels. At a 140 px export this holds only if the atlas's source tile size divides 140 (for example 14, 20, 28, 35, 70 or 140). **This affects the plan.** Pick the atlas tile size to fit, or accept non-integer scaling with smoothing off (nearest-neighbour, with slightly uneven pixel widths).
- Preview option: draw at an integer tile size and let CSS scale the canvas with `image-rendering: pixelated`, instead of redrawing at a fractional size.

#### Loading the atlas: `createImageBitmap` / `HTMLImageElement.decode()`

```ts
const blob = await fetch(atlasUrl).then(r => r.blob());
const atlas = await createImageBitmap(blob);           // ImageBitmap, ready to draw
// or
const img = new Image(); img.src = atlasUrl; await img.decode(); // resolves when decoded
```

- Either way gives an awaitable "ready" signal, which avoids the `InvalidStateError` above.
- The atlas must come from the **same origin** (served from the API's `wwwroot`). A cross-origin image taints the canvas, and the export then throws `SecurityError`.

#### Offscreen export canvas: `OffscreenCanvas`

```ts
const off = new OffscreenCanvas(cols * 140, rows * 140);
const ctx = off.getContext('2d')!;
// …render…
const blob = await off.convertToBlob({ type: 'image/png' }); // Promise<Blob>
```

- It's never added to the page, so neither `devicePixelRatio` nor CSS can affect it. This is the most direct way to meet the "download at fixed 140 px" rule.
- The alternative is a detached `document.createElement('canvas')` with the same width and height and `canvas.toBlob(cb, 'image/png')`. **Note:** `toBlob`'s callback receives `null` if the image can't be created. Treat `null` as an error; never download it.

#### Encoding: `HTMLCanvasElement.toBlob` versus `toDataURL`

- Use `toBlob` or `convertToBlob`. `toDataURL` builds a base64 string, which is wasteful at 140 px per square (Konva's docs recommend the same).
- Default type is `image/png`, which is lossless. `quality` applies only to jpeg and webp.

#### Triggering the download

```ts
const url = URL.createObjectURL(blob);
const a = document.createElement('a');
a.href = url; a.download = `battle-map-${seed}.png`;
document.body.appendChild(a); a.click(); a.remove();
setTimeout(() => URL.revokeObjectURL(url), 0); // revoke later rather than right away
```

- MDN: revoke object URLs once they're no longer needed. Every example found appends the anchor to the page before calling `click()`. Keep that pattern for Firefox.

### Canvas size limits

| Browser | Max side | Max area | At 140 px per square |
| --- | --- | --- | --- |
| Chrome | 32,767 px | 268,435,456 px (16,384²) | about **117 × 117** squares |
| Firefox 122+ | 32,767 px | about 23,168² | about 165 × 165 squares |

Sources: the canvas-size changelog and test results, and StackOverflow #6081483. The limits vary with OS, RAM and GPU. **Past the limit the canvas goes blank silently.**

#### `canvas-size` (npm, v2.0.0, Feb 2024, MIT, under 1 KB, no dependencies)

Not on Context7. The API below comes from the repo's `docs/index.md`.

```ts
import canvasSize from 'canvas-size';

const { success } = await canvasSize.test({ width: w, height: h });            // does w×h fit?
const { success, width, height } = await canvasSize.test({ sizes: [[w1,h1],[w2,h2]] }); // first size that fits
const { width, height } = await canvasSize.maxArea();                           // also maxWidth(), maxHeight()
// options: useWorker (OffscreenCanvas in a worker; slower on Chromium), onSuccess, onError
// result: { success, width, height, testTime, totalTime }
```

- **Typing gap:** v2 has no bundled `.d.ts`. `@types/canvas-size` is 1.2.2 and describes the v1 API, which lacks `success`. You'd need a small local declaration.
- **Recommendation for S-01:** the map size is one fixed value, so a pure guard with the constants above (`cols*140 <= 16384 && rows*140 <= 16384`) may be enough, with no dependency. Revisit `canvas-size` in S-03, when S/M/L sizes arrive. Let `/10x-plan` decide.

### Wall autotiling (write in-house)

The usual method is the **47-tile "blob" / Godot 3x3-minimal** bitmask:

1. For each wall cell, sample all 8 neighbours. Build a mask with N=1, E=2, S=4, W=8, NE=16, SE=32, SW=64, NW=128.
2. **Corner rule:** a diagonal bit counts only if both of its neighbouring sides are set (for example, clear NE unless N and E are both set). This reduces the 256 raw masks to 47 distinct tiles.
3. Look up the tile in a mask-to-atlas-frame table. The simpler 4-bit version (sides only, 16 tiles) is acceptable if the MVP art has no inner corners. Its downside: room corners and T-junctions get mismatched tiles.

Reference implementations (read them; don't depend on them):

- `esengine/estella` `sdk/src/tilemap/autotile.ts`: a pure TS resolver with `normalizeCornerMask`, which also falls back to the nearest tile by Hamming distance when a tileset is missing a mask.
- `f0rbit/echo` commit `ad777c5`: moved from 4-bit to 47-tile. Its test checks **that all 256 raw inputs map into the 47-tile set**, which is a good unit test to copy.
- `tlhunter/node-autotile`: takes a boolean grid and returns 47-tile offsets (CommonJS, older).
- `@syropian/autotile` v1.0.1: TS, 2 stars, mainly 4-way paths. Too young to depend on.

Autotiling is purely a rendering concern (the grid contract stays semantic, as required by the root `AGENTS.md`). Any variant choice seeds the project's own PRNG from the response seed.

### Playwright download check (Context7 `/microsoft/playwright`)

```ts
const downloadPromise = page.waitForEvent('download'); // register BEFORE clicking, no await
await page.getByRole('button', { name: /download/i }).click();
const download = await downloadPromise;
expect(download.suggestedFilename()).toMatch(/\.png$/);   // comes from the <a download> attribute
const path = await download.path();                       // waits for completion; throws if failed
// then read the PNG header: width/height == cols*140 × rows*140, and it's not blank
```

- `download.path()` gives a file with a random GUID name, so use `suggestedFilename()` for the name. `saveAs(path)` copies the file somewhere else.
- The PNG IHDR chunk holds the width and height as big-endian uint32 at byte offsets 16 and 20. You can check them without an image library.

### Questions for `/10x-plan`

1. The atlas source tile size. Must it divide 140 for perfectly crisp export pixels?
2. Should the render function write into a context it's given (`render(ctx, grid, seed, tileSize)`), so the preview canvas and the `OffscreenCanvas` share it, or return a canvas as `web/AGENTS.md` states?
3. Where do rendering tests run? `OffscreenCanvas` and real image decoding aren't available in jsdom. Options are a real browser (Vitest browser mode or Playwright component tests) or `node-canvas` / `skia-canvas`.
4. A 47-tile or 16-tile wall set for the MVP art. This depends on which tileset is chosen.
5. A constant size guard, or `canvas-size`, in S-01.

---

# Tileset follow-up (2026-09-25, during `/10x-plan`)

> The user asked for a deeper, link-backed comparison of the tileset options, plus a draft of how each semantic cell maps to concrete tiles. Sources: Exa web research and inspection of the downloaded packs. The packs were only downloaded to the session scratchpad, never into the repo. **This supersedes the planning-time recommendation of 0x72 DungeonTileset II**: the deep dive showed its art is 3/4 perspective, which fits a one-square-per-cell grid badly.

## What a battle map needs from a tileset

- **Top-down art.** One grid square is exactly one cell (`prd.md:65-68`). Art in 3/4 perspective, where walls show a front face and "high" walls overhang the cell above, breaks that.
- **Doors in both orientations.** Doors can sit in horizontal and vertical walls. A door that only faces the viewer can't be rotated in 3/4 art.
- **Exact scaling to 140 px.** Pixel art stays even only if its tile size divides 140; 16 px gives 8.75×. Vector art can be rasterised at exactly 140 px.
- **A licence that allows the raw atlas in a public repo:** CC0, or CC-BY with simple attribution.

## Comparison (all links checked on 2026-09-25)

| Tileset | Licence | Tile px (divides 140?) | Perspective | Walls | Doors (H/V) | Fit | Links |
|---|---|---|---|---|---|---|---|
| **Kenney Scribble Dungeons** | CC0 (`License.txt`: "Creative Commons Zero, CC0") | 64 / 128 PNG plus **SVG** (vector, so it can be rasterised at exactly 140) | **true top-down**, hand-drawn graph-paper style | **edge walls**: `wall`, `wall_edge`, `wall_corner`, `wall_half`, `wall_curve`, `wall_diagonal`, `inner_round`, `inner_diagonal`, plus `wall_secret`, `wall_damaged`. Composed and rotated, not a 47-piece set | `door_closed`, `door_open`, `doorway`, one drawing rotated 90° for the other orientation (valid in top-down art) | **4/5** | [page](https://kenney.nl/assets/scribble-dungeons) · [sample](https://kenney.nl/media/pages/assets/scribble-dungeons/5b084d9272-1674932853/sample.png) |
| **Puny Dungeon** (Shade) | CC0 ("Feel free to use this for your game … No need to give me credit") | 16 (no, 8.75×) | top-down with a short front face on walls | **wall cells**, "2-Edge Wang walls for procedural generated maps" | yes: horizontal gates and vertical bars (sample map) | **4/5** | [page](https://opengameart.org/content/16x16-puny-dungeon-tileset) · [preview](https://opengameart.org/sites/default/files/a4.png) |
| **0x72 DungeonTileset II v1.7** | CC0 ("use this tileset for whatever you like (CC-0)") | 16 (no) | **3/4**: walls with brick fronts, "high" walls 16×32 overhanging by 8 px | a 47-piece blob (`atlas_walls_low-16x16.png`, Godot 3×3 minimal), but walls are **thin and centred in the tile**, and half-floor tiles fill the gaps (author: "I gave up and just put the walls in the middle") | **front-facing only**, 2 cells wide (`doors_leaf_closed` 32×32). **No side door** | **2/5** | [page](https://0x72.itch.io/dungeontileset-ii) · [v1.7 devlog](https://0x72.itch.io/dungeontileset-ii/devlog/696000/version-17-autotiles) · [preview](https://img.itch.zone/aW1hZ2UvMzA5MDI4LzE1MTg1NTgucG5n/original/cX%2BRV3.png) · [files mirror](https://github.com/ernestwwchin/myworld/tree/main/public/assets/vendor/0x72_dungeon/0x72_DungeonTilesetII_v1.7) |
| Dungeon Crawl Stone Soup (rltiles) | CC0 ("use these tilesets in your program freely") | 32 (no) | overhead; walls are whole-square textured blocks | wall cells, one tile per cell (no autotiling needed) | `gate_*` (horizontal) and `vgate_*` (vertical), 3 pieces each | 3/5 (mixed art styles) | [page](https://opengameart.org/content/dungeon-crawl-32x32-tiles) |
| Screaming Brain Studios Top Down Dungeon Pack | CC0 | 64 (no; rendered textures, so smooth scaling is acceptable) | true top-down | the only **true top-down 47-piece set** (28 wall styles, Tiled wangsets) | **none** | 3/5 | [page](https://opengameart.org/content/top-down-dungeon-pack) · [demo](https://opengameart.org/sites/default/files/demo_1_15.png) |
| schwarnhild Playdate 20×20 | informal ("use the assets however you like … please remember to credit me"), with **no clause on redistributing the raw assets** | **20 (yes, 7×)** | true top-down, 1-bit | ring walls in 2 styles | doors in every wall direction (screenshots) | 3/5 (licence risk) | [page](https://schwarnhild.itch.io/playdate-dungeon-tileset-top-down-20x20) |
| Kenney Tiny Dungeon / Roguelike Caves & Dungeons / 1-Bit Pack | CC0 | 16 (no) | 3/4 (1-Bit: block walls) | wall-top ring plus front faces, no blob set | front-facing only | 2/5 | [tiny-dungeon](https://kenney.nl/assets/tiny-dungeon) · [caves](https://kenney.nl/assets/roguelike-caves-dungeons) · [1-bit](https://kenney.nl/assets/1-bit-pack) |
| SarturXz Mini Top-Down Dungeon | CC0 | **10 (yes, 14×)** | 3/4, brick front face | a few pieces | front-facing | 2/5 | [page](https://sarturxz.itch.io/mini-top-down-dungeon-tileset) |
| Forgotten Adventures, 2-Minute Tabletop | **not usable** (FA forbids redistribution and embedding in software; 2MT is CC BY-NC) | — | — | — | — | excluded | [FA licence](https://forgotten-adventures.net/info/) · [2MT licence](https://2minutetabletop.com/faq/license-and-attribution/) |

**Conclusion:** two packs fit a top-down grid well, and each represents a different wall model.
- **Scribble Dungeons** has edge walls, is vector and CC0, and looks like a hand-drawn DM map.
- **Puny Dungeon** has wall cells, is 16 px pixel art and CC0, and has doors in both orientations.

0x72 fails on perspective and on side doors, so it would need custom door art and would still look like a game screenshot rather than a battle map.

## Two wall models, and how our grid maps onto each

Our semantic grid (as drafted for the plan) has cells `void` (outside the dungeon), `floor` (room), `corridor`, `wall` and `door`. The API always produces wall **cells**. The renderer decides how to draw them.

### Model A: wall cells (Puny Dungeon, DCSS, 0x72 low atlas)

A `wall` cell is drawn as a wall piece chosen from a neighbour mask of wall cells.

| Semantic cell | Tile(s) | Selection rule |
|---|---|---|
| `void` | solid background (Puny: dark stone / black) | constant |
| `floor` | ground variants | seeded variant per cell (the web PRNG seeded from `seed` + cell index) |
| `corridor` | the same ground set or a darker variant | seeded variant |
| `wall` | a 16-piece edge set (Puny "2-edge Wang") or a 47-piece blob | 4-bit (N, E, S, W) or 8-bit mask of neighbouring **wall** cells, then lookup |
| `door` | horizontal gate if the walls are to the W/E, vertical bars if they are to the N/S | orientation from which neighbours are walls |

- **Cost:** a mask-to-piece lookup table and a hand-labelled atlas index, because Puny ships a sheet, not named files. 16 → 140 scaling gives uneven 8/9 px pixels.
- **Look:** thick stone walls one cell wide, like a game map.

### Model B: edge walls (Scribble Dungeons)

`wall` and `void` cells are drawn as blank "rock" (paper). Every **walkable** cell (`floor`, `corridor`, `door`) draws wall strips on each side that faces a non-walkable cell. This is the graph-paper look in Kenney's sample.

| Semantic cell | Tile(s) | Selection rule |
|---|---|---|
| `void`, `wall` | blank paper (optionally light hatching later) | constant |
| `floor` | `tiles`, with rare seeded `tiles_cracked` / `tiles_decorative` | seeded variant |
| `corridor` | `tile` (plain), which tells corridors apart from rooms | constant, or seeded |
| walls around a walkable cell | `wall` rotated 0/90/180/270° per open side; `wall_edge` or `wall_corner` where two adjacent sides are closed; `inner_round` where only the diagonal is closed (an inner corner) | 4-bit mask of non-walkable **orthogonal** neighbours, plus diagonal checks for inner corners, then a lookup of piece and rotation |
| `door` | `door_closed` rotated so the bar runs along the wall line (horizontal if the door's W/E neighbours are walls, vertical if its N/S neighbours are) | orientation from the neighbours |

- **Cost:** a small composition table (4-bit side mask plus inner-corner overlays) instead of a 47-piece blob. Rotation is valid because the art is top-down.
- **Scaling:** the SVG can be rasterised once to an **atlas at exactly 140 px per tile**, as a build or dev script whose output is committed as one PNG. The export then uses the atlas 1:1 with smoothing off, which meets `web/AGENTS.md:14-16` exactly. The preview draws the same atlas scaled down.
- **Look:** thin black walls on a white grid, like a classic DM battle map, and it prints well. Line art needs no pixel-exact integer scaling.
- **Unconfirmed:** whether Scribble's pieces cover every combination cleanly: dead ends (3 closed sides) and single-cell pillars. The fallback is to compose them from rotated `wall` strips, which the pieces allow.

### Where the mapping lives

Either model keeps the API contract unchanged (semantic cells only, `AGENTS.md:16`). The mapping is one `web/` module, for example `tileset.ts`, holding:
- the atlas URL, imported so Vite hashes it;
- the atlas coordinates for each named piece;
- the selection rules above.

Swapping the tileset later means replacing that module and the atlas, not the grid.

## Local previews (scratchpad only, not committed)

- Scribble pieces contact sheet: `…/scratchpad/scribble_sheet.png`
- The downloaded packs: `…/scratchpad/tilesets/` (and `x/` for the extracted ones)

### Correction (2026-09-26, found while planning)

`Vector/scribbleDungeons_tiles.svg` is one 1280×800 sheet whose pieces do **not** sit on a tile grid (the raster `Tilesheet/tilesheet.png` is 896×704 = 14×11 tiles of 64 px). It therefore cannot be rasterised straight into 140 px tiles. The plan builds the atlas from the individual **128 px PNG pieces** (`PNG/Double (128px)/`) instead: each is resampled once, with a high-quality filter, to exactly 140 px by a build script, which also pre-renders the 90°, 180° and 270° rotations. The renderer then only copies atlas cells 1:1 with smoothing off. The "SVG → exactly 140" claim in the table above is superseded; the fit assessment stands.
