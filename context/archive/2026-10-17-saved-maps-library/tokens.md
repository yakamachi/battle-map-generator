# Tokens added for the saved-map library (Phase 3)

Adds to the parchment & ink tokens in `context/archive/2026-09-29-home-view-ui-contract/tokens.md`, for the Dialog primitive (`web/app/components/ui/dialog.tsx`) and the `destructive` Button variant. Values live in `web/app/app.css` (`:root`, the `prefers-color-scheme: dark` block, and `@theme inline` as `--color-popover`, `--color-popover-foreground`, `--color-overlay`).

## Values (oklch)

| Token | Light | Dark | Notes |
|---|---|---|---|
| popover | `oklch(0.99 0.008 85)` | `oklch(0.24 0.012 60)` | = card |
| popover-foreground | `oklch(0.22 0.02 60)` | `oklch(0.93 0.02 85)` | = card-foreground |
| overlay | `oklch(0.22 0.02 60 / 40%)` | `oklch(0 0 0 / 60%)` | ink wash behind a dialog |

No existing value changed.

## Contrast (WCAG 2 ratio)

Same method as the original record: oklch → linear sRGB (clamped) → relative luminance. Translucent colours are composited in sRGB over the surface beneath them (the dialog footer's `bg-muted/50` over `popover`, the destructive tint over `popover`, the overlay over `background`).

| Pair | Required | Light | Dark |
|---|---|---|---|
| popover-foreground / popover | ≥ 4.5 | 16.87 | 13.41 |
| muted-foreground / popover (dialog description) | ≥ 4.5 | 6.39 | 6.63 |
| foreground / footer (`muted/50` over popover) | ≥ 4.5 | 15.46 | 12.66 |
| muted-foreground / footer | ≥ 4.5 | 5.86 | 6.26 |
| destructive / `destructive/10` over popover (Delete button) | ≥ 4.5 | 5.42 | 4.67 |
| popover / background under the overlay (dialog edge) | informational | 2.62 | 1.21 |

The dialog edge in dark mode relies on its `ring-foreground/10` outline as well as the luminance step.

## Destructive button variant

`border-destructive/30 bg-destructive/10 text-destructive`, with `hover:border-destructive`. A deeper tint on hover was rejected: `destructive/20` over popover gives 3.97 in dark mode (`/15`: 4.31), under 4.5, so the hover darkens the border instead and the text ratio stays at the values above. A solid `destructive` fill fails one theme either way (light text on it: 5.99 light / 2.82 dark; dark text: 2.66 light / 5.65 dark).
