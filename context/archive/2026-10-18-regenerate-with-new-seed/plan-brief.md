# Generate Button Dice States (S-02): Plan Brief

> Full plan: `context/changes/regenerate-with-new-seed/plan.md`
> Research: `context/changes/regenerate-with-new-seed/research.md`
> Prototype: `context/changes/regenerate-with-new-seed/prototype/dice-demo.html` (variant D4)

## What & Why

S-02 (FR-005) asks that a DM can regenerate a map and get a new layout with the same parameters. That already works. What's missing is that the UI never says so. Instead of relabelling the button, the Generate button now shows its states with dice: ready, rolling, done, and "click again for a new layout".

## Starting Point

Generate sends no seed, so the API picks a fresh one on each click (`api/Maps/MapEndpoints.cs:67`), and e2e already checks that two Generates differ. The button only switches its text between "Generate" and "Generating…" (`web/app/routes/home.tsx:161-163`). There are no icons and no animations in the app yet.

## Desired End State

On home, the button shows:

- a d20 on the dark primary style before a map exists
- the d20 tumbling, its number changing on each hop, while generating
- when the map is drawn, the d20 popping into two dice on a softer accent style, with a ring pulse on the button and a glow on the preview; "Roll a new layout with the same settings" on hover

Changing a parameter brings the d20 back. A failure shakes the button. Save and `/maps/:id` never flash. Under reduced motion nothing moves, but every state stays visible.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Label | Always "Generate", including while rolling | The dice carry the message, and the button width stays steady | Plan |
| Animation | D4: tumble + flicker in step, then a pop into two dice | Chosen by the user from four prototyped variants | Research |
| Icons | Inline MDI `dice-d20-outline` (with live faces) and `dice-multiple-outline` | Apache-2.0 needs no visible credit, and no icon library is added | Research |
| Motion tech | `@theme` `--animate-*` with `@keyframes`, `motion-safe:` | No dependency, it's Tailwind v4's documented pattern, and reduced motion keeps the states | Research + docs |
| Flash trigger | Only after a Generate finishes drawing | Save also replaces the drawn map and must not flash | Research |
| Tooltip | Native `title`, Reroll only | No new primitive, and Radix stays dialog-only | Plan |
| Flash colour | Existing `--ring` | No new token or contrast work for the flash | Plan |
| Reroll style | New `accent` button variant on existing tokens | Generic primitive. The accent pair's contrast is recorded in this change | Plan |
| Resting face | Plain "20" | The d20 only shows in Ready, so a seed number would be meaningless | Research |

## Scope

**In scope:**

- a button state function with unit tests
- a `DiceIcon` component
- the `accent` variant
- keyframes in `app.css`
- home wiring: the flash, the shake, a `role="status"` announcement and the tooltip
- the `MapPreview` `flashKey`
- a contrast record
- a visual spec tweak, a new `ready-params-changed` shot and baselines
- `web/AGENTS.md` notes and the S-02 roadmap wording

**Out of scope:**

- any API, contract or fixture change
- a seed input or seed-derived faces
- a tooltip primitive or new colour tokens
- dice or animation libraries
- changes to `/maps/:id` or the dialogs
- changing what "busy" covers

## Architecture / Approach

`generateButtonState({ busy, drawnParameters, form })` maps home's existing state to `ready | rolling | reroll`, with Boss size compared only on a Boss fight. The view picks the button variant, the icon and the tooltip from it. One-shot effects replay from per-Generate counters: a pending marker is set in `onGenerate` and resolved by the first drawn map (flash) or error (shake). That way Save can't trigger them, and the button never remounts, so focus survives. All motion is finite or infinite CSS animation. Playwright's screenshot mode therefore lands on settled frames.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Button states, dice and flash | The full D4 behaviour, accessibility and reduced motion, with unit tests | Retriggering animations without remounting the button (focus loss) |
| 2. Gates and docs | Updated and new baselines, e2e green, AGENTS and roadmap wording | Baseline churn beyond the button row needs explaining in the PR |

**Prerequisites:** branch `regenerate-with-new-seed` from `main`. Docker is needed for `visual:update` and `visual:docker`, and the e2e stack (`npm --prefix e2e run build:app`) for e2e.
**Estimated effort:** about one session, across 2 phases.

## Open Risks & Assumptions

- The visual gate is red on the branch between phase 1 and phase 2. That's acceptable, because gates guard the merge, not commits.
- Mid-roll screenshots show the d20 without a face (infinite animations are reset to frame 0). That's deterministic, but it differs from what a user sees.
- The `accent` button sits close in tone to the outline Download button. The icon swap carries the distinction, and the contrast is recorded in phase 1.

## Success Criteria (Summary)

- After a Generate, a DM can see at a glance that the next click rolls a new layout with the same settings, and that changing a setting turns it back into a fresh roll.
- The effects never fire after Save or on a saved map, and reduced-motion users get the same information without movement.
- Every existing e2e and visual locator keeps working, and the new screenshots pin the states.
