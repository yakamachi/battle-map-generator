# Home View UI Contract — Plan Brief

> Full plan: `context/changes/home-view-ui-contract/plan.md`
> Research: `context/changes/home-view-ui-contract/research.md`

## What & Why

The only screen, `web/app/routes/home.tsx`, works but was put together one feature at a time:
- Its colours are grey and red values typed straight into the markup, each with its own dark-mode twin.
- It has two hand-built buttons and a copied error paragraph.
- The preview blanks out and jumps on every Generate.
- A first visit and a bad link give no guidance.

This change introduces a small design-system contract: semantic tokens plus a few repo-owned components. It fixes those four charges before S-02, S-03 and S-04 add more UI to the same view.

## Starting Point

- `app.css` defines one token (the font).
- There is no components directory and no UI library.
- Dark mode follows the OS through Tailwind's `dark:` variant.
- The e2e (Chrome and Firefox) pins the button names, a single canvas and the `Seed: N` text.
- There is no visual testing and no lint.

## Desired End State

- **Look:** parchment and ink in light mode, charcoal and parchment in dark mode, all from tokens in `app.css`.
- **Components:** the view uses `Button`, `Alert` and a `MapPreview` frame, with 0 hardcoded values (`npm run ui:scan` in CI).
- **Preview:**
  - it always fits the viewport whole, with no layout jumps
  - it keeps the previous map dimmed while generating
  - before the first map, it explains what to do
- **Error pages:** a bad link shows a styled 404 with a way back.
- **Gate:** screenshots of every state (light, dark, mobile) are checked in CI.
- **Rule:** `web/AGENTS.md` tells the next agent where the tokens and components live.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Contract variant | No design system, so introducing it is the change | `app.css` has only a font token and there are no components | Research |
| Dependencies | Option (b): `cn` + `class-variance-authority` only, shadcn-style Button written by hand | shadcn conventions and later `shadcn add`, without `init`'s 5–6 packages or its `.dark` class variant | Plan (user) |
| Dark mode | Stays OS-driven: dark values in the `prefers-color-scheme` media query, no `@custom-variant dark` | A `.dark` class variant would turn off every `dark:` class, since nothing sets the class | Research |
| Palette | Parchment & ink motif (oklch, contrast checked, recorded in `tokens.md`) | Frames the black-on-white Scribble map like a DM handout, not a template | Plan (user) |
| Preview | Fit to the viewport at the map's aspect ratio; canvas always mounted; previous map dimmed while loading | Removes the jump and the scroll, so you see what you'll download | Plan (user) |
| Visual gate | Playwright `toHaveScreenshot`, Chromium, desktop light + dark + 390 px mobile, API mocked from `fixtures/grids`, run in the Playwright container | A real CI gate on the states with no new dependency, deterministic, off the rate limit | Plan (user) |
| Component scope | Only what home uses: Button (default, outline), Alert, MapPreview | Keeps the 2–3 component scope; S-03 and S-04 add Input/Label via the rule | Plan (user) |
| Guard | `ui:scan` Node script in CI (strict on views; colour literals only on `ui/` primitives, so `shadcn add` output passes) plus a `## UI` rule in `web/AGENTS.md` | There is no linter, and a failing check beats a forgotten rule | Plan + plan review F1 |

## Scope

**In scope:**
- tokens in `app.css` and `tokens.md`
- `cn`/`cva`, `components.json`, `lib/utils.ts`
- `Button`, `Alert`, `MapPreview`
- migrating `home.tsx`
- styling the `root.tsx` ErrorBoundary
- the visual spec, config and CI job
- `ui:scan`
- the `web/AGENTS.md` and `web/CLAUDE.md` rules

**Out of scope:**
- `shadcn init`, and `radix-ui`, Base UI, lucide or tw-animate
- a dark-mode toggle
- Input, Label or Select
- the canvas bitmap, renderer, tileset or download
- regenerate, parameters or login UI
- copy or language changes
- self-hosting Inter
- Firefox screenshots

## Architecture / Approach

Tokens are the single source:
1. `:root` holds the light values, and the dark media query holds the dark ones.
2. `@theme inline` turns them into utilities such as `bg-primary`.
3. The `ui/` components use only those utilities, and the view uses only the components and utilities.

`MapPreview` owns the frame and states. `home.tsx` keeps the download rule (current map === drawn map) and adds a "drawing on screen" flag.

The visual spec serves the built SPA with a tiny Node server, mocks `/api/maps/generate` with the fixture grids, and screenshots each state in the same Playwright image CI uses.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Contract foundation | deps, `components.json`, parchment and ink tokens, `tokens.md` | hand-picked oklch values fail contrast in one theme |
| 2. Components + view (C1, C2) | `Button`, `Alert`, `home.tsx` on tokens, scan 4 → 0 | a markup change breaks an e2e locator |
| 3. Preview + entry states (C3, C4) | `MapPreview` (fit, empty, loading), styled 404 | the state sequencing lets a stale drawing look downloadable |
| 4. Visual gate + guard | screenshot baselines in CI container, `ui:scan`, UI rule | baselines drift from font or network flakiness |

**Prerequisites:** PR #7 (S-01 closeout) merged; Docker for `visual:update`; the owner can edit branch protection.
**Estimated effort:** about 3–4 sessions, one PR per phase with an impl review between phases.

## Open Risks & Assumptions

- Inter loads from Google Fonts inside the CI container. If screenshots flake on font loading, the fallback is to block font requests in the visual spec and accept system-font baselines.
- It assumes the Playwright `v1.63.0-noble` image stays available and matches `@playwright/test` 1.63.0 in `package.json`.
- The oklch starting values are a proposal. Phase 1 may adjust them to meet contrast, and records the final values.

## Success Criteria (Summary)

- A DM sees a coherent parchment-and-ink page in either OS theme, with clear hover, focus and disabled states, and a preview that never jumps and always fits the screen.
- An e2e run with unchanged locators, the `visual` screenshot gate and `ui:scan` all pass in CI.
- The next slice's agent finds the rule, the tokens and the components, and has no reason to type a literal colour.
