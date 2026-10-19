<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Generate Button Dice States (S-02)

- **Plan**: context/changes/regenerate-with-new-seed/plan.md
- **Scope**: Phase 1 of 2
- **Reviewed phases**: 1
- **Date**: 2026-10-19
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 1 warning, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | WARNING |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

Evidence: commit 09a1096. All 7 planned changes are present and match the plan. Two agreed deviations are recorded in the commit message: `aria-disabled` instead of `disabled`, and a 0.9 s minimum roll. One justified drift: `arrive` uses `backdrop-filter`, because the glow is an overlay. Save cannot trigger the flash: only `onGenerate` sets the pending marker (`web/app/routes/home.tsx:129`, effect at `:107-117`). Typecheck, `ui:scan`, 108/108 tests in Chromium and Firefox and the build all pass. Nothing in e2e breaks. In the visual specs, only `home.visual.spec.ts:57` breaks, and phase 2 already plans that change.

## Findings

### F1 — The busy Generate button is no longer dimmed, so under reduced motion Rolling looks like Ready

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality (accessibility), Pattern Consistency, Success Criteria
- **Location**: web/app/routes/home.tsx:222; web/app/components/ui/button.tsx:8
- **Detail**: The base button dims its busy state with `disabled:opacity-50`. Moving to `aria-disabled` dropped that dimming, and the view adds only `aria-disabled:pointer-events-none`. The disabled styling now also lives in the view rather than in the primitive. Under reduced motion, a busy button is identical to Ready: the same variant and the same still d20 on "20". That contradicts the plan's "the state still shows through the icon, the colour and the disabled state", and manual check 1.9, which is ticked. With motion on, the tumble still signals the state. The `loading-*` baselines would also show an undimmed button.
- **Fix A ⭐ Recommended**: Add `aria-disabled:pointer-events-none aria-disabled:opacity-50` to the primitive's base string, next to the `disabled:` pair, and drop the class from home.
  - Strength: The same treatment as every disabled control, and today's busy button was dimmed too. Any future `aria-disabled` button gets it for free.
  - Tradeoff: The tumble runs at 50% opacity, a little less punchy than prototype D4.
  - Confidence: HIGH. It is a one-line change in the base string that mirrors the existing `disabled:` pair.
  - Blind spot: No other button uses `aria-disabled` today, so there is no other affected button to check.
- **Fix B**: Put `aria-disabled:pointer-events-none` in the primitive, and dim only under reduced motion in home (`motion-reduce:aria-disabled:opacity-50`).
  - Strength: It keeps the full-colour roll that was approved in the prototype.
  - Tradeoff: The rule is split across files, and the busy look differs depending on the motion setting.
  - Confidence: MED. Tailwind v4 stacks variants, but the result needs a visual check.
  - Blind spot: How the stacked variants render in Firefox has not been verified.
- **Decision**: FIXED (Fix A): `aria-disabled:pointer-events-none aria-disabled:opacity-50` moved into the button primitive's base string; the view keeps only `relative`

### F2 — The dice pop also plays after Save's redraw and when the form is changed back

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: web/app/components/dice-icon.tsx:39-42
- **Detail**: The dice icon remounts each time the button enters Reroll, so `pop` plays then. After a Save, the button briefly rolls during the redraw and then pops, though with no ring pulse, glow or announcement. Changing the form back to the drawn map's settings also pops. The plan allowed the pop to follow the icon swap, but "What We're NOT Doing" says "no icon … change in the Save … dialogs".
- **Fix**: Play `pop` only on the swap that follows a Generate's flash, by passing a pop flag from home.
- **Decision**: FIXED: `DiceIcon` takes a `pop` prop. Home sets `popDice` in `onGenerate` and clears it on Save and on any form change, so the dice pop only after a Generate

### F3 — The plan text is out of date on the two agreed deviations

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Scope Discipline
- **Location**: context/changes/regenerate-with-new-seed/plan.md (phase 1 §5 "`disabled={busy}` is unchanged"; Performance "adds no JavaScript timers")
- **Detail**: The `aria-disabled` swap and the 0.9 s minimum roll are recorded only in commit 09a1096. The plan still states the opposite, and later reviews treat the plan as ground truth.
- **Fix**: Add an addendum to the plan in phase 2's docs pass, covering `aria-disabled`, the minimum roll, and the phase 2 locator.
- **Decision**: FIXED: added a plan.md "Addendum (implementation, 2026-10-19)" covering aria-disabled, the minimum roll, pop only after a Generate, the busy announcement and the phase 2 locator

### F4 — Screen readers no longer hear that a roll started

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (accessibility)
- **Location**: web/app/routes/home.tsx:280-282
- **Detail**: Before this change, the focused button's name changed to "Generating…". Now only `aria-busy` and `aria-disabled` change, and most screen readers don't re-announce those on a focused element. Completion is still announced.
- **Fix**: While rolling, put "Generating a map…" in the existing `role="status"` region.
- **Decision**: FIXED: the role="status" region reads "Generating a map…" while a Generate is loading
