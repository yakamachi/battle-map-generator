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
- Mind browser canvas size limits when adding larger maps or higher export scales.
- Printing across multiple pages at 1 inch per square is post-MVP (see the PRD's non-goals); do not build it into the MVP.

## Tests

- Rendering tests use the shared fixture grids from `../api/` tests: fixed grid and seed in, stable image out.
- Playwright end-to-end tests run in both Chromium and Firefox, including the PNG download (dimensions match the grid × 140 px, and the file is not blank).
