---
change_id: home-view-ui-contract
title: Home view UI contract
status: implementing
created: 2026-09-29
updated: 2026-09-30
archived_at: null
---

## Notes

`/10x-ui` change (Module 2, Lesson 5).

- **View:** `web/app/routes/home.tsx`, the only route and the one every DM uses (generate → preview → download).
- **Token source:** `web/app/app.css`. Values go in `:root` / `.dark` and are published through `@theme inline`, so classes like `bg-primary` exist. Today `@theme` only sets `--font-sans`.
- **Contract variant: no design system.** Introducing the contract is the change, under three conditions:
  1. Any new dependency (for example shadcn/ui, or its `cn`/`clsx` helpers) is marked as such, and the user decides whether to add it.
  2. The scope is one token block (primary, surface, border, muted, destructive, radius, spacing) plus the 2–3 components this view uses (Button, alert or error message, maybe the preview frame). Not a whole library.
  3. Whatever the repo already has wins: Tailwind v4 via `@tailwindcss/vite`, the dark mode in `app.css`, and the existing e2e and Playwright setup.
- **Pre-audit (2026-09-29):**
  - The hardcoded-value scan finds 4 hits in `home.tsx` (lines 101, 109, 116, 121) and 0 in `root.tsx`.
  - `app.css:11` sets `bg-white dark:bg-gray-950`.
  - There are 0 uses of role-based colour classes like `bg-primary`.
  - There is no components directory.
  - The agent rules (`AGENTS.md` and `CLAUDE.md`, root and `web/`) have no UI styling rules and nothing that invites one-off values.
- **Out of scope:** the canvas bitmap and the PNG. They stay at a fixed 140 px per cell (`web/AGENTS.md`), and this change styles only the page around the preview.
