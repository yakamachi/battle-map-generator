<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Home View UI Contract

- **Plan**: context/changes/home-view-ui-contract/plan.md
- **Scope**: Phase 3 of 4 (commit 32d84c6)
- **Reviewed phases**: 3
- **Date**: 2026-09-29
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 3 observations

## Summary

**Plan drift: none.**
- `MapPreview` matches the plan: an always-mounted single canvas, sized by CSS only through `map-preview-fit` and `object-contain`, with the exact empty-state copy, the busy loading overlay and the dimmed previous drawing.
- `canDownload` is unchanged.
- Extras: `label` prop, `flex-wrap`, `transition-opacity`, the overlay pill. All harmless.
- Only the three allowed new strings were added.

**Every state change was traced:**
- idle
- generate, draw
- regenerate
- HTTP error with a previous drawing
- render error
- download error
- Generate clicked before the draw finishes

No path makes a stale drawing look downloadable or shows a seed that doesn't match the pixels. The effect's cancellation covers the races.

**`display: none` canvas.** It still draws, returns `getImageData` and `toBlob` in both browsers.

**Built CSS.** `map-preview-fit`, `hidden`, `size-full` and `object-contain` are all emitted. `100dvh` is supported in the target browsers. No overflow at 390 px.

**Automated criteria.** All passed on this tree:
- typecheck and build
- the hardcoded-value scan (full on the view files, colour literals only on `ui/`)
- e2e 2/2 (Chromium and Firefox)
- `npm test` 36/36

**Manual checks 3.5–3.8.** Confirmed by the user. The dimming in 3.5 happens too fast to see in a real run; the Phase 4 `loading-regenerate` screenshot pins that state.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — The Generate button and the preview disagree while the atlas loads

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/home.tsx (the Generate button's label and `disabled`, next to `previewState`)
- **Detail**:
  - This window occurs once the map response has arrived but the tile atlas hasn't loaded (the first map), or during the draw.
  - Status is `ready`, so the frame shows "Generating…" with `aria-busy`, but the button reads "Generate" and is enabled.
  - Clicking it cancels a map that was already generated, which also costs one of the 10 generate calls allowed per minute.
- **Fix**: Derive `busy = previewState === "loading"` and use it for both the button's label ("Generating…") and its `disabled`.
- **Decision**: FIXED — `busy = previewState === "loading"` drives the Generate label and `disabled`

### F2 — The canvas `aria-label` has no role

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/components/map-preview.tsx (canvas)
- **Detail**: This dates from S-01. A `<canvas>` has no implicit role, and ARIA 1.2 prohibits naming a generic element, so screen readers may ignore "Battle map, seed N" and axe may flag it.
- **Fix**: Add `role="img"` to the canvas. `locator("canvas")` in the e2e is unaffected.
- **Decision**: FIXED — `role="img"` on the canvas

### F3 — The 8.5rem header allowance is only documented in app.css

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/app.css (`@utility map-preview-fit`), web/app/routes/home.tsx (layout above the preview)
- **Detail**:
  - The utility subtracts 8.5rem for the padding, heading, button row and gaps above the preview. It adds up to 8rem, leaving 0.5rem of slack.
  - The number lives in `app.css` and the layout it measures lives in `home.tsx`. Adding a row above the frame (S-03's parameter form) makes the map scroll again, and nothing points the editor at the utility.
- **Fix**: Add a one-line comment in `home.tsx` above the header markup: if you add rows above the preview, update `map-preview-fit` in `app.css`.
- **Decision**: FIXED — comment above `<main>` in home.tsx pointing at `map-preview-fit`

### F4 — The empty-state copy repeats the tile size and the default map size

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/components/map-preview.tsx (defaults 30×20, copy "30×20 … (140 px per square)")
- **Detail**: `TILE_SIZE` lives in `app/map/tileset.ts`, and the map size comes from the API. If either changes (S-03 adds S/M/L sizes), the copy goes stale without anyone noticing.
- **Fix**: Build "140 px per square" from `TILE_SIZE`, and phrase the size from the `width`/`height` props: the default 30×20 now, the chosen size later.
- **Decision**: FIXED — copy built from `width`×`height` props and `TILE_SIZE` (text unchanged today)
