---
change_id: testing-invariant-boss-sweep
title: Invariant and boss-arena sweep
status: implemented
created: 2026-10-05
updated: 2026-10-05
archived_at: null
---

## Notes

Rollout Phase 1 of context/foundation/test-plan.md ("Invariant and boss-arena sweep").
Risks covered: #1 (invalid grid for an untested combination), #6 (boss arena indistinguishable or below floor).
Test types planned: unit (parametrized sweep over seed × room count × encounter × boss size).
Risk response intent: every valid combination yields a grid that is grid-aligned, has every room reachable, stays inside bounds, has no half-cells, and gives the boss arena a size above every other room and at or above its floor (oracle: PRD invariants, not current output).
