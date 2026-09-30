<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Home View UI Contract

- **Plan**: context/changes/home-view-ui-contract/plan.md
- **Scope**: Phase 2 of 4 (commit 6efae5e)
- **Reviewed phases**: 2
- **Date**: 2026-09-29
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 2 observations

## Summary

**Plan drift.** There is no drift and nothing is missing.
- **Button:** a native `<button>` with `type` defaulting to `"button"`, `default` and `outline` variants only, and one size. It has the ring focus style, token-based hover and `disabled` state, and imports `cn` from `"cn"`.
  - Every deviation from the upstream `radix-nova` source was either planned (no `asChild` or Slot, fewer variants and sizes, `rounded-md`) or harmless (no icon padding, no `aria-expanded` styling).
- **Alert:** `default` and `destructive` variants, `role="alert"`, token classes only.
- **`home.tsx`:** only the imports and the JSX changed. State, effects, handlers and the canvas are untouched.
- **E2E:** the locators at `map.spec.ts:38,41-42,44-57,59` still match.
- **Carry-overs in this commit:** the Phase 1 review, the SHA suffixes on rows 1.x, the ticks on rows 2.x, and the Phase 4 amendment for F1.

**Compiled CSS.** Every class produced CSS.
- `dark:` utilities compile to `@media (prefers-color-scheme: dark)`.
- Alpha tokens compile to `color-mix`, with a fallback.

**Accessibility.**
- Both buttons have accessible names.
- The two `role="alert"` regions can't be shown at the same time.
- A retry unmounts the alert and mounts it again, so the message is announced again.
- The destructive Alert's contrast is about 5.2:1 in both themes.

**Automated criteria.** All passed on this tree:
- typecheck and build
- the colour-literal scan (0 hits)
- no `dark:` classes or arbitrary values in `routes/`
- e2e 2/2 in Chromium and Firefox

The manual checks 2.5 and 2.6 were confirmed by the user.

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

### F1 — `outline-none` hides keyboard focus in Windows High Contrast mode

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/components/ui/button.tsx:8
- **Detail**:
  - In Tailwind v4, `outline-none` compiles to `outline-style: none`. That also removes the outline from the base-layer `*` rule, so focus is shown only by the `box-shadow` ring and `focus-visible:border-ring`.
  - Windows forced-colors (High Contrast) mode drops box-shadows and forces border colours to system colours, so a focused button looks the same as an unfocused one.
  - In v4, `outline-hidden` is a transparent 2px outline that forced-colors mode makes visible.
- **Fix**: Replace `outline-none` with `outline-hidden` in the Button base classes.
- **Decision**: FIXED — `outline-none` → `outline-hidden` (button.tsx:8); built CSS shows the forced-colors 2px outline

### F2 — Button radius differs from what `shadcn add` will produce

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/components/ui/button.tsx:8
- **Detail**:
  - Upstream radix-nova primitives use `rounded-lg`, which maps to `--radius` (0.5rem).
  - The plan chose `rounded-md` for Button, which is 0.4rem.
  - So the Input and Select that S-03 and S-04 add through `shadcn add` will be visibly rounder than the buttons next to them.
- **Fix**: Use `rounded-lg` in Button (and in the Alert's `rounded-lg`, already there), so every primitive uses `--radius` like upstream. The radius itself is then tuned in one place, the `--radius` token.
- **Decision**: FIXED — Button `rounded-md` → `rounded-lg` (uses `--radius`, same as upstream primitives)

### F3 — Alert's `default` variant announces assertively

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/components/ui/alert.tsx:27
- **Detail**:
  - Both variants render `role="alert"`, which is assertive and interrupts screen readers.
  - That's right for errors. The `default` variant is meant for notices, which should be polite (`role="status"`).
  - It's unused today, so there's no user impact yet. It would bite the first notice added in S-02 to S-04.
- **Fix**: Default the role by variant: `destructive` gets `alert`, `default` gets `status`. A caller can still override it through props.
- **Decision**: FIXED — role by variant: destructive → `alert`, default → `status`; props still override
