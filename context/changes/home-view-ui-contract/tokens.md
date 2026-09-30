# Design tokens: parchment & ink

Source: parchment & ink motif, chosen in /10x-plan 2026-09-29.

The tokens live in `web/app/app.css`: light values in `:root`, dark values in `@media (prefers-color-scheme: dark) { :root { … } }`, published to Tailwind through `@theme inline` as `--color-<name>` and `--radius-sm/md/lg/xl`. Dark mode follows the OS; there is no `.dark` class. Change a value in `app.css` and here in the same commit, and recompute the ratios.

## Values (oklch)

| Token | Light | Dark |
|---|---|---|
| background | `oklch(0.97 0.018 85)` | `oklch(0.2 0.01 60)` |
| foreground | `oklch(0.22 0.02 60)` | `oklch(0.93 0.02 85)` |
| card | `oklch(0.99 0.008 85)` | `oklch(0.24 0.012 60)` |
| card-foreground | = foreground | = foreground |
| primary | `oklch(0.25 0.02 60)` | `oklch(0.9 0.03 85)` |
| primary-foreground | `oklch(0.97 0.018 85)` | `oklch(0.22 0.02 60)` |
| secondary | `oklch(0.92 0.025 80)` | `oklch(0.3 0.015 60)` |
| secondary-foreground | = foreground | = foreground |
| muted | `oklch(0.93 0.02 80)` | `oklch(0.28 0.012 60)` |
| muted-foreground | `oklch(0.48 0.03 65)` | `oklch(0.72 0.03 75)` |
| accent | `oklch(0.92 0.025 80)` | `oklch(0.3 0.015 60)` |
| accent-foreground | = foreground | = foreground |
| destructive | `oklch(0.5 0.17 30)` | `oklch(0.68 0.15 30)` |
| border | `oklch(0.85 0.03 75)` | `oklch(1 0 0 / 12%)` |
| input | `oklch(0.85 0.03 75)` | `oklch(1 0 0 / 16%)` |
| ring | `oklch(0.5 0.08 60)` | `oklch(0.72 0.08 70)` |
| radius | `0.5rem` (sm ×0.6, md ×0.8, lg ×1, xl ×1.4) | same |

All colours are inside the sRGB gamut, so browsers render them without clipping.

## Contrast (WCAG 2 ratio)

Computed as oklch → linear sRGB (clamped to gamut) → relative luminance → `(L1 + 0.05) / (L2 + 0.05)`. Translucent dark `border`/`input` are composited over `background` first.

| Pair | Required | Light | Dark |
|---|---|---|---|
| foreground / background | ≥ 4.5 | 15.92 | 14.75 |
| primary-foreground / primary | ≥ 4.5 | 14.72 | 12.88 |
| muted-foreground / background | ≥ 4.5 | 6.03 | 7.29 |
| destructive / background | ≥ 4.5 | 5.99 | 5.90 |
| ring / background | ≥ 3 | 5.63 | 7.21 |
| border / background | informational | 1.45 | 1.41 |

Supporting pairs (informational): foreground/card 16.87 light, 13.41 dark; muted-foreground/muted 5.35 light, 5.88 dark; destructive/card 6.35 light, 5.36 dark; input/background 1.45 light, 1.62 dark.

The focus outline in `@layer base` uses `ring` at 50% opacity (`outline-ring/50`); the 3:1 check above is for the solid `ring` token that components use for their focus border.
