---
date: 2026-09-29T13:40:44Z
researcher: Claude (Opus 5.5) with Karol Mitek
git_commit: 4aa5271
branch: home-view-ui-contract
repository: yakamachi/battle-map-generator
topic: "/10x-ui audit of the home view: charges, contract variant, and constraints on a restyle"
tags: [research, ui, web, home-view, tailwind, design-tokens, shadcn]
status: complete
last_updated: 2026-09-29
last_updated_by: Claude (Opus 5.5)
---

# Research: /10x-ui audit of the home view

**Date**: 2026-09-29T13:40:44Z
**Researcher**: Claude (Opus 5.5) with Karol Mitek
**Git Commit**: 4aa5271 (`main`, S-01 Phase 4). The working tree differs from it only in `CLAUDE.md` (lesson pack) and this change folder.
**Branch**: home-view-ui-contract
**Repository**: yakamachi/battle-map-generator

## Research Question

Run the `/10x-ui` two-way audit on `web/app/routes/home.tsx`, the only route. It should answer four questions:

1. Where do values and components live, and does the view read them?
2. Which token or component should have covered each literal in the view?
3. What depends on the view's markup and would limit a restyle?
4. What do current Tailwind v4 and shadcn/ui docs say about adding the contract to this stack?

The output is 3–5 charges for `/10x-plan`.

## Summary

- **No design system exists.**
  - The only token source, `web/app/app.css`, publishes a single variable, `--font-sans` (`app.css:3-6`).
  - There is no components directory, and there are no UI libraries in `web/package.json`.
  - The view reads zero semantic token classes and has 4 hardcoded-value hits (`home.tsx:101,109,116,121`).
  - The contract variant is **"no design system"**: introducing the contract is the change (see `change.md`).
- **Four charges**, listed under `## Charges`:
  - **C1** Missing tokens: palette literals, with dark mode written per element.
  - **C2** Missing shared component: two hand-built buttons and two copied alerts, with no hover or focus-visible state.
  - **C3** Accidental architecture: the preview's lifecycle makes the page jump and blank out on every generate.
  - **C4** Accidental architecture: the idle view and the unknown-route error page leave the user with no guidance.
- **Hard constraints on the restyle:**
  - The e2e selectors: the button names "Generate" and "Download PNG", the native `disabled` attribute, exactly one `<canvas>`, and one element whose whole text is `Seed: <digits>`.
  - The canvas bitmap must stay 4200×2800.
  - Dark mode stays OS-driven (`prefers-color-scheme`), with no class toggle.
- **For the user to decide** (`change.md` condition 1): whether to add dependencies at all, and which ones.
  - Current shadcn (CLI 4.21.0) builds `Button` from `cn` + `class-variance-authority` + `radix-ui` or `@base-ui/react`.
  - `shadcn init` also adds `shadcn`, `lucide-react` and `tw-animate-css`, plus a `.dark` class variant that clashes with this repo's media-query dark mode.

## Charges

Each charge has file:line evidence and the effect on the user. The scan command is the one from `/10x-ui` (hardcoded-value scan), run on `home.tsx` and `root.tsx`. It found 4 hits in `home.tsx` and 0 in `root.tsx`.

### C1: Missing tokens (palette literals, dark mode per element)

- **Evidence:**
  - `home.tsx:101`: `bg-gray-900 … text-white … dark:bg-gray-100 dark:text-gray-900` (Generate)
  - `home.tsx:109`: `border-gray-900 … dark:border-gray-100` (Download PNG)
  - `home.tsx:116` and `home.tsx:121`: `text-red-700 dark:text-red-400` (both alerts)
  - `app.css:10`: `@apply bg-white dark:bg-gray-950` on `html, body`
  - Semantic token classes such as `bg-primary`, `text-destructive` or `border-border` appear 0 times in `home.tsx` or `root.tsx`.
- **What should have covered them:**
  - `primary` / `primary-foreground` for Generate
  - `border` / `foreground` for the outline button
  - `destructive` for the alerts
  - `background` / `foreground` for the page
  - A radius token for `rounded`
- **Effect on the user:** each colour exists only where it was typed, and so does its dark twin. Any new control that forgets a `dark:` class shows light-theme colours on the dark page. S-02 (regenerate), S-03 (parameter form) and S-04 (login) will each add controls to this view (`roadmap.md:102-138`). Changing the accent later means editing every class by hand.

### C2: Missing shared component (two hand-built buttons, two copied alerts)

- **Evidence:**
  - `home.tsx:97-104` and `home.tsx:105-112` are two `<button>`s. Both have the shape classes `rounded px-4 py-2 disabled:opacity-50` typed out separately, and each has its own colour classes.
  - `home.tsx:115-119` and `home.tsx:120-124` are the same `<p role="alert" className="text-red-700 dark:text-red-400">` written twice.
  - There is no `web/app/components/` directory.
- **States, from the classes on those lines:**
  - No `hover:` or `focus-visible:` class exists on either button, so hover gives no feedback.
  - Keyboard focus falls back to the browser default outline, with no token behind it.
  - Disabled is `opacity-50` on the native `disabled` attribute. That is adequate, and the e2e depends on it.
- **Effect on the user:** the two buttons in the same row don't respond to hover. Keyboard users get whatever outline the browser draws, which differs between Chrome and Firefox, and both browsers are required (`prd.md:126`). Every upcoming slice will copy these classes a third and fourth time.

### C3: Accidental architecture: the preview blanks and jumps on every generate

- **Evidence:**
  - `home.tsx:40`: `map` is `null` unless `status.kind === "ready"`.
  - `home.tsx:71-72`: clicking Generate sets status to `loading`.
  - `home.tsx:125`: `{map && <canvas …>}` then unmounts the canvas.
  - `render.ts:33-34`: after a new map arrives, the canvas mounts at the browser default 300×150 and is sized only once the tile atlas has loaded and `renderMap` runs.
  - `home.tsx:129`: the preview is `maxWidth: 100%` of `container` (up to 1536 px at the widest Tailwind breakpoint).
- **Effect on the user:**
  - Regenerating removes the current map at once and collapses the page.
  - Then a 300×150 empty box appears.
  - Then the page grows to the full map height.
  - With 3:2 maps, a preview 1280 px wide is about 853 px tall, which is taller than a 720 px viewport (Playwright's desktop default, `playwright.config.ts:20-21`). The user has to scroll to see the whole map they are about to download.
  - There is no loading placeholder, only the button label "Generating…" (`home.tsx:103`).
  - The PRD allows generation to take up to 5 minutes (`prd.md:122-124`).

### C4: Accidental architecture: empty entry and the unknown-route page

- **Evidence:**
  - Idle state, `home.tsx:93-114`: a heading, an enabled Generate, and a disabled Download PNG. Nothing says what the app does or why Download is disabled.
  - Deep link to an unknown path: the API serves `index.html` as the SPA fallback (`api/Program.cs:135`). React Router then renders `root.tsx:48-75` ErrorBoundary with unstyled `404` / "The requested page could not be found." and no way back to the generator.
- **Effect on the user:** a first-time DM sees a page with no guidance, and a disabled button with no explanation. A mistyped or stale link shows a bare error with no link home.
- **Logged-out entry is N/A for now.** There is no auth until S-04 (`roadmap.md:127-138`). What an anonymous user sees is still open (PRD Open Questions #3, `prd.md:189-191`).

### Not a charge: agent rules

`AGENTS.md` and `CLAUDE.md` (root and `web/`) contain no UI styling rules. That includes rules inviting arbitrary or one-off values. `web/CLAUDE.md:13` notes that no lint tooling exists. There is no rule causing the drift. There is also no rule to stop it, so the "Make it stick" step has to add one to `web/AGENTS.md`, outside the 10x-cli block.

## 7-state matrix, current state (for the plan's states phase)

| State | Current (`home.tsx`) | Source |
|---|---|---|
| default | palette literals | `:101,109` |
| hover | none on either button | `:101,109` |
| focus-visible | browser default only | `:101,109` (no `focus-visible:` class) |
| disabled | native `disabled` + `opacity-50` | `:100,108,101,109` |
| error | text in `role="alert"`, red literal; 5 messages: rate-limited, network, http (`:20-29`), render failure (`:62`), export failure (`:89`) | `:115-124` |
| empty | idle view without guidance (C4) | `:93-114` |
| loading | label only, canvas unmounted (C3) | `:103,125` |

## Detailed Findings

### Token and component source (source → views)

- `app.css:1` is `@import "tailwindcss"`.
- `app.css:3-6` is `@theme { --font-sans: "Inter", … }`, the only token.
- `app.css:8-15` sets the page background with palette classes and `color-scheme: dark` under `@media (prefers-color-scheme: dark)`.
- Views that read tokens: none apart from the `font-sans` default.
- Views that import shared components: none. No components directory exists, and `web/app/` holds only `routes/`, `map/` and `api/`.
- Inter comes from Google Fonts (`root.tsx:13-24`). `web/public/` has only `favicon.ico`.

### What depends on the markup (limits on a restyle)

- **E2E**, the only spec: `web/e2e/map.spec.ts`.
  - `:38` `getByRole("button", { name: "Generate" })`. The match is a substring and case-insensitive, so "Generating…" and "Generate map" also match. Renaming the button to something without "generate" breaks it.
  - `:41-42` `getByRole("button", { name: "Download PNG" })` plus `toBeEnabled`. This needs a real `<button>` with the native `disabled` attribute, not only `aria-disabled`.
  - `:44-57` `locator("canvas")` in strict mode. There must be exactly one canvas, it must be visible with a non-zero size, and it must be 4200×2800 intrinsically.
  - `:18,77-78` hash the first canvas's bitmap and compare it to the downloaded PNG.
  - `:59-60` `getByText(/^Seed: \d+$/)`. Exactly one element must have the whole text `Seed: <digits>`. The label can't be split or reworded.
  - Nothing asserts on the alerts, the canvas `aria-label`, the `<h1>`, or the ErrorBoundary.
- **Vitest** (`render.test.ts`, `tileset.test.ts`) uses its own canvases and never mounts `home.tsx`.
  - `render-baselines.json` hashes canvas bitmaps per fixture and browser, so CSS can't affect it.
- **Visual testing:** none. There is no `toHaveScreenshot`, `toMatchSnapshot` or snapshot directory.
  - Playwright 1.63 is installed (`web/package.json`), with `chromium` and `firefox` desktop projects at 1280×720 and no mobile or dark projects (`playwright.config.ts:20-21`).
  - The e2e runs against the built SPA served by .NET (`playwright.config.ts:22-32`).
  - `POST /api/maps` is limited to 10 per minute per IP, and the spec already uses 4 (`map.spec.ts:82`). A screenshot gate that generates maps adds to that budget.
- **Lint and hooks:** none. No ESLint, Prettier, Stylelint, Biome, Husky, lint-staged or Lefthook, and no git hooks (`web/CLAUDE.md:13`). The only static gate is `tsc` in strict mode (`npm run typecheck`).
- **CI jobs touching `web/`** (`.github/workflows/ci.yml`):
  - `web` (typecheck, build) at `:44-61`
  - `contract` at `:65-99`
  - `web-tests` at `:102-119`
  - `e2e` at `:123-159`
- **Deploy:** `deploy.yml` builds the SPA into `api/wwwroot`. The F1 plan's 165 MB/day bandwidth cap counts SPA assets (`infrastructure.md:92,100`). Extra dependencies add bytes to every cold visit.

### Stack docs (external, verified 2026-09-29)

Sources: Context7 `/websites/ui_shadcn` and `/websites/tailwindcss`, the live pages `ui.shadcn.com/docs/{installation/react-router,installation/manual,theming,components-json,cli}`, and `tailwindcss.com/docs/{dark-mode,theme,colors}`. Versions were checked with `npm view` on 2026-09-29: shadcn 4.21.0, cn 0.4.0 (repo shadcn-ui/cn), class-variance-authority 0.7.1, radix-ui 1.6.7, @base-ui/react 1.8.0, tw-animate-css 1.4.0. Context7 still serves some outdated snippets (`clsx` + `tailwind-merge`, forwardRef + `@radix-ui/react-slot`), so where they disagree, the live pages above take precedence.

- **`npx shadcn@latest init`** (React Router template, reads the tsconfig `~/*` alias). It writes:
  - `components.json`
  - `lib/utils.ts` (`export { cn } from "cn"`)
  - CSS imports `tw-animate-css` and `shadcn/tailwind.css`
  - `@custom-variant dark (&:is(.dark *))`
  - `:root` / `.dark` oklch token blocks
  - `@theme inline`
  - a base layer (`border-border outline-ring/50`, `body bg-background text-foreground`)

  Dependencies: `shadcn`, `class-variance-authority`, `cn`, `lucide-react`, `tw-animate-css`, plus the primitive package. The default base is now Base UI (`@base-ui/react`); Radix is `-b radix` → `radix-ui`. `cssVariables` cannot be changed after init.
- **`add button`:**
  - The registry item (`ui.shadcn.com/r/styles/radix-nova/button.json`, fetched 2026-09-29) lists `dependencies: ["cn"]`. Its source imports `class-variance-authority`, `cn`, and `{ Slot } from "radix-ui"` (the Slot is used only for `asChild`).
  - Variants: default, outline, secondary, ghost, destructive, link.
  - Focus style: `focus-visible:ring-3 ring-ring/50`.
  - It uses `dark:` classes (`dark:bg-input/30`, `dark:hover:bg-muted/50`, …).
- **Manual path, no init:** install only what the component imports, add the tokens, `@theme inline` and base layer by hand, and copy `button.tsx`. `components.json` is optional and only needed for future CLI use. Inference, not documented: without `asChild`, the Button needs only `class-variance-authority` + `cn`.
- **Dark mode:**
  - Tailwind v4's `dark:` variant follows `prefers-color-scheme` by default.
  - shadcn's `@custom-variant dark (&:is(.dark *))` switches it to a class, which this app never sets. That would turn off every `dark:` class, including the ones inside shadcn's Button, while the media-query tokens still switched.
  - To keep OS-driven dark mode: put the dark values in `@media (prefers-color-scheme: dark) { :root { … } }` and do not add the custom variant.
- **`@theme` vs `@theme inline`:** values that change per theme must be published with `@theme inline` (`--color-primary: var(--primary)`). A nested `var()` resolves where it is defined, which gives the wrong fallback. Static values such as `--font-sans` can stay in plain `@theme`.
- **Other stack details:**
  - Tailwind v4 gives buttons `cursor: default`. shadcn documents a base-layer fix, and init has a `--pointer` flag.
  - `app.css:10`'s `bg-white dark:bg-gray-950` and shadcn's `body { @apply bg-background text-foreground }` would overlap. Keep one.

## Code References

- `web/app/routes/home.tsx:40,71-72,125`: `map` gating that unmounts the canvas while loading (C3)
- `web/app/routes/home.tsx:93-114`: idle layout (C4)
- `web/app/routes/home.tsx:97-112`: two hand-built buttons (C1, C2)
- `web/app/routes/home.tsx:115-124`: two copied alerts (C1, C2)
- `web/app/routes/home.tsx:129`: inline `maxWidth: 100%` preview sizing (C3)
- `web/app/map/render.ts:33-34`: canvas sized only at render time (C3)
- `web/app/app.css:1-15`: the only token source, and body colour literals (C1)
- `web/app/root.tsx:48-75`: unstyled ErrorBoundary (C4)
- `web/e2e/map.spec.ts:18,38,41-60,77-78`: locators and pixel checks that must survive
- `web/playwright.config.ts:18-32`: desktop Chrome and Firefox, built SPA via .NET

## Architecture Insights

- The canvas is off-limits to CSS work beyond display sizing. `web/AGENTS.md` (Map rendering) fixes 140 px per square, one canvas, and preview = that canvas scaled with CSS. The contract covers only the page around the preview, and C3's fix must not change the canvas `width`/`height` attributes.
- Tailwind v4 already published through `@tailwindcss/vite` is the stack's own token path (`:root` values → `@theme inline`). Whether components come from shadcn is the open dependency question, not a given.
- A kitchen-sink route would ship with the production SPA unless it is gated, for example on `import.meta.env.DEV`. A Playwright `toHaveScreenshot` gate needs no new dependency, but it would add Linux-and-font-specific baselines. Inter is fetched from Google Fonts at runtime, so rendering depends on the network.

## Historical Context (from prior changes)

- `context/changes/first-map-download/plan.md:48-57` ("What We're NOT Doing") deferred S/M/L and encounter type (S-03), regenerate (S-02) and login (S-04). Styling is not mentioned; the S-01 home screen was scoped as a minimal flow (`plan.md:322-329`: disabled while loading, `max-width: 100%` preview, seed shown, readable errors, a real title). *PR #7, still unmerged, moves this folder to `context/archive/2026-09-25-first-map-download/`.*
- `context/changes/first-map-download/research.md:208-213` chose plain Canvas 2D over Konva. That applies to the canvas only. `research.md:244` suggested `image-rendering: pixelated` for the preview, which was not adopted.
- No prior document chooses or rejects shadcn, a component library, a palette, a dark-mode strategy, a UI language, or accessibility or responsive rules. The implemented UI is English (`root.tsx:28` `lang="en"`), while the product docs are Polish.

## Related Research

- `context/changes/first-map-download/research.md`: S-01 stack and rendering research (canvas, atlas, download).

## Open Questions

1. **Dependencies (user decision, `change.md` condition 1).**
   - (a) Full `shadcn init` + `add button`: 5–6 dependencies, and it adds the `.dark` class variant, which must be removed.
   - (b) Manual shadcn-style Button: `cn` + `class-variance-authority`, plus `radix-ui` only if `asChild` is needed. Tokens are written by hand, with `components.json` for later CLI use.
   - (c) No new dependencies: a repo-owned `Button` with a variant map over token classes.
   - The alert and preview frame are small enough to be repo-owned in any option.
2. **Visual gate form:** a dev-only kitchen-sink route, a Playwright `toHaveScreenshot` project (desktop + one mobile width), or both. It must respect the generate rate limit and account for Inter coming from Google Fonts.
3. **Preview sizing (C3):** fit the preview to the viewport height (for example `max-height` + `object-fit`-like sizing via `aspect-ratio`) or keep full width with scrolling. Either way the whole map must stay visible, with no cropping (grid guardrail, `prd.md:65-69`).
4. **Anticipating later slices:** the component set should probably leave room for S-03's form controls and S-04's inputs. Building them now would break the "2–3 components" scope.
5. **Palette:** neutral shadcn defaults, or a named motif (for example parchment and ink, matching the Scribble tileset). That is a product and design choice for the plan.
