# First Map Download — Plan Brief

> Full plan: `context/changes/first-map-download/plan.md`
> Research: `context/changes/first-map-download/research.md`

## What & Why

Roadmap slice S-01 closes the product's single user story end to end: the DM clicks "Generate", sees a dungeon aligned to the grid, and downloads a PNG at 140 px per square that matches the preview, in Chrome and Firefox. The same change moves the project to pull requests, so that anything merged into `main` has passed the tests.

## Starting Point

- **API:** health probes, the account store from F-01 and 15 integration tests (`api.Tests`). There is no generator, grid contract, rate limiter, or OpenAPI document at build time.
- **Web:** the React Router starter screen, with no test tooling and no art.
- **CI:** `deploy.yml` tests and deploys on every push to `main`; there is no workflow for pull requests, and `main` is unprotected.

## Desired End State

- **In the app:**
  - "Generate" asks `POST /api/maps/generate` for a seeded 30×20 BSP dungeon. The cells are `void`, `floor`, `corridor`, `wall` and `door`.
  - The screen shows it in the Kenney *Scribble Dungeons* graph-paper style.
  - "Download PNG" saves `battle-map-<seed>.png` at exactly 4200×2800, the same bitmap as the preview.
- **In the pipeline:**
  - Every change arrives through a PR whose `CI` checks run: API tests, web types, the contract check, rendering tests in 2 browsers, and e2e in 2 browsers.
  - `main` is protected, and a merge deploys without re-running the tests.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Slice shape | One change, 4 phases, one PR per phase | Only the download closes US-01, and the API, client and fixtures must change together | Plan |
| Map size | Fixed 30×20, API cap 60×60 | A typical encounter area, far below the browser canvas limit (about 117×117) | Plan |
| Rate limit | 10/min per IP, 429 with `Retry-After`, forwarded headers on App Service | Stops a stuck client from burning F1's 60 CPU-min/day while generation is public before S-04 | Plan |
| Tileset | Kenney Scribble Dungeons (CC0), edge-wall model | True top-down, doors both ways, looks like a DM battle map; 0x72 is 3/4 perspective with no side doors | Research (follow-up) |
| Atlas | 128 px PNG pieces resampled once to 140 px, with pre-rendered rotations | The renderer only copies pixels, so the output is exact and identical across browsers (the SVG sheet isn't on a grid) | Research (correction) |
| Preview vs export | One canvas at 140 px per square, CSS-scaled; the download is `toBlob` of that canvas | The PNG matches the preview by construction, and DPR or zoom can't change it | Plan |
| Contract | Build-time OpenAPI (committed) → `openapi-typescript` + `openapi-fetch`, drift check in CI | OpenAPI is the documented contract source, and drift becomes a type error | Plan |
| Web tests | Vitest 5 browser mode in Chromium and Firefox, one pixel hash per fixture per browser (`UPDATE_BASELINES=1`) | Tests the real browser canvas in both required browsers without failing on engine rounding differences | Plan (review F2) |
| Fixtures | Root `fixtures/grids/*.json`, updated with `UPDATE_FIXTURES=1` | A neutral place both toolchains read; the API pins them, the web renders them | Plan |
| CI model | Tests on PRs only (`ci.yml`); `deploy.yml` without tests; `main` protected (strict, admins enforced) | "Merged means tested" and faster deploys; this reverses F-01's test-before-deploy step | Plan (user) |
| Stale tabs | `contractVersion` deferred | The risk is already accepted for two users (`infrastructure.md:177`) | Research |

## Scope

**In scope:**
- the PR workflow and branch protection;
- the PRNG, BSP generator, grid contract and fixtures;
- the rate-limited endpoint, and OpenAPI generated at build time;
- the generated TypeScript client;
- the Scribble atlas with its script and the mapping module;
- the preview screen and PNG download;
- the Vitest browser tests and Playwright e2e in 2 browsers;
- documentation updates.

**Out of scope:**
- sizes and encounter type (S-03), the regeneration experience (S-02), login (S-04);
- `contractVersion`, decorations and boss cells;
- saving maps, printing, server-side rendering;
- removing the leftover SSR dependencies;
- PR review approvals.

## Architecture / Approach

The API (C#) runs a seeded BSP and returns cell kinds, never sprites. The generated OpenAPI file becomes TypeScript types for `web/`. There, `tileset.ts` turns cells into draw operations: floor variants, wall strips on sides facing rock, inner corners, and doors across the wall line. `render.ts` copies each piece from the 140 px atlas 1:1 into a `width×140` by `height×140` canvas. That canvas is both the CSS-scaled preview and the downloaded PNG. Fixed-seed fixture grids connect the C# tests, the browser tests and the e2e tests.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. PR workflow and CI gate | `ci.yml` (api, web), `deploy.yml` without tests, `main` protected | Protection needs the checks to have run once; an admin lockout if they're misconfigured |
| 2. Grid contract and generator | PRNG, BSP with guarantees, fixtures, `POST /api/maps/generate` with cap and rate limit, build-time OpenAPI | Forwarded headers on App Service (per-IP limit); build-time OpenAPI runs `Program.cs` |
| 3. Web client, atlas, preview | Typed client, Scribble atlas and mapping, Generate screen, browser rendering tests, contract drift check | Scribble pieces may not cover every wall combination cleanly (dead ends, pillars) |
| 4. PNG download and e2e | Download button, Playwright in Chromium and Firefox, e2e CI job, docs | Firefox canvas export; CI time for 2 browsers |

**Prerequisites:**
- F-01 done (archived);
- the owner approves the branch-protection and App Service app-setting commands;
- the Kenney Scribble Dungeons pack (CC0) is vendored into `web/tileset-src/`.

**Estimated effort:** about 4 sessions, one per phase and PR.

## Open Risks & Assumptions

- **Forwarded headers:** assumed to work through `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`, checked manually from two networks in Phase 2.
- **Scribble edge cases:** dead ends and single pillars may need several rotated `wall` strips instead of a dedicated piece; the mapping tests pin whichever composition is chosen.
- **Cross-browser pixel hash:** baselines are per browser; whether Chromium and Firefox match is only logged.
- **Seed stability:** any change to the generator changes which map a seed produces, so the fixtures and baselines are updated in the same commit.

## Success Criteria (Summary)

- On production, in Chrome and in Firefox, the DM generates a dungeon and downloads a 4200×2800 PNG that matches the preview and sits exactly on the grid.
- Every merge to `main` has passed the API, web, contract, rendering and e2e checks; nothing reaches `main` any other way.
- Generation is public but bounded: 10 per minute per IP, 60×60 at most, and no database access.
