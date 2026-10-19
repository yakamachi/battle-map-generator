# Generate Button Dice States (S-02) Implementation Plan

## Overview

Make regenerating a map discoverable through the Generate button's states, without changing its "Generate" label. The button shows:

- **Ready**: a d20 on the primary style.
- **Rolling**: the d20 tumbles while its face number changes on each hop.
- **Done**: the d20 pops into two dice, the button's ring pulses and the preview glows.
- **Reroll**: two dice on an accent style, with a tooltip.
- **Error**: a shake, then back to Ready.

Changing a parameter after a map exists returns the button to Ready. This is variant D4 from `prototype/dice-demo.html`, and it is all `web/` work: no API, contract or fixture change.

## Current State Analysis

- Regenerating already works. Generate sends `seed: null` (`web/app/routes/home.tsx:83-87` → `web/app/api/client.ts:82`), and the API draws a fresh 32-bit seed from the OS RNG (`api/Maps/MapEndpoints.cs:67`, `:94-99`). `e2e/tests/map.spec.ts:83-89` already checks that a second Generate gives a different preview.
- The button only toggles its text: `{busy ? "Generating…" : "Generate"}`, disabled while `busy` (`home.tsx:161-163`). `busy` covers both "request in flight" and "map in state but not yet drawn" (`home.tsx:69-77`).
- Every input the new states need already exists in home:
  - `status` (`home.tsx:33-37`)
  - `drawnMap`, with `parameters: { roomCount, encounter, bossSize | null }` (`web/app/map/use-map-canvas.ts:62`, `web/app/api/schema.d.ts:166-171`)
  - the form values (`home.tsx:61-63`)
- There is no icon library and no animation yet. Primitives were hand-copied without `lucide-react` or `tw-animate-css` (`web/AGENTS.md:36`, `web/app/components/ui/dialog.tsx:11`).
- `MapPreview` is shared by home and `/maps/:id` (`web/app/components/map-preview.tsx`, `web/app/routes/saved-map.tsx:105`).

## Desired End State

On the home page:

- **Before a map exists**, the button shows a d20 resting on "20" on the dark primary style.
- **After a click**, the d20 tumbles in hops, its number changing once per hop, and the button is disabled with `aria-busy`.
- **When the map is drawn**:
  - the d20 pops into two dice on the accent style
  - the button's ring pulses
  - the preview frame glows once
  - a screen reader hears "New map generated, seed N"

  Hovering the button then shows "Roll a new layout with the same settings".
- **Changing Rooms, Encounter or (on a Boss fight) Boss size** returns the button to Ready. Changing them back returns it to Reroll.
- **A failed Generate** shakes the button, which ends in Ready, and the existing error alert shows.
- **Save** never flashes, and `/maps/:id` never flashes.
- **With reduced motion requested**, nothing moves. The state still shows through the icon, the colour and the disabled state.

Verify with:

- the unit tests for the state function
- `ui:scan` and typecheck
- the visual gate with updated baselines and a new `ready-params-changed` shot
- e2e in Chrome and Firefox
- a manual pass in both themes and with reduced motion

### Key Discoveries:

- **Screenshots run with `animations: "disabled"`** (`web/visual/home.visual.spec.ts:32-36`). Playwright fast-forwards finite animations to the end and cancels infinite ones back to their initial state (Context7, `/microsoft/playwright` screenshot docs). So the Done effects must be finite CSS animations over a settled style, never a JS timer that holds a "done" state.
- **Save replaces the map in state** (`home.tsx:182`), which redraws the canvas and produces a new `drawnMap`. A flash keyed on `drawnMap` would fire after Save, so the trigger must be "a map started by Generate finished drawing".
- **When the form switches to Skirmish, it keeps its `bossSize`** (`home.tsx:142`), while a skirmish map echoes `bossSize: null`. The parameter comparison checks Boss size only when `encounter === "boss"`, mirroring `home.tsx:86`.
- **Tailwind v4 defines animations with `@keyframes` inside `@theme`** next to `--animate-*` variables, and `motion-safe:` and `motion-reduce:` are built-in variants (Context7, `/websites/tailwindcss` theme docs).
- **The accent pair has no recorded contrast.** `accent` equals `secondary` in both themes, and `accent-foreground` equals `foreground` (`context/archive/2026-09-29-home-view-ui-contract/tokens.md:17-22`). Its ratio is not in the archived contrast table (`:34-41`), so this change records it.
- **e2e and visual tests find the button by its name.** Generate is located by name in 4 e2e specs and 2 visual specs. `waitForLoading` locates "Generating…" (`home.visual.spec.ts:57`), the only locator this plan changes.

## What We're NOT Doing

- No change to the label ("Generate" in every state, including Rolling) and no "Generuj ponownie" wording.
- No API, OpenAPI, client-type or fixture change. Seed choice stays on the server.
- No seed input, and no seed-derived resting face. The d20 rests on a plain "20" because it only shows in Ready.
- No tooltip primitive. A native `title` is used, and only in Reroll.
- No new colour token. The flash uses `--ring`, and Reroll uses the existing `accent`/`accent-foreground`.
- No dice library, Lottie, WebGL or raster images. Two inline MDI SVG paths (Apache-2.0) are used.
- No flash, icon or button-state change on `/maps/:id` or in the Save and Rename dialogs.
- No change to what `busy` covers. Like today's "Generating…", the button shows Rolling during the brief redraw after Save.

## Implementation Approach

1. Compute the button's state with a pure function from state home already holds, and test it.
2. Render the icons as one small view component with inline SVG and `currentColor`.
3. Put all motion in `app.css` as `@theme` `--animate-*` and `@keyframes`, applied through `motion-safe:` classes. Reduced motion then removes the movement but keeps the state.
4. Fire the two one-shot effects (the Done flash and the Error shake) from per-Generate counters in home. The button never remounts, so keyboard focus survives a roll.
5. Land the behaviour first (phase 1), then update the visual baselines and the docs (phase 2), so the screenshot diff is reviewed once, against finished behaviour.

## Critical Implementation Details

**Timing & lifecycle**: The flash and the shake each need to replay on every Generate without remounting the `<button>`. Remounting through a changing `key` would drop keyboard focus after Enter. Two ways work:

- remount only an `aria-hidden` decoration inside the button (the ring-pulse overlay), keyed by a counter
- restart a CSS animation on the button element imperatively (remove the class, force a reflow, add it back), as in `prototype/dice-demo.html`

The icon's pop can simply follow the icon swap, since the dice icon mounts when the state becomes Reroll.

**State sequencing**: Set the "Generate pending" marker in `onGenerate` before the request. When `drawnMap` becomes non-null while it is set, clear it and bump the flash counter. When `status` becomes `error` while it is set, clear it and bump the shake counter. Save never sets the marker. That is what keeps Save from flashing.

## Phase 1: Button states, dice and flash

### Overview

Everything the user sees and hears changes in this phase. The visual gate is expected to fail until phase 2 regenerates the baselines.

### Changes Required:

#### 1. Button state function

**File**: `web/app/lib/generate-button.ts` (new), `web/app/lib/generate-button.test.ts` (new)

**Intent**: Derive which state the Generate button shows from home's existing state, so the rule is unit-tested rather than buried in JSX.

**Contract**: `generateButtonState({ busy, drawnParameters, form }) → "ready" | "rolling" | "reroll"`, where:

- `busy` wins and gives `"rolling"`
- a drawn map whose parameters match the form gives `"reroll"`
- anything else gives `"ready"`

`form` is `{ roomCount, encounter, bossSize }`. Boss size is compared only when `encounter === "boss"`. Error is not a resting state: after a failure there is no drawn map, so the function returns `"ready"`, and the shake is transient. Tests cover:

- no map
- busy, with and without a map
- matching parameters
- each field differing
- a skirmish map against a form whose hidden `bossSize` differs (still `"reroll"`)
- a boss map with a different size (`"ready"`)

#### 2. Dice icon

**File**: `web/app/components/dice-icon.tsx` (new)

**Intent**: The two icons from D4 as one view component: the `mdi:dice-d20-outline` hexagon outline with live `<text>` faces, and `mdi:dice-multiple-outline`.

**Contract**:

- Props: `variant: "d20" | "dice"` and `rolling: boolean`.
- The SVG is `aria-hidden`, uses `currentColor` only and sizes through the button's `[&_svg]` rule at `size-5`.
- The d20 has:
  - a resting "20" face, hidden while rolling, but only under `motion-safe`, so reduced motion keeps "20"
  - eight staggered faces that flicker in step with the tumble (`flick8`, 1.8 s, `steps(1)`, delays of −0.225 s × i)
- The dice variant plays the `pop` animation when it mounts.
- A source comment names MDI and its Apache-2.0 licence.
- No literal colours, no arbitrary px or rem classes (`ui:scan` views scope). SVG `font-size` and `x`/`y` attributes are SVG geometry, not Tailwind classes.

#### 3. Motion and tokens

**File**: `web/app/app.css`

**Intent**: Define the animations once, as theme animations, so views use `motion-safe:animate-*` classes and `ui:scan` stays clean.

**Contract**: These `@theme` entries, each with its `@keyframes`, timed as in the prototype:

- `--animate-tumble`: 0.9 s, 90° hops with a 3 px lift, infinite
- `--animate-flick8`: 1.8 s, `steps(1)`, infinite
- `--animate-pop`: 0.35 s overshoot
- `--animate-ring-pulse`: 0.7 s, `--ring` from about 70% to 0% alpha, spreading to 10 px
- `--animate-arrive`: 0.7 s inset `--ring` glow fading out
- `--animate-shake`: 0.4 s

Colours come only from `var(--ring)` through `color-mix`. No new colour variable, no `.dark` and no `@custom-variant`. Update the `map-preview-fit` comment only if a row is added (none is planned).

#### 4. Accent button variant

**File**: `web/app/components/ui/button.tsx`

**Intent**: Add the Reroll look as a generic primitive variant rather than view classes.

**Contract**: `variant: "accent"` uses:

- `bg-accent text-accent-foreground border-border`
- a hover border in `ring`
- a focus-visible treatment matching `outline`

Token classes only.

#### 5. Home wiring

**File**: `web/app/routes/home.tsx`

**Intent**: Render the button from `generateButtonState`, swap the icon, fire the flash and shake once per Generate, announce success, and pass the preview's flash.

**Contract**:

- The label is always "Generate", and `disabled={busy}` is unchanged. `aria-busy` is set while rolling.
- The variant is `default` in Ready and Rolling, and `accent` in Reroll.
- `title="Roll a new layout with the same settings"` is set in Reroll only.
- `DiceIcon` is `"dice"` in Reroll and `"d20"` otherwise, with `rolling` while Rolling.
- The pending-Generate marker and the flash and shake counters follow "State sequencing" above. The button element never remounts.
- The ring-pulse overlay is an `aria-hidden` span inside the button, keyed by the flash counter, with `motion-safe:animate-ring-pulse`.
- A visually hidden `role="status"` element (`sr-only`) reads "New map generated, seed N" after each flash. Including the seed means repeated announcements differ.
- `Seed: N`, the alerts and every other control are unchanged.

#### 6. Preview flash

**File**: `web/app/components/map-preview.tsx`

**Intent**: Let home make the frame glow once when a generated map arrives, without affecting `/maps/:id`.

**Contract**: Add an optional `flashKey?: number` prop. When it is above 0, render an `aria-hidden`, pointer-transparent overlay keyed by it, with `motion-safe:animate-arrive`, filling the frame (`absolute inset-0`, the frame's radius). `saved-map.tsx` does not pass the prop.

#### 7. Contrast record

**File**: `context/changes/regenerate-with-new-seed/tokens.md` (new)

**Intent**: Record the contrast of the newly used `accent-foreground`/`accent` pair, as `web/AGENTS.md:35` requires.

**Contract**: Use the method of the archived `tokens.md` (oklch → linear sRGB → relative luminance) and record:

- `accent-foreground / accent`, at least 4.5, light and dark
- `accent / background`, informational, since the Reroll button sits next to the outline Download button

Also note that the flash reuses `--ring` (`ring / background` is already recorded at 5.63 light and 7.21 dark), so no new token was added.

### Success Criteria:

#### Automated Verification:

- Type checking passes: `npm --prefix web run typecheck`
- UI contract scan passes: `npm --prefix web run ui:scan`
- Unit and rendering tests pass in Chromium and Firefox, including the new `generate-button.test.ts`: `npm --prefix web test`
- Production build succeeds: `npm --prefix web run build`

#### Manual Verification:

- In `npm run dev`, the full D4 flow looks like the prototype in light and dark themes (the tumble with the number changing per hop, then the pop into two dice with the ring pulse and the preview glow)
- Changing Rooms, Encounter or (on a Boss fight) Boss size after a roll brings back the d20, and changing them back brings back Reroll
- A failed Generate shakes the button and ends in Ready, and the alert shows (try it offline, or with a 429 from repeated clicks)
- Save does not flash the button or the preview, and `/maps/:id` does not flash
- With reduced motion on, nothing moves, and Ready, Rolling and Reroll stay distinguishable (use the OS setting or DevTools emulation)
- Keyboard focus stays on Generate through the roll, and the screen reader announces "New map generated, seed N"

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Phase blocks use plain bullets. The corresponding `- [ ]` checkboxes live in the `## Progress` section at the bottom of the plan.

---

## Phase 2: Gates and docs

### Overview

Bring the screenshot gate and the docs in line with the new behaviour, and confirm the deployed shape still passes e2e.

### Changes Required:

#### 1. Visual spec

**File**: `web/visual/home.visual.spec.ts`

**Intent**: Locate the busy state without the old "Generating…" button name, and pin the new Ready-after-a-parameter-change state.

**Contract**:

- `waitForLoading` waits for the Generate button (`exact: true`) to be disabled.
- A new test, `ready-params-changed`, generates seed 42, waits for ready, changes Rooms and takes a shot. It runs in all three projects.

The other tests keep their names. The `loading-*` shots now show the d20 at rest with no face, because the infinite animations are cancelled to their first frame. The `ready` and `ready-boss` shots show Reroll.

#### 2. Baselines

**File**: `web/visual/__screenshots__/{desktop-light,desktop-dark,mobile}/`

**Intent**: Regenerate the changed home baselines and add the new ones, only through the Docker image.

**Contract**: Run `npm --prefix web run visual:update`. Expect diffs only in the home view's button row and in the new shot. Auth and library baselines should be unchanged, apart from any shot that shows the home button (`library.visual.spec.ts:89` clicks Generate before its Save shot). The PR explains the visual change.

#### 3. Web docs

**File**: `web/AGENTS.md`

**Intent**: Record the new UI rules so later changes keep them.

**Contract**: Under "## UI", add:

- motion lives in `app/app.css` as `@theme` `--animate-*` with `@keyframes`, is applied through `motion-safe:`, and never uses `tw-animate-css`
- `components/dice-icon.tsx` holds the MDI paths (Apache-2.0)
- one-shot effects replay through counters without remounting the button
- the Generate flash fires only after a Generate, never after Save
- `MapPreview`'s `flashKey` is home-only

Under "## Tests", note the `waitForLoading` locator change.

#### 4. Roadmap

**File**: `context/foundation/roadmap.md`

**Intent**: Replace S-02's old "generuj ponownie" outcome with what was built.

**Contract**: In S-02's Outcome line and its At-a-glance row, write (in Polish, like the rest of the file) the outcome: the button shows readiness, rolling and the readiness for a new layout through the dice states, without changing its label, and regenerating gives a new layout. Rewrite the Unknowns as resolved. Status stays under the lifecycle skills' control.

### Success Criteria:

#### Automated Verification:

- Visual gate passes with updated and new baselines in the CI image: `npm --prefix web run visual:docker`
- e2e passes in Chrome and Firefox against the built app: `npm --prefix e2e run build:app && npm --prefix e2e test`
- UI contract scan still passes: `npm --prefix web run ui:scan`

#### Manual Verification:

- The baseline diff touches only the Generate button plus the new `ready-params-changed` shots, and each delta is explained in the PR description
- The `web/AGENTS.md` and roadmap S-02 wording reads correctly and matches the shipped behaviour

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human before opening the pull request.

---

## Testing Strategy

### Unit Tests:

- `generate-button.test.ts` covers every branch of `generateButtonState`, including the hidden Boss-size case on a skirmish map and the busy-over-everything rule.

### Integration Tests:

- **Visual gate**: Ready (`idle`), Rolling (`loading-first`, `loading-regenerate`), Reroll (`ready`, `ready-boss`), Ready after a parameter change (`ready-params-changed`, new) and the error state (`error-rate-limited`), in three projects.
- **e2e**: `map.spec.ts` keeps proving that a second Generate gives a different map and download. All button locators keep working because the label stays "Generate".

### Manual Testing Steps:

1. Generate, watch the roll, the pop into two dice, the ring pulse and the preview glow. Hover to see the tooltip.
2. Change Rooms and check that the d20 comes back. Change it back and check that the two dice come back.
3. Save the map and check that nothing flashes. Open it in My maps and check that nothing flashes.
4. Turn on reduced motion and repeat step 1: no motion, and the states are still visible.
5. Switch the OS to dark and repeat step 1.

## Performance Considerations

There are only CSS transform and opacity animations on a 20 px SVG and one overlay. They run only while Rolling or for under a second after Done or Error, and add no JavaScript timers. Generate's request count is unchanged.

## Migration Notes

Not applicable: no data, API or stored-map change.

## Addendum (implementation, 2026-10-19)

These changes were agreed during phase 1 manual testing and its review (`reviews/impl-review-phase-1.md`). They override the phase text above where the two disagree.

- **Focus-safe busy state**: Generate uses `aria-disabled` while busy, with no `onClick`, instead of `disabled={busy}`. Chromium drops keyboard focus from a button that becomes disabled. The button primitive's base string styles `aria-disabled` the same way as `disabled` (`pointer-events-none`, `opacity-50`), so the busy button is dimmed in every motion setting (review F1).
- **Minimum roll**: `onGenerate` waits for whichever is slower, the response or a 0.9 s timer (one tumble). Under `prefers-reduced-motion` there is no wait. This supersedes "adds no JavaScript timers" in Performance Considerations: there is one bounded 900 ms timer per Generate.
- **Pop only after a Generate**: `DiceIcon` takes a `pop` prop. Home sets it in `onGenerate` and clears it on Save and on any form change. The dice that come back after those appear without the pop (review F2).
- **Busy announcement**: while a Generate is loading, the `role="status"` region reads "Generating a map…". It then reads "New map generated, seed N" (review F4).
- **Phase 2 locator**: `waitForLoading` can keep `toBeDisabled()` on `getByRole("button", { name: "Generate", exact: true })`, because Playwright treats `aria-disabled="true"` on a button as disabled. The `loading-*` shots now show the dimmed d20 at rest with no face. Each Generate takes at least 0.9 s, which is well inside the specs' 20 s render timeout.

## References

- Research: `context/changes/regenerate-with-new-seed/research.md` (including the 2026-10-19 dice follow-up and the D4 decision)
- Prototype: `context/changes/regenerate-with-new-seed/prototype/dice-demo.html` (variant D4: keyframes, timings, SVG paths)
- Token method and the existing ratios: `context/archive/2026-09-29-home-view-ui-contract/tokens.md`
- Button today: `web/app/routes/home.tsx:161-163`; busy derivation: `web/app/routes/home.tsx:69-77`
- Screenshot options: `web/visual/home.visual.spec.ts:32-36`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Button states, dice and flash

#### Automated

- [x] 1.1 Type checking passes: `npm --prefix web run typecheck` — 09a1096
- [x] 1.2 UI contract scan passes: `npm --prefix web run ui:scan` — 09a1096
- [x] 1.3 Unit and rendering tests pass in Chromium and Firefox, including the new `generate-button.test.ts`: `npm --prefix web test` — 09a1096
- [x] 1.4 Production build succeeds: `npm --prefix web run build` — 09a1096

#### Manual

- [x] 1.5 In `npm run dev`, the full D4 flow looks like the prototype in light and dark themes — 09a1096
- [x] 1.6 Changing Rooms, Encounter or (on a Boss fight) Boss size after a roll brings back the d20, and changing them back brings back Reroll — 09a1096
- [x] 1.7 A failed Generate shakes the button and ends in Ready, and the alert shows — 09a1096
- [x] 1.8 Save does not flash the button or the preview, and `/maps/:id` does not flash — 09a1096
- [x] 1.9 With reduced motion on, nothing moves, and Ready, Rolling and Reroll stay distinguishable — 09a1096
- [x] 1.10 Keyboard focus stays on Generate through the roll, and the screen reader announces "New map generated, seed N" — 09a1096

### Phase 2: Gates and docs

#### Automated

- [ ] 2.1 Visual gate passes with updated and new baselines in the CI image: `npm --prefix web run visual:docker`
- [ ] 2.2 e2e passes in Chrome and Firefox against the built app: `npm --prefix e2e run build:app && npm --prefix e2e test`
- [ ] 2.3 UI contract scan still passes: `npm --prefix web run ui:scan`

#### Manual

- [ ] 2.4 The baseline diff touches only the Generate button plus the new `ready-params-changed` shots, and each delta is explained in the PR description
- [ ] 2.5 The `web/AGENTS.md` and roadmap S-02 wording reads correctly and matches the shipped behaviour
