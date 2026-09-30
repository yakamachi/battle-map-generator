# Battle Map Generator Web

Read the root `../AGENTS.md` first (ownership split, generation rules, testing, deployment). This file adds frontend-only rules.

## Scope

This app owns the login UI, encounter parameters, generate and regenerate requests, and **all visual output**: turning the API's semantic grid into a picture, the on-screen preview, and the PNG download. Sprite and tileset work, the main direction for improving maps, lives entirely here.

React Router with TypeScript in SPA mode (`ssr: false`; the starter defaults to SSR). There is no Node server in production: ASP.NET Core in `../api/` serves `build/client`. Call the API with relative `/api` URLs; no CORS in production.

## Map rendering

- One pure mapping from the grid to draw operations (`drawOps` in `app/map/tileset.ts`): it maps semantic cells to sprites, does wall autotiling and picks visual variants. Variant randomness is derived from the response seed, never from `Math.random()`.
- **Render once, at a fixed 140 px per grid square, into one canvas** (twice Roll20's 70 px standard, so it also prints sharply at 1 inch per square). The preview is that same canvas scaled down with CSS, and the download is `toBlob` of that same canvas, so there is no second render path. `devicePixelRatio`, browser zoom and display scaling must never affect the canvas bitmap or the downloaded file.
- Draw at integer pixel positions with image smoothing off, so tiles never show seams.
- Keep the tileset as one atlas image with a hashed filename, so browsers cache it and it does not consume the hosting plan's daily bandwidth quota.
- The tileset is Kenney's *Scribble Dungeons* (CC0; `tileset-src/scribble/License.txt`). Only the pieces in use are vendored in `tileset-src/scribble/`. `npm run atlas` (`scripts/build-scribble-atlas.mjs`) resamples them to 140 px, pre-renders every rotation and writes `app/map/tileset/scribble-atlas.png` plus its JSON index; both are committed and CI fails if a fresh build differs. The renderer never rotates or scales.
- Which piece a semantic cell gets (floors, variants, wall strips, corners, doors) lives only in `app/map/tileset.ts`; a new cell kind or piece starts there.
- `app/map/download.ts` saves that same canvas with `toBlob` as `battle-map-<seed>.png`; a failed export shows an error, never an empty file.
- Mind browser canvas size limits when adding larger maps or higher export scales.
- Printing across multiple pages at 1 inch per square is post-MVP (see the PRD's non-goals); do not build it into the MVP.

## Tests

- Rendering tests use the shared fixture grids in `../fixtures/grids/` (written by the API tests): fixed grid and seed in, stable image out. Each browser's pixel hash is pinned in `app/map/render-baselines.json`.
- The baselines were generated locally and matched Ubuntu CI exactly on the first run, so regenerating them locally (`UPDATE_BASELINES=1 npm test`) is fine for now. Update them in the same commit as any fixture, atlas or mapping change.
- End-to-end tests live in the root `../e2e/` package, not here: they test the whole app (the .NET host serving this SPA). Changes to markup they locate (button names, the single canvas, the `Seed: N` text) must keep them green; see `../e2e/AGENTS.md`.
- Commands: `npm test` (Vitest browser mode, mapping and rendering, both browsers), `npm run typecheck`.
