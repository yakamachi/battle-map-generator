---
change_id: regenerate-with-new-seed
title: Generate button states that make regenerating a map discoverable
status: implementing
created: 2026-10-18
updated: 2026-10-19
archived_at: null
---

## Notes

S-02: regenerating a map is discoverable through the Generate button's states, without changing its label. The label stays "Generate". The button shows Ready (primary, d20 icon; also returns here when parameters change after a map exists), Rolling (d20 tumbles, aria-busy, disabled), Done (~600ms flash on both the button and the preview), Reroll (accent style, dice+↻ icon, tooltip "Roll a new layout with the same settings") and Error (short shake, then back to Ready). Not by colour alone: aria-live announces "New map generated" and prefers-reduced-motion is respected. Animations use plain @keyframes, no tw-animate-css. Update S-02's roadmap outcome to match.
