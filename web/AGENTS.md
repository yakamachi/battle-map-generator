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
- A `bossArena` cell is room floor for walls, corners and door bars, and draws the `tiles_decorative` piece; plain floors never use that piece, so the arena stands out.
- `app/map/download.ts` saves that same canvas with `toBlob` as `battle-map-<seed>.png`; a failed export shows an error, never an empty file.
- Mind browser canvas size limits when adding larger maps or higher export scales: `renderMap` refuses a side over 16384 px or an area over 8400² px (the API's 60×60 cap).
- Printing across multiple pages at 1 inch per square is post-MVP (see the PRD's non-goals); do not build it into the MVP.

## Access

- Protected pages render inside the `routes/protected.tsx` layout (wired in `app/routes.ts`): its `clientLoader` calls `getSession()` and redirects to `/login` when there is no account. `routes/login.tsx` and `routes/register.tsx` sit outside that layout and redirect to `/` once a session already exists. A route with a `clientLoader` in SPA mode relies on the root `HydrateFallback` (`app/root.tsx`) for the gap before the first render; React Router permits `HydrateFallback` only on the root route in SPA mode, so a route-local one is not an option.
- Auth calls (`getSession`, `register`, `login`, `logout`) live in `app/api/auth.ts`, which shares the `openapi-fetch` client exported from `app/api/client.ts`; map generation calls stay in `client.ts`.
- The layout's header (account email, Log out) is a row above every protected page's own `<main>`: a row added there counts against `map-preview-fit` in `app/app.css`, same as a row added inside `home.tsx`.

## UI

- Tokens live in `app/app.css`: semantic values in `:root` plus the `prefers-color-scheme: dark` media block, published to Tailwind through `@theme inline`. The original values and contrast ratios are in `../context/archive/2026-09-29-home-view-ui-contract/tokens.md` (read-only). A new or changed colour, radius or size goes into `app/app.css`, never into a view, and the change that makes it rechecks contrast in both themes and records it in its own change folder.
- Components live in `app/components/ui`. Check there before creating one. Add a missing one with `npx shadcn@latest add <name>` (then drop the `radix-ui` import if the component only needs it for `asChild`), or copy it by hand the same way `button.tsx`, `alert.tsx`, `label.tsx` and `native-select.tsx` were (the last two avoid shadcn's `radix-ui` and `lucide-react` imports).
- Views (`app/routes/`, `app/root.tsx`, components outside `ui/`): no literal colours (hex, `rgb()`, `hsl()`, `oklch()`), no palette classes (`bg-gray-900`, `text-white`), no `dark:` classes and no arbitrary values (`p-[13px]`). Use token classes (`bg-card`, `text-muted-foreground`) and primitives. Primitives in `ui/`: no literal colours or palette classes, but shadcn's token-based `dark:` refinements are fine. `npm run ui:scan` enforces both, and CI runs it.
- Dark mode follows the OS (`prefers-color-scheme`); there is no `.dark` class. Never add `@custom-variant dark` or a `.dark` block (`ui:scan` fails on both in `app/app.css`).
- After `shadcn add`, review the `app/app.css` diff: remove any `.dark` block or `@custom-variant` the CLI wrote, and add any new variable it needs (for example `--popover`) by hand in both `:root` and the dark media block, with its contrast checked in both themes, recorded in the change that adds it.
- The screenshot gate lives in `visual/` (`home.visual.spec.ts`, `auth.visual.spec.ts`, baselines in `visual/__screenshots__/`), runs in CI in the Playwright image and mocks the API; both specs share `visual/fixtures.ts` for hermetic fonts and a mocked `/api/auth/me` (a fixed account by default, `test.use({ account: null })` for a logged-out visitor). Update baselines only through `npm run visual:update`, and explain the visual change in the pull request.

## Tests

- Rendering tests use the shared fixture grids in `../fixtures/grids/` (written by the API tests): fixed grid and seed in, stable image out. Each browser's pixel hash is pinned in `app/map/render-baselines.json`.
- The baselines were generated locally and matched Ubuntu CI exactly on the first run, so regenerating them locally (`UPDATE_BASELINES=1 npm test`) is fine for now. Update them in the same commit as any fixture, atlas or mapping change.
- End-to-end tests live in the root `../e2e/` package, not here: they test the whole app (the .NET host serving this SPA). Changes to markup they locate (button names, the single canvas, the `Seed: N` text) must keep them green; see `../e2e/AGENTS.md`.
- Commands: `npm test` (Vitest browser mode, mapping and rendering, both browsers), `npm run typecheck`.
