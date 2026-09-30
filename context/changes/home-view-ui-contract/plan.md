# Home View UI Contract Implementation Plan

## Overview

This change gives the home view (`web/app/routes/home.tsx`, the only route) a design-system contract:
- **Tokens:** semantic "parchment & ink" colour tokens in `web/app/app.css`.
- **Components:** a repo-owned `Button`, `Alert` and map-preview frame.
- **Fixes:** the four charges from research (C1–C4) and the 7 component states.
- **Guards:** a Playwright screenshot gate and a UI rule, so S-02, S-03 and S-04 build on the contract instead of copying literals.

It is a `/10x-ui` change: one view plus global tokens.

### Implementation notes (what shipped differs from the text below)

Added at closeout (full review F5, 2026-09-30). Where these notes and a phase block disagree, the notes describe the code on `main`:

- **Button radius:** `rounded-lg` (the `--radius` token, the same as upstream shadcn primitives), not `rounded-md` (impl-review-phase-2 F2).
- **Button focus:** `outline-hidden`, not `outline-none`, so focus stays visible in forced-colors mode (impl-review-phase-2 F1).
- **Alert role:** by variant: `destructive` renders `role="alert"`, `default` renders `role="status"` (impl-review-phase-2 F3).
- **Generate button:** one `busy` state drives its label and `disabled` together with the preview overlay. The canvas has `role="img"`. The empty-state copy is built from the map size and `TILE_SIZE` (impl-review-phase-3 F1, F2, F4).
- **`web/vite.config.ts`:** `preview.host: "127.0.0.1"`. The SPA build prerenders through `vite preview`, and in the Playwright container (IPv6 loopback) `localhost` made that fail with ECONNREFUSED. Found in Phase 4.
- **404 link:** `web/app/root.tsx` wraps `buttonVariants(...)` in `cn(...)`. The raw cva string holds both `border-transparent` and `border-border`, and without the merge the transparent one won, so the link had no border. Found in Phase 4 by the visual gate.
- **Screenshot tolerance:** `maxDiffPixels: 50, threshold: 0.05`, not `maxDiffPixelRatio: 0.01`. The planned tolerance let that missing border (~400 px at 1.45:1 contrast) pass. It's safe to be strict because baselines and CI render in the same image.
- **Fixtures:** the visual spec uses only `fixtures/grids/seed-42.json`. The regenerate shot holds the second request, so `seed-1.json` is never drawn.
- **Baselines:** `web/.gitignore` un-ignores `visual/__screenshots__/`, which a blanket ignore rule would have excluded.
- **Closeout (full review F1–F4, F6):**
  - The visual spec serves a vendored Inter woff2 instead of Google Fonts.
  - `ui:scan` covers border sides and `caret`/`accent`/`decoration`/`ring-offset`/`placeholder`, and walks `app/components` recursively.
  - `serve-spa.mjs` fails clearly without a build, and 404s missing files.
  - Mobile shots are `fullPage`.
- **Delivery:** all four phases shipped in one PR (#9, squash `dcb83c9`), not one PR per phase. The e2e moved to the root `e2e/` package mid-change (#8); the commands above already use it.

## Current State Analysis

From `research.md`:

- **No design system.**
  - `app.css:3-6` publishes a single token, `--font-sans`.
  - `app.css:10` paints the page with `bg-white dark:bg-gray-950`.
  - There is no components directory and no UI library in `web/package.json`.
- **C1 Missing tokens:** palette literals, each with a hand-written dark twin, at `home.tsx:101,109,116,121` (4 hits from the hardcoded-value scan).
- **C2 Missing shared component:**
  - Two hand-built buttons at `home.tsx:97-112`, with no hover and no focus-visible style.
  - One alert paragraph written twice, at `home.tsx:115-124`.
- **C3 Preview lifecycle:**
  - `home.tsx:40,71-72,125` unmount the canvas on every Generate.
  - `render.ts:33-34` sizes the canvas only after the tile atlas loads.
  - The result: the preview blanks out, shows a 300×150 box, then jumps to full height.
  - At 1280×720 the preview is about 853 px tall, so the user has to scroll to see it.
- **C4 Entry states:**
  - The idle view (`home.tsx:93-114`) gives no guidance.
  - An unknown route shows an unstyled `root.tsx:48-75` ErrorBoundary with no link home.
- **Dark mode:** follows the operating system (`prefers-color-scheme`, Tailwind's default `dark:` variant). There is no class toggle.
- **Tests:**
  - There is no visual testing.
  - There is no lint and there are no git hooks (`web/CLAUDE.md:13`).
  - Playwright 1.63 runs e2e against the .NET host.

## Desired End State

- **Theme:** the page reads as parchment and ink in light mode and charcoal and parchment in dark mode. Every colour, radius and focus ring comes from tokens in `app.css`.
- **Components:**
  - `home.tsx` and `root.tsx` build their UI from `~/components/ui/*` and token classes only.
  - The full hardcoded-value scan (colour literals, `dark:` and numeric arbitrary values) returns 0 hits on the views: `app/routes`, `app/root.tsx`, and `app/components/*.tsx` outside `ui/`.
  - The colour-literal part of the scan returns 0 hits on `app/components/ui`. Its primitives may keep shadcn's token-based `dark:` refinements.
  - CI enforces this with `npm run ui:scan`.
- **Preview:**
  - Before the first map, a placeholder explains what to do.
  - While generating, the previous map stays visible, dimmed and marked busy.
  - A drawn map always fits the viewport whole, at its aspect ratio, with no layout jump.
- **Error pages:** an unknown URL shows a styled not-found page with a link back to the generator.
- **7 states:** each of the 7 states is shown in a Playwright screenshot baseline, or marked N/A with a reason. The baselines cover desktop light, desktop dark and a 390 px mobile width, and a `visual` CI job checks them.
- **Rule for later work:** `web/AGENTS.md` tells the next agent:
  - where the tokens and components live
  - how to add a component
  - not to write literal colours or arbitrary values in views
- **Verify:**
  - `npm run ui:scan`
  - `npm --prefix e2e test` (unchanged locators, both browsers)
  - the `visual` CI job
  - reviewing the committed baselines

### Key Discoveries:

- E2E locators that must survive (`e2e/tests/map.spec.ts`):
  - `:38` button name "Generate" (substring match)
  - `:41-42` "Download PNG" with the native `disabled` attribute
  - `:44-57` exactly one `<canvas>`, 4200×2800 intrinsic
  - `:59-60` one element whose whole text is `Seed: <digits>`
- The fixture grids in `fixtures/grids/seed-*.json` have the exact `GeneratedMap` shape (`seed, width, height, cells, rooms`; 30×20). They can be returned from a mocked `POST /api/maps/generate` (`app/api/client.ts:21`), which keeps visual tests deterministic and off the 10-per-minute rate limit.
- `build/client` is a static SPA (`ssr: false`), so the visual tests don't need .NET. Docker 29 is available locally for running baselines in the CI image.
- shadcn's current Button (registry `radix-nova/button.json`) imports `cn`, `class-variance-authority` and `radix-ui` (only for `asChild`). Its classes use `dark:` variants, so its dark mode must stay media-query based. shadcn's `.dark` custom variant must not be added (`research.md`, Stack docs).
- Per-theme values must be published through `@theme inline`. Plain `@theme` resolves nested `var()` where it is defined, not where it is used.

## What We're NOT Doing

- **No `shadcn init`, and no `radix-ui`, `@base-ui/react`, `lucide-react`, `tw-animate-css` or `shadcn` runtime packages.** The only new dependencies are `cn` and `class-variance-authority`.
- **No dark-mode toggle and no `.dark` class variant.** Dark mode stays OS-driven.
- **No components the view doesn't use yet.** That means no Input, Label or Select. S-03 and S-04 add them through the path the rule describes.
- **No changes to the canvas bitmap, `render.ts`, the tileset, `download.ts`, or the 140 px per square rule.**
- **No regenerate button, parameters form or login UI.** Those belong to S-02, S-03 and S-04.
- **No changes to existing strings or language.** The UI stays English, and the button names stay "Generate" and "Download PNG". New strings are limited to the empty-state message, the "Generating…" loading overlay and the 404 page's "Back to the generator" link.
- **No self-hosting Inter.** It still comes from Google Fonts.
- **No lint tool.** The guard is a dependency-free Node scan script.
- **No screenshot testing in Firefox.** The visual gate is Chromium only. Firefox is still covered by the e2e and rendering tests.

## Implementation Approach

The phases follow the `/10x-ui` order:
1. Environment and tokens: the contract exists before any view reads it.
2. Components, and the view migrated onto them: C1, C2.
3. Preview and entry states: C3, C4 and the state matrix.
4. The gate and the guard.

Each phase leaves the e2e green with unchanged locators. The token values are written down in `tokens.md` in this change folder, so they don't live only in a chat.

## Critical Implementation Details

- **State sequencing (C3):** Download must still be enabled only when the map in `status` is the map drawn on the canvas (`home.tsx:41`). That rule doesn't change.
  - What changes: the canvas stays mounted once a map has been drawn.
  - During `loading`, the last drawn map stays visible, dimmed. The Download button is still disabled and the seed is hidden.
  - On `error`, the frame goes back to the placeholder. The previous drawing must not look downloadable.
  - A second state value ("a drawing is on screen") is needed besides `renderedMap`. `renderedMap` is reset on every Generate (`home.tsx:72`).
- **Canvas sizing:**
  - Only CSS may size the preview: `object-fit: contain` inside a frame with the map's aspect ratio (`width / height` from the response).
  - The canvas `width`/`height` attributes stay owned by `renderMap`.
  - Fitting to the viewport needs a height limit (viewport minus header). Express it as `@utility map-preview-fit { … }` in `app.css`, holding the `calc()`. Don't use a `--spacing-*` token, which would leak into every spacing utility (`p-`, `gap-`, …), and don't use an arbitrary class in the view.
- **Container baselines:** screenshot baselines must be generated in `mcr.microsoft.com/playwright:v1.63.0-noble`, the image CI uses. Fonts and anti-aliasing on the local CachyOS host differ.
  - The local update script must keep the container's `node_modules` in a named Docker volume. Otherwise it overwrites the host's `node_modules` as root.
  - Before each screenshot, wait for `document.fonts.ready`.

## Phase 1: Contract Foundation (environment + tokens)

### Overview

Install the two small dependencies and the `cn` helper, and set up `components.json` so `shadcn add` works later. Define the parchment and ink tokens in `app.css` and write their values down. After this phase the contract exists and the page background reads it. The buttons still use literals until Phase 2.

### Changes Required:

#### 1. Dependencies and helper

**File**: `web/package.json`, `web/app/lib/utils.ts`, `web/components.json`

**Intent**: Add the minimum shadcn-style toolchain chosen in option (b), so components can be written the shadcn way and later ones can come from `npx shadcn add`.

**Contract**:
- `cn` (^0.4.0) and `class-variance-authority` (^0.7.1) are runtime dependencies.
- `app/lib/utils.ts` re-exports `cn` from `"cn"`.
- `components.json`:
  - `tsx: true`, `rsc: false`, `style: "radix-nova"`
  - `tailwind.css: "app/app.css"`, `cssVariables: true`, `baseColor: "neutral"`, `prefix: ""`
  - aliases: `components` → `~/components`, `ui` → `~/components/ui`, `lib` → `~/lib`, `utils` → `~/lib/utils`, `hooks` → `~/hooks`
  - `iconLibrary: "lucide"` is kept only as a CLI default. It doesn't install anything.

#### 2. Semantic tokens

**File**: `web/app/app.css`

**Intent**: One source of role-named values, used in both themes. This replaces the body palette literals (C1).

**Contract**:
- Keep `@import "tailwindcss"` and the plain `@theme { --font-sans }`.
- Add `:root` with:
  - `--background`, `--foreground`
  - `--card`, `--card-foreground`
  - `--primary`, `--primary-foreground`
  - `--secondary`, `--secondary-foreground`
  - `--muted`, `--muted-foreground`
  - `--accent`, `--accent-foreground`
  - `--destructive`, `--border`, `--input`, `--ring`, `--radius`
- Put the dark values in `@media (prefers-color-scheme: dark) { :root { … color-scheme: dark } }`.
- Add `@theme inline`, publishing `--color-<name>: var(--<name>)` for each colour and the `--radius-sm/md/lg/xl` scale derived from `--radius`.
- Add `@layer base`: `* { border-border outline-ring/50 }`, `body { bg-background text-foreground }`, and pointer cursor on enabled buttons.
- No `@custom-variant dark`.
- Remove `bg-white dark:bg-gray-950`.
- A comment names the source: the parchment & ink motif, recorded in this change's `tokens.md`.

Starting values (oklch). The implementer may adjust them to meet the contrast criteria and records the final values in `tokens.md`:

| Token | Light | Dark |
|---|---|---|
| background | 0.97 0.018 85 | 0.20 0.010 60 |
| foreground | 0.22 0.020 60 | 0.93 0.020 85 |
| card | 0.99 0.008 85 | 0.24 0.012 60 |
| primary | 0.25 0.020 60 | 0.90 0.030 85 |
| primary-foreground | 0.97 0.018 85 | 0.22 0.020 60 |
| secondary / accent | 0.92 0.025 80 | 0.30 0.015 60 |
| muted | 0.93 0.020 80 | 0.28 0.012 60 |
| muted-foreground | 0.48 0.030 65 | 0.72 0.030 75 |
| destructive | 0.50 0.170 30 | 0.68 0.150 30 |
| border / input | 0.85 0.030 75 | 1 0 0 / 12% (input 16%) |
| ring | 0.50 0.080 60 | 0.72 0.080 70 |
| radius | 0.5rem | — |

(`*-foreground` for card, secondary and accent is `foreground`.)

#### 3. Token record

**File**: `context/changes/home-view-ui-contract/tokens.md`

**Intent**: Keep the chosen values and their contrast ratios in the repo, so the next session doesn't reinvent them.

**Contract**: a table of final light and dark values per token. It also lists the contrast ratios for:
- foreground/background
- primary-foreground/primary
- muted-foreground/background
- destructive/background
- ring/background
- border/background

It notes the source ("parchment & ink motif, chosen in /10x-plan 2026-09-29").

### Success Criteria:

#### Automated Verification:

- Type check and build pass: `npm --prefix web run typecheck && npm --prefix web run build`
- Rendering tests unchanged: `npm --prefix web test`
- E2E still passes in both browsers: `npm --prefix e2e run build:app && npm --prefix e2e test`
- `app.css` has no palette classes or `@custom-variant`: `! grep -nE '(bg|text|border)-(white|black|gray|red)|@custom-variant' web/app/app.css`

#### Manual Verification:

- The page background and text are parchment and ink with the OS in light mode, and charcoal and parchment in dark mode, in Chrome and Firefox
- `tokens.md` records contrast of at least 4.5:1 for foreground/background, primary-foreground/primary, muted-foreground/background and destructive/background, and at least 3:1 for ring/background, in both themes

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Components and View Migration (C1, C2)

### Overview

Add the repo-owned `Button` and `Alert` and move `home.tsx` onto them and onto token classes. The hardcoded-value scan on the view drops from 4 hits to 0. Hover, focus-visible and disabled come from the component.

### Changes Required:

#### 1. Button

**File**: `web/app/components/ui/button.tsx`

**Intent**: The one button primitive (C2). It is adapted from shadcn's radix-nova Button so later `shadcn add` output looks the same.

**Contract**:
- Exports `Button` and `buttonVariants`.
- Renders a native `<button>`: no `asChild`, no Slot, no `radix-ui`.
- `type` defaults to `"button"`.
- `cva` variants:
  - `variant`: `default` (primary) and `outline`
  - `size`: `default` only
- The base classes cover:
  - `rounded-md` from the radius token
  - `focus-visible:ring-3 focus-visible:ring-ring/50` with the border in `ring`
  - `disabled:pointer-events-none disabled:opacity-50`
  - a token-driven `hover:` per variant
- Sets `data-slot="button"`, `data-variant`.
- Imports `cn` from `"cn"` as shadcn's current source does. `~/lib/utils` re-exports it for components that expect it there.
- Only the variants `home.tsx` uses. S-02's regenerate action reuses `default` or `outline`.
- It may keep shadcn's token-based `dark:` refinements (for example `dark:bg-input/30`). With OS-driven dark mode they follow `prefers-color-scheme`. It must contain no colour literals or palette classes.

#### 2. Alert

**File**: `web/app/components/ui/alert.tsx`

**Intent**: One error and notice primitive, replacing the duplicated `<p role="alert">` (C2).

**Contract**:
- Exports `Alert`.
- `variant`: `default` or `destructive`. Destructive uses `text-destructive`, a tinted token border and a background.
- Renders `role="alert"`.
- The children are the message text. Meaning doesn't rely on colour alone: the text says what went wrong.

#### 3. Home view migration

**File**: `web/app/routes/home.tsx`

**Intent**: Build the view from the components and tokens only (C1, C2), with no behaviour change in this phase.

**Contract**:
- Generate uses `<Button>` (default) and Download PNG uses `<Button variant="outline">`. The names and the native `disabled` stay the same.
- Both error messages use `<Alert variant="destructive">`.
- The seed is one element with the whole text `Seed: {seed}`, styled `text-muted-foreground` with tabular numbers.
- The heading uses token and type classes.
- No `dark:` classes remain in the view.

### Success Criteria:

#### Automated Verification:

- Type check and build pass: `npm --prefix web run typecheck && npm --prefix web run build`
- The colour-literal part of the hardcoded-value scan (the `/10x-ui` pattern) returns 0 hits on `web/app/routes/home.tsx` and `web/app/components/`
- There are no `dark:` classes or numeric arbitrary values in `web/app/routes/`: `! grep -rnE 'dark:|-\[[0-9.]+(px|rem)\]' web/app/routes`
- E2E passes in both browsers with the locators unchanged: `npm --prefix e2e run build:app && npm --prefix e2e test`

#### Manual Verification:

- In Chrome and Firefox, light and dark: both buttons show a token-coloured hover, a visible `ring` focus when you Tab to them, and a readable disabled Download PNG
- An error (stop the API, then click Generate) shows the destructive Alert, readable in both themes

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 3: Preview and Entry States (C3, C4)

### Overview

Add a preview frame that fits the viewport and never jumps, with a real empty state and a loading state that keeps the previous map. Style the not-found and error page with the same tokens and components. This completes the 7-state matrix.

### Changes Required:

#### 1. Map preview frame

**File**: `web/app/components/map-preview.tsx` (a view-level component, not a `ui/` primitive)

**Intent**: Own the preview's layout and states so the canvas never unmounts or resizes the page (C3). Show a helpful placeholder before the first map (C4).

**Contract**:
- Props:
  - the canvas ref
  - the aspect ratio (`width` and `height` in squares; defaults to 30×20 before any map)
  - `state`: `"empty" | "loading" | "ready"`
  - whether a drawing is on screen
- A `card`-token frame with a `border`, at the map's aspect ratio. Its size is limited to the container width and the viewport height minus the header, through the `map-preview-fit` utility in `app.css`, so the whole map is visible at 1280×720 and 390×844.
- The single `<canvas>` is always mounted inside it and sized only by CSS (`object-fit: contain`, full frame). It keeps its `aria-label`.
- **Empty:** a `muted-foreground` message, "Click Generate to create a 30×20 battle map, then download it as a PNG (140 px per square)". No Roll20 claim until the scale follow-up: at 140 px it imports at twice Roll20's 70 px unit. The canvas is hidden.
- **Loading:** the frame gets `aria-busy="true"` and a "Generating…" overlay. A previous drawing stays visible, dimmed (`opacity` utility). With no previous drawing, the placeholder shows the overlay.
- **Ready:** the canvas is fully visible.

#### 2. Home view state wiring

**File**: `web/app/routes/home.tsx`

**Intent**: Feed the frame without changing the download rule (see Critical Implementation Details → State sequencing).

**Contract**:
- `canDownload` is unchanged: current map === drawn map.
- A "drawing on screen" flag becomes true after `renderMap` and false on `error`.
- Frame state:
  - `idle` or `error` → `empty`
  - `loading` → `loading`
  - `ready` → `ready`. While the atlas still loads, the frame shows the loading overlay until the draw finishes.
- The Seed element is rendered only while a drawn current map exists.
- Alerts sit above the frame.

#### 3. Not-found and error page

**File**: `web/app/root.tsx`

**Intent**: A link from outside to an unknown path gets a styled page with a way back (C4).

**Contract**:
- The ErrorBoundary uses token classes (heading, `text-muted-foreground` details, `bg-muted` stack block).
- It adds a React Router `<Link to="/">` styled with `buttonVariants({ variant: "outline" })`, labelled "Back to the generator".
- The 404 text stays the same.

### Success Criteria:

#### Automated Verification:

- Type check and build pass: `npm --prefix web run typecheck && npm --prefix web run build`
- The full hardcoded-value scan, including `dark:` and numeric arbitrary values, returns 0 hits on `web/app/routes/`, `web/app/root.tsx` and `web/app/components/map-preview.tsx`. The colour-literal part returns 0 hits on `web/app/components/ui/`
- E2E passes in both browsers, including the second-map round, with one canvas and the pixel checks: `npm --prefix e2e run build:app && npm --prefix e2e test`
- Rendering tests unchanged: `npm --prefix web test`

#### Manual Verification:

- At 1280×720 in Chrome and Firefox: generate, then regenerate. The page doesn't jump, the previous map stays dimmed while loading, and each finished map is fully visible without scrolling
- At 390 px wide (devtools device mode), the empty state, a finished map and the buttons fit without horizontal scrolling
- A network error after a map was shown returns the frame to the placeholder with the Alert, and Download PNG is disabled
- `/does-not-exist` shows the styled not-found page, and "Back to the generator" returns to `/`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Phase 4: Visual Gate and Guard

### Overview

Pin the states in Playwright screenshot baselines generated and checked in the CI image. Turn the hardcoded-value scan into a CI check. Write the UI rule that keeps the next agent on the contract.

### Changes Required:

#### 1. Static server for the built SPA

**File**: `web/scripts/serve-spa.mjs`

**Intent**: Serve `build/client` for the visual tests without .NET or new dependencies.

**Contract**:
- `node scripts/serve-spa.mjs <port>` uses `node:http` and serves files from `build/client` with the right content types.
- Unknown paths fall back to `index.html`.
- `/api/*` returns 404. The tests mock it.

#### 2. Visual config and spec

**File**: `web/playwright.visual.config.ts`, `web/visual/home.visual.spec.ts`, `web/visual/__screenshots__/**`

**Intent**: A deterministic screenshot of every reachable state of the one view, in both themes and at one mobile width.

**Contract**:
- Config:
  - `testDir: "visual"`, and `webServer` runs `serve-spa.mjs`.
  - Projects, all Chromium:
    - `desktop-light` (1280×720)
    - `desktop-dark` (1280×720, `colorScheme: "dark"`)
    - `mobile` (390×844)
  - `expect.toHaveScreenshot` with `maxDiffPixelRatio: 0.01`, and `snapshotPathTemplate` under `visual/__screenshots__/{projectName}/{arg}{ext}`.
- Each test mocks `POST /api/maps/generate` with `page.route`:
  - fulfil with `fixtures/grids/seed-42.json` (then `seed-1.json` for the second map),
  - or with status 429,
  - or hold the request unanswered for the loading shots.
- It waits for `document.fonts.ready` before each shot.
- For the loading shots, the held handler just returns without awaiting anything or calling `fulfill`/`continue`. Each of those tests ends with `await page.unrouteAll({ behavior: 'ignoreErrors' })`. Never use `'wait'`, which hangs on a handler that never resolves.
- Screenshots:
  - `idle` (empty and disabled Download)
  - `idle-focus` (Tab to Generate)
  - `idle-hover` (hover Generate)
  - `loading-first`
  - `ready` (after Download PNG is enabled)
  - `loading-regenerate` (previous map dimmed)
  - `error-rate-limited`
  - `not-found`
- Hover and `idle-focus` run on the desktop projects only. They are N/A on mobile, where there is no pointer hover or keyboard.

#### 3. Scripts and CI

**File**: `web/package.json`, `web/scripts/ui-scan.mjs`, `.github/workflows/ci.yml`

**Intent**: Run the gate and the scan on every pull request.

**Contract**:
- Scripts:
  - `ui:scan`: `node scripts/ui-scan.mjs` has two scopes:
    - **Views** (`app/routes`, `app/root.tsx`, `app/components/*.tsx` outside `ui/`): the full `/10x-ui` pattern plus `dark:`.
    - **Primitives** (`app/components/ui`): only the colour-literal part (hex/rgb/hsl/oklch functions and palette classes), so unmodified `shadcn add` output passes.
    - **Token source** (`app/app.css`): `@custom-variant` or a `.dark` selector, so a `shadcn add` that rewrites the CSS can't silently switch dark mode to a class nothing sets (impl-review phase 1, F1).
    - It prints `file:line` hits with the scope that matched, and exits 1 if there are any.
  - `visual`: `playwright test -c playwright.visual.config.ts`
  - `visual:docker` and `visual:update`: run `npm ci && npm run build && npx playwright test -c playwright.visual.config.ts [--update-snapshots]` in `mcr.microsoft.com/playwright:v1.63.0-noble`, with the repo mounted, `--ipc=host --init`, `--user` set to the host UID/GID (so new baselines aren't owned by root), a writable `HOME`/npm cache, and `node_modules` in a named volume.
- CI:
  - The `web` job adds `npm run ui:scan`.
  - A new `visual` job with `container: { image: mcr.microsoft.com/playwright:v1.63.0-noble, options: --user 1001 --ipc=host }` (Playwright's GitHub Actions container example plus its Docker memory advice) runs `npm ci`, `npm run build` and `npm run visual` in `web/`, and uploads the report and diffs on failure.

#### 4. Rule and docs

**File**: `web/AGENTS.md`, `web/CLAUDE.md`

**Intent**: Make the contract outlast this session.

**Contract**:
- `web/AGENTS.md` gets a new `## UI` section:
  - Tokens live in `app/app.css`: `:root` plus the dark media query, published through `@theme inline`, recorded in the change's `tokens.md`. New values go there, never in a view.
  - Components live in `app/components/ui`. Check there before creating one, and add missing ones with `npx shadcn@latest add <name>` (then drop `radix-ui` if the component only needs it for `asChild`) or the same manual path.
  - Views (routes, `root.tsx`, components outside `ui/`): no literal colours, palette classes, `dark:` classes or arbitrary values. Primitives in `ui/`: no literal colours or palette classes, but shadcn's token-based `dark:` refinements are fine. `npm run ui:scan` enforces both.
  - Dark mode is OS-driven; never add `@custom-variant dark`.
  - After `shadcn add`, review the `app/app.css` diff. Remove any `.dark` block or `@custom-variant` the CLI wrote. Add any new variables it needs (for example `--popover`) by hand in both `:root` and the dark media block, and record them in `tokens.md`.
  - The screenshot gate lives in `visual/`. Update baselines only through `npm run visual:update`, and explain the visual change in the PR.
- `web/CLAUDE.md` "Commands and gotchas" gains `ui:scan`, `visual` and `visual:update`.

### Success Criteria:

#### Automated Verification:

- The scan passes: `npm --prefix web run ui:scan`
- The scan fails on a literal: temporarily add `bg-gray-900` to `home.tsx` and `dark:bg-primary` to `map-preview.tsx`, confirm `ui:scan` exits 1 and names both lines, then revert
- The visual gate passes in the CI image locally: `npm --prefix web run visual:docker`
- `ui:scan` fails on the token source: temporarily add `@custom-variant dark (&:is(.dark *));` to `app.css`, confirm it exits 1 and names the line, then revert
- The pull request's `web`, `visual` and existing checks are green

#### Manual Verification:

- The committed baselines (8 desktop-light, 8 desktop-dark, 6 mobile) show every state correctly in both themes and at mobile width; the reviewer has looked at each image
- `visual` is added to the required status checks on `main` (owner action)
- `web/AGENTS.md` has the `## UI` section, and nothing in the agent rules invites one-off values

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets — the corresponding `- [ ]` checkboxes for these items live in the `## Progress` section at the bottom of the plan.

---

## Testing Strategy

### Unit Tests:

- None new. The rendering tests (`npm test`) must pass unchanged, because the canvas bitmap is out of scope.

### Integration Tests:

- **E2E** (`e2e/tests/map.spec.ts`, both browsers, real API) is unchanged and must stay green after every phase. It proves the locators, the single canvas and the preview-equals-PNG invariant survive the restyle.
- **Visual** (`web/visual/`, Chromium, mocked API, in the CI container) covers the 7-state matrix at desktop light, desktop dark and a 390 px mobile width.

### Manual Testing Steps:

1. With the OS in light mode, open the app in Chrome. You should see the parchment page, the empty-state message and a disabled Download PNG.
2. Tab to Generate. The ring focus is visible. Press Enter. The loading overlay appears, then the map fits the viewport.
3. Click Generate again. The previous map dims, with no jump. Download PNG enables when the new map is drawn, and the file matches it.
4. Switch the OS to dark mode and repeat in Firefox.
5. Stop the API and click Generate. The destructive Alert appears and the placeholder comes back.
6. Open `/does-not-exist`. The styled 404 page appears, with "Back to the generator".

## Performance Considerations

`cn` and `class-variance-authority` add a few KB of gzipped JS to the bundle. That is well within the F1 bandwidth budget (`infrastructure.md:92`). There are no new fonts or images.

## Migration Notes

None. No data or API changes. Screenshot baselines are new files.

## References

- Research: `context/changes/home-view-ui-contract/research.md` (charges C1–C4, constraints, stack docs)
- Change identity and contract variant: `context/changes/home-view-ui-contract/change.md`
- `/10x-ui` checklist: `.claude/skills/10x-ui/references/ui-quality-checklist.md` (local tooling, gitignored)
- E2E locators: `e2e/tests/map.spec.ts:18,38,41-60`
- shadcn radix-nova Button: `https://ui.shadcn.com/r/styles/radix-nova/button.json` (fetched 2026-09-29)

## Progress

> SHAs rebased 2026-09-30 onto `main` after the e2e move (#8): 876dbbc → 06b97cf, 6efae5e → 8672ddb, 32d84c6 → 8336eea. The review reports keep the original SHAs.

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Contract Foundation (environment + tokens)

#### Automated

- [x] 1.1 Type check and build pass — 06b97cf
- [x] 1.2 Rendering tests unchanged — 06b97cf
- [x] 1.3 E2E still passes in both browsers — 06b97cf
- [x] 1.4 `app.css` has no palette classes or `@custom-variant` — 06b97cf

#### Manual

- [x] 1.5 Parchment/ink light and charcoal/parchment dark page in Chrome and Firefox — 06b97cf
- [x] 1.6 `tokens.md` records contrast ≥ 4.5:1 for text pairs and ≥ 3:1 for ring, both themes — 06b97cf

### Phase 2: Components and View Migration (C1, C2)

#### Automated

- [x] 2.1 Type check and build pass — 8672ddb
- [x] 2.2 Colour-literal scan returns 0 hits on home.tsx and components — 8672ddb
- [x] 2.3 No `dark:` classes or arbitrary values in routes — 8672ddb
- [x] 2.4 E2E passes in both browsers with the locators unchanged — 8672ddb

#### Manual

- [x] 2.5 Hover, focus ring and disabled state visible on both buttons, both browsers and themes — 8672ddb
- [x] 2.6 Error shows the destructive Alert, readable in both themes — 8672ddb

### Phase 3: Preview and Entry States (C3, C4)

#### Automated

- [x] 3.1 Type check and build pass — 8336eea
- [x] 3.2 Hardcoded-value scan returns 0 hits on the views, colour literals 0 on ui/ — 8336eea
- [x] 3.3 E2E passes in both browsers including the second-map round — 8336eea
- [x] 3.4 Rendering tests unchanged — 8336eea

#### Manual

- [x] 3.5 No layout jump on generate/regenerate; map fully visible at 1280×720 — 8336eea
- [x] 3.6 390 px width fits without horizontal scroll — 8336eea
- [x] 3.7 Network error returns the placeholder with the Alert and disables download — 8336eea
- [x] 3.8 Styled not-found page with a working link home — 8336eea

### Phase 4: Visual Gate and Guard

#### Automated

- [x] 4.1 `ui:scan` passes — 5de944b
- [x] 4.2 `ui:scan` fails on an injected literal — 5de944b
- [x] 4.3 Visual gate passes in the CI image locally — 5de944b
- [x] 4.4 Pull request checks green, including `visual` — 5de944b
- [x] 4.8 `ui:scan` fails on `@custom-variant` in app.css — 5de944b

#### Manual

- [x] 4.5 Every committed baseline reviewed in both themes and mobile — 5de944b
- [x] 4.6 `visual` added to required checks on `main` — 5de944b
- [x] 4.7 `web/AGENTS.md` has the UI rule section — 5de944b
