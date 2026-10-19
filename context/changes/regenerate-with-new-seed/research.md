---
date: 2026-10-18T23:56:15+02:00
researcher: Claude (Opus 5.5), for Karol Mitek
git_commit: 6b6939f
branch: regenerate-with-new-seed
repository: battle-map-generator
topic: "S-02: making regeneration discoverable through Generate-button states (Ready, Rolling, Done flash, Reroll, Error)"
tags: [research, web, home-view, generate-button, map-preview, visual-gate, e2e, s-02]
status: complete
last_updated: 2026-10-19
last_updated_note: "Follow-up: dice icon and animation sources"
last_updated_by: Claude (Opus 5.5)
---

# Research: S-02, the Generate button's states

**Date**: 2026-10-18T23:56:15+02:00
**Researcher**: Claude (Opus 5.5), for Karol Mitek
**Git Commit**: 6b6939f
**Branch**: regenerate-with-new-seed
**Repository**: battle-map-generator

## Research Question

What does the code need for the S-02 design in `change.md`? The button keeps its "Generate" label and shows these states:

- **Ready**: primary style with a d20 icon. Also shown again when the parameters change after a map exists.
- **Rolling**: the d20 tumbles.
- **Done**: about 600 ms of flash on both the button and the preview.
- **Reroll**: accent style, dice+↻ icon and a tooltip.
- **Error**: a shake, then back to Ready.

It also needs an `aria-live` announcement, `prefers-reduced-motion` support, plain `@keyframes` and an updated S-02 roadmap outcome. This research covers where each piece hooks in, which constraints and tests it touches, and what the plan still has to decide.

## Summary

- **Regeneration already works.** On the home page, every Generate click sends `seed: null` (`web/app/routes/home.tsx:83-87` → `web/app/api/client.ts:82`). The API then draws a fresh 32-bit seed from the OS RNG (`api/Maps/MapEndpoints.cs:67`, `:94-99`). With the same form values, each click therefore gets a new seed. S-02's work is the UI only. No API, contract or fixture changes are needed.
- **The home view already has every input the button needs.** `status.kind`, `busy` and `drawnMap` are at `home.tsx:59-78`. `drawnMap.parameters` holds `{ roomCount, encounter, bossSize | null }` (`web/app/api/schema.d.ts:166-171`), so the "parameters changed since the last map" check can compare the form against the drawn map. No new state is needed for that.
- **No icon library is installed** (`web/package.json` dependencies; `lucide-react` was left out on purpose, `web/AGENTS.md:36`). The d20 and the dice+↻ icon must be inline SVG using `currentColor`. The button already sizes child SVGs to `size-4` (`web/app/components/ui/button.tsx:8`).
- **The tests that find the button by name pin two names: "Generate" and "Generating…".** e2e uses "Generate" in 4 specs. The visual gate uses "Generate" (exact) in 2 specs (home and library), and the home spec also finds the busy button by the name "Generating…" (`web/visual/home.visual.spec.ts:57`). Keeping the label "Generate" keeps them green, as long as the icons are `aria-hidden`. Whether Rolling still says "Generating…" is a decision for the plan (Open Questions).
- **Screenshots are taken with `animations: "disabled"`** (`web/visual/home.visual.spec.ts:32-36`). The Done flash must therefore be a CSS animation laid over a settled style, not a JS timer that holds a "done" class. Otherwise the `ready` and `ready-boss` shots depend on timing. At least the `ready`, `ready-boss`, `loading-*`, `idle*` and `error-rate-limited` baselines will change, because of the icon and the Reroll style, and need `npm run visual:update`.
- **Pitfall: the Done flash must follow a finished Generate, not a change of `drawnMap`.** Save calls `setStatus({ kind: "ready", map: saved.map })` (`home.tsx:182`) with a new object, which redraws the canvas and produces a new `drawnMap`. A flash keyed on `drawnMap` would fire after Save.

## Detailed Findings

### The home view's state (where the button's state comes from)

- `Status` has four kinds: `idle`, `loading`, `ready` with a map, and `error` with a message (`web/app/routes/home.tsx:33-37`).
- `busy` is true while a request is in flight **or** while a map is in state but not yet drawn: `previewState === "loading"` (`home.tsx:69-77`). The comment at `home.tsx:76` says this deliberately covers both the button and the frame. Rolling should keep using `busy`, so the tumble lasts until the map is drawn, not only until the response arrives.
- `drawnMap` (from `useMapCanvas`, `web/app/map/use-map-canvas.ts:62`) is non-null only when the map in state is the one on the canvas. Done and Reroll can be derived from it.
- The form values are separate state (`home.tsx:61-63`). `bossSize` keeps its value when the encounter is switched to skirmish (the field only hides, `home.tsx:142`), while `drawnMap.parameters.bossSize` is `null` for a skirmish (`schema.d.ts:170`). A staleness check therefore compares `bossSize` only when `encounter === "boss"`. That mirrors the request at `home.tsx:86`.
- The error paths reach `status.kind === "error"` in two ways: a failed request (`home.tsx:88-93`) and a render failure (`home.tsx:67`, `use-map-canvas.ts:49-54`). A failed request also calls `forgetShownMap()` (`home.tsx:88`). The Error shake can trigger on the change to `status.kind === "error"`.
- The current button is `<Button onClick={onGenerate} disabled={busy}>{busy ? "Generating…" : "Generate"}</Button>` (`home.tsx:161-163`).
- `Seed: N` is shown next to the buttons while a map is drawn (`home.tsx:202-204`). Two e2e specs and the visual specs read that text (see Tests). It stays.

### The preview frame (the second flash target)

- `MapPreview` (`web/app/components/map-preview.tsx:23-69`) is shared by home (`home.tsx:208`) and the saved-map view (`web/app/routes/saved-map.tsx:105`). A preview flash should be opt-in through a prop, or driven by home only, so `/maps/:id` does not flash when a saved map loads. That is a decision for the plan, but the shared use is a fact.
- While loading, the canvas already fades to `opacity-40` with `transition-opacity` (`map-preview.tsx:48-52`) and shows a "Generating…" badge (`map-preview.tsx:60-66`, `loadingLabel` default `:30`). The badge text is not a button, so the button name locators do not match it.
- The frame sets `aria-busy` while loading (`map-preview.tsx:40`).

### The button primitive and the design tokens

- `buttonVariants` has `default`, `outline` and `destructive` (`web/app/components/ui/button.tsx:11-19`). Its header says to add a variant there, token classes only, when a view needs one (`button.tsx:5-6`). Reroll's "accent style" therefore means a new variant. The `secondary` and `accent` tokens already exist in both themes (`web/app/app.css:26-31`, `:50-55`, published at `:74-79`). In both themes `accent` has the same value as `secondary` (light `oklch(0.92 0.025 80)`, dark `oklch(0.3 0.015 60)`). Its text pairs with `accent-foreground`, the same value as `foreground`.
- A new or changed colour token requires a contrast check in both themes, recorded in this change's folder (`web/AGENTS.md:35`). Reusing the existing `accent` and `accent-foreground` adds no token. A new flash or glow colour (for example `ring` at some alpha) is still a decision for the plan.
- `ui:scan` (`web/scripts/ui-scan.mjs`) fails views on literal colours, palette classes, any `dark:` class and arbitrary `px`/`rem` values (`-[13px]`). It does not scan `@keyframes` in `app.css` for colours; it scans `app.css` only for `@custom-variant` and `.dark`. Keyframes and any `@utility` for the animations belong in `app.css`, using `var(--…)` tokens.
- No animation is in use yet. `dialog.tsx`'s header records that enter and exit animations were left out because they need `tw-animate-css` and a `data-open` variant (`web/app/components/ui/dialog.tsx:11`). Tailwind v4's built-in `animate-spin` and `animate-pulse`, and `motion-safe:`/`motion-reduce:` variants, need no extra package. Their availability under Tailwind 4.2 was not checked against the docs in this research (see Open Questions).

### Tooltip and announcement

- There is no tooltip primitive in `app/components/ui/`. Options are a native `title` attribute (no dependency, no styling, not shown on touch) or a Radix tooltip. `radix-ui` is installed, but the AGENTS rule keeps it to `dialog.tsx` (`web/AGENTS.md:37`).
- `Alert` renders `role="alert"` for `destructive` and `role="status"` otherwise (`web/app/components/ui/alert.tsx:27`). The page has no other live region today. A visually hidden `role="status"` region announcing "New map generated" would be new markup. `getByRole("alert")` in `home.visual.spec.ts:126` selects only destructive alerts, so a `status` region does not collide with it.

### Tests and gates this touches

- **e2e** (`e2e/tests/`): `getByRole("button", { name: "Generate" })` appears in `library.spec.ts:17`, `parameters.spec.ts:28` and `map.spec.ts:38`, and as `exact: true` in `auth.spec.ts:34`. `map.spec.ts:83-89` already generates twice and asserts that the second preview hash differs from the first, which is S-02's end-to-end behaviour. `Seed: N` is read at `map.spec.ts:59` and `library.spec.ts:19`. Inline SVGs marked `aria-hidden` leave the accessible name "Generate" unchanged.
- **Visual gate** (`web/visual/home.visual.spec.ts`): it uses `name: "Generate", exact: true` at `:41`, `:46`, `:79` and `:85`, `name: "Generating…"` at `:57` (`waitForLoading`), and `library.visual.spec.ts:89`. The shots are `idle`, `idle-focus`, `idle-hover`, `loading-first`, `ready`, `loading-regenerate`, `error-rate-limited`, `boss-form` and `ready-boss` (`:59-158`), in the projects whose baseline folders are `desktop-light`, `desktop-dark` and `mobile` (`web/visual/__screenshots__/`). `shot()` passes `animations: "disabled"` (`:32-36`). Playwright then finishes finite CSS animations and resets infinite ones before the capture. This was not checked against the docs in this research. Baselines are updated only through `npm run visual:update` in Docker, with the visual change explained in the PR (`web/CLAUDE.md`).
- **The `ready` shot after the change shows the Reroll style.** The form is unchanged after Generate, so the button is in Reroll. The `loading-regenerate` shot shows Rolling with the tumble reset to its first frame.
- **Unit and component tests**: Vitest runs `app/**/*.test.ts` only (`web/vitest.config.ts`, `include`), in browser mode in Chromium and Firefox. No React testing library is installed. The cheapest testable seam is a pure function, for example one that maps `status`, `busy`, `drawnMap` and the form values to `"ready" | "rolling" | "reroll" | "error"`, tested in a `.test.ts` file. Rendering components would need a new dev dependency (`vitest-browser-react`) and an `include` for `.tsx`.

### Seed behaviour (unchanged and verified)

- `generateMap` sends `seed: seed ?? null` (`web/app/api/client.ts:82`), and home never passes a seed (`home.tsx:83-87`).
- When the request's seed is `null`, the API calls `NewSeed()` (`api/Maps/MapEndpoints.cs:67`), which reads 4 bytes from `RandomNumberGenerator` (`:94-99`). Two clicks get the same seed only on a 1-in-2³² match. The visual spec pins `seed: null` in the request body (`home.visual.spec.ts:153`).

## Code References

- `web/app/routes/home.tsx:33-37` - `Status` union
- `web/app/routes/home.tsx:65-78` - `map`, `drawnMap`, `previewState`, `busy` derivation
- `web/app/routes/home.tsx:80-94` - `onGenerate` (no seed, error path calls `forgetShownMap`)
- `web/app/routes/home.tsx:161-163` - the Generate button today
- `web/app/routes/home.tsx:182` - Save's `onSaved` replaces the map in state (flash pitfall)
- `web/app/routes/home.tsx:202-204` - `Seed: N` text
- `web/app/map/use-map-canvas.ts:38-62` - render effect, `renderedMap`/`shownMap`, `drawnMap`
- `web/app/components/map-preview.tsx:23-69` - preview frame, shared with `saved-map.tsx:105`
- `web/app/components/ui/button.tsx:7-29` - `buttonVariants` (where a Reroll variant goes)
- `web/app/app.css:15-62` - tokens (`accent`, `secondary`, `ring` in both themes)
- `web/app/components/ui/alert.tsx:27` - `alert` vs `status` role
- `web/scripts/ui-scan.mjs` - view, primitive and token scan rules
- `web/vitest.config.ts` - `include: ["app/**/*.test.ts"]`, browser mode in Chromium and Firefox
- `web/visual/home.visual.spec.ts:32-36,41,57,59-158` - screenshot options, name locators, states
- `e2e/tests/map.spec.ts:83-89` - two Generates, different preview hash
- `api/Maps/MapEndpoints.cs:67,94-99` - fresh seed when none is sent

## Architecture Insights

- The button's state is a view concern derived entirely from state home already holds. A pure `generateButtonState(...)` helper next to `home.tsx` (or in `app/lib/`) keeps it testable under the existing `.test.ts` setup.
- Motion must split into a settled style plus a transient CSS animation. That keeps the screenshot gate deterministic and lets `prefers-reduced-motion` turn off the motion alone, without hiding the state. The state still shows through the icon and the colour, as `change.md` requires ("not by colour alone").
- To replay the flash on every finished Generate, the plan needs a trigger that changes each time, for example a `key` or `data-` counter that increments in `onGenerate` once the new map is drawn. Keying it on `drawnMap` would also fire after Save (see Summary).
- `web/AGENTS.md:47` already asks that changes to markup e2e finds (button names, the canvas, `Seed: N`) keep e2e green. This change keeps all three.

## Historical Context (from prior changes)

- `context/archive/2026-09-29-home-view-ui-contract/research.md:51`, `:166-172` - shadcn init would have added `lucide-react` and `tw-animate-css`. The project chose hand-copied primitives without them, which still holds (`web/AGENTS.md:36`).
- `context/archive/2026-09-29-home-view-ui-contract/research.md:94` - earlier, regenerating removed the current map at once. That was fixed (the dimmed previous map stays, `use-map-canvas.ts:28-31`). This still holds, and the `loading-regenerate` shot pins it.
- `context/archive/2026-09-25-first-map-download/plan-brief.md:55` and `context/archive/2026-09-30-encounter-parameters/plan-brief.md:43` - both deferred "the regeneration experience" to S-02. Neither defined it further.
- `context/foundation/roadmap.md` S-02 Unknowns (2026-10-18) - "may be mostly done". The seed and e2e parts are confirmed above. The "generuj ponownie" label it mentions is superseded by the user's choice to keep "Generate" (`change.md` Notes).

## Related Research

- `context/archive/2026-09-29-home-view-ui-contract/research.md` - tokens, primitives and the screenshot gate this change builds on.

## Follow-up (2026-10-19): dice icon and animation sources

**Question**: is there a library that does the dice (icon and roll animation) for us, or do we bring our own images?

**Answer**: neither a dice library nor raster images. Copy one or two SVG paths inline and animate them with CSS `@keyframes`. This matches how primitives were hand-copied without `lucide-react` (`web/AGENTS.md:36`).

- **Icon sets with a d20** (Iconify search API, 2026-10-19, query `d20`, 12 hits): `mdi:dice-d20` and `mdi:dice-d20-outline` (Apache-2.0), `fa7-solid:dice-d20` (Font Awesome Free, CC BY 4.0), `la:dice-d20` (Line Awesome, MIT), `iconoven:d20` and `iconoven:dice-d20` (MIT), `at-icons:d20` (MIT), and `game-icons:dice-twenty-faces-twenty` (CC BY 3.0). Lucide, which shadcn uses, has `dices` and `dice-1`…`dice-6` but no d20.
- **Reroll candidates**: `mdi:dice-multiple(-outline)`, `lucide:dices` and `game-icons:rolling-dices`. Iconify search for "dice reroll" returned 0 hits, so a combined d20+↻ glyph has to be composed by hand.
- **At button size (16–20 px)**, rendered on the app's tokens in both themes: the detailed `game-icons` d20 turns to mud, while the hexagon silhouettes (`mdi` outline, `fa7`, `la`, `at-icons`) stay readable. The `mdi:dice-d20-outline` "20" reads as a d20 instantly. Icons at `size-5` (20 px) read better than the button's default `size-4`.
- **Licences**: Apache-2.0 and MIT need the licence notice kept with copied code (a source comment next to the path). CC BY 3.0 and 4.0 need visible attribution, and the app has no credits page (the tileset is CC0). That rules out `game-icons` and makes Font Awesome less convenient.
- **Animation libraries are overkill**: `@3d-dice/dice-box` (MIT, 11.4 MB unpacked, WebGL physics) and Lottie (runtime plus JSON per animation) are both heavy for a 20 px icon. A dice library also adds nothing that CSS transforms on an inline SVG don't already give.
- **Prototype**: a throwaway `demo.html` in the session scratchpad (not committed) compares three CSS-only takes: (A) a tumble with 90° hops, (B) a wobble with the face number flickering via `steps(1)` across stacked `<text>` faces, then landing on 20, (C) a CSS-perspective flip. Each has the Done ring pulse, the preview arrival glow, the Reroll accent style, the error shake and a reduced-motion switch. Screenshots confirmed that B's faces cycle 5 → 11 → 2 at 90 ms intervals.
- **Gate fit for B**: an infinite `steps()` animation is reset to its first frame under `animations: "disabled"`. This is per Playwright's documented behaviour, but not re-checked in Context7 here (Open Question 5). The `loading-*` screenshots would then show the first flicker face, not a random one. The resting face ("20") is a static `<text>` and needs no animation.

### Chosen direction (user decision, 2026-10-19): variant D4

The prototype is kept as a reference at `context/changes/regenerate-with-new-seed/prototype/dice-demo.html`. It is a throwaway page, not app code. The tokens are copied from `app.css` and the state handling is simulated.

- **Ready**: the `mdi:dice-d20-outline` hexagon outline with live `<text>` faces at rest on "20", on the primary button.
- **Rolling**: the whole die tumbles (`tumble`, 0.9 s, 90° hops with a 3 px lift). The face number turns with it and changes once per quarter-turn, in step with the hops: 8 faces staggered over 1.8 s with `steps(1)` (`flick8`).
- **Done**: the d20 is replaced by `mdi:dice-multiple-outline` (two dice) with a `pop` overshoot (0.35 s). At the same time the button gets the ring pulse and the preview gets the arrival glow.
- **Reroll**: two dice on the accent style. A parameter change returns to Ready, with the d20 on the primary style.
- **Resting face**: a plain "20". The seed number is moot, because the d20 only shows in Ready, before a map exists or after a parameter change.
- **Licence**: both icons are MDI (Apache-2.0), with a source comment kept next to the paths.

## Open Questions

1. **The Rolling label**: keep "Generating…" (the visual spec's `waitForLoading` at `home.visual.spec.ts:57` and the preview badge already use it), or stay "Generate" with only the tumbling d20 and `aria-busy`? Keeping "Generating…" changes no locator. Dropping it changes `waitForLoading` and keeps the button width steady.
2. **The tooltip mechanism**: native `title` or a new tooltip primitive. A primitive would be the second Radix component, against `web/AGENTS.md:37`.
3. **The flash colour**: reuse `ring` or `accent` at some alpha (no new token), or add a dedicated token with a recorded contrast check.
4. **Preview flash scope**: an opt-in prop on `MapPreview` so `/maps/:id` stays still, or a wrapper in home.
5. **Docs to confirm during planning**, via Context7, which this research did not fetch: Tailwind v4.2's `motion-safe`/`motion-reduce` variants and `@keyframes` inside `@theme` or `@utility`, and Playwright 1.63's `animations: "disabled"` semantics for finite and infinite animations.
6. **Roadmap**: S-02's Outcome line (`context/foundation/roadmap.md:106`) and its Unknowns need rewriting to the button-states outcome. That is a doc edit for the plan, not an open fact.
