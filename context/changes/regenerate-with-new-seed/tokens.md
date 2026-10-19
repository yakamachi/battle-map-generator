# Tokens used by the Generate button states (S-02)

Adds to the parchment & ink record in `context/archive/2026-09-29-home-view-ui-contract/tokens.md`. This change adds **no new token and changes no value** in `web/app/app.css`. It starts using an existing pair, `accent-foreground` on `accent`, for the new `accent` Button variant (`web/app/components/ui/button.tsx`, home's Reroll state), so that pair's contrast is recorded here as `web/AGENTS.md` requires.

## Values used (oklch, unchanged)

| Token | Light | Dark | Notes |
|---|---|---|---|
| accent | `oklch(0.92 0.025 80)` | `oklch(0.3 0.015 60)` | = secondary |
| accent-foreground | `oklch(0.22 0.02 60)` | `oklch(0.93 0.02 85)` | = foreground |
| ring | `oklch(0.5 0.08 60)` | `oklch(0.72 0.08 70)` | Reroll hover border; the Done flash |

## Contrast (WCAG 2 ratio)

Same method as the original record: oklch → linear sRGB (clamped to gamut) → relative luminance → `(L1 + 0.05) / (L2 + 0.05)`. The script reproduces the archived ratios exactly (foreground/background 15.92 / 14.75, primary-foreground/primary 14.72 / 12.88, ring/background 5.63 / 7.21) before computing the new pairs.

| Pair | Required | Light | Dark |
|---|---|---|---|
| accent-foreground / accent (Reroll label and icon) | ≥ 4.5 | 13.69 | 11.13 |
| accent / background (Reroll button next to the outline Download button) | informational | 1.16 | 1.33 |
| ring / accent (Reroll hover border) | informational (≥ 3 for a UI edge) | 4.84 | 5.44 |

The Reroll fill sits close to the page background in both themes, as the outline buttons beside it do; its edge is the `border` token (border/background 1.45 light, 1.41 dark in the original record), and the state also shows through the two-dice icon, so it is not told apart by colour alone.

## The flash reuses `ring`

The Done ring pulse on the button and the arrival glow on the preview (`--animate-ring-pulse`, `--animate-arrive` in `app.css`) take their colour only from `var(--ring)` through `color-mix` (fading from 70% and 80% alpha to 0%). `ring / background` is already recorded at 5.63 light and 7.21 dark, so no new colour token was added and no further check is needed. The error shake moves the button and adds no colour.
