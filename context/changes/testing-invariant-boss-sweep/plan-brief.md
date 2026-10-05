# Invariant and boss-arena sweep — Plan Brief

> Full plan: `context/changes/testing-invariant-boss-sweep/plan.md`
> Research: `context/changes/testing-invariant-boss-sweep/research.md`

## What & Why

The map generator must never hand a DM an invalid grid, and a boss fight must always get an arena larger than the other rooms and at or above its floor (PRD guardrail and business rules). Risks #1 and #6 in the test plan cover this. The change makes the existing invariant check cover more maps.

## Starting Point

The parametrized sweep already exists in `api.Tests/MapGeneratorTests.cs`. It runs all 44 combinations (room counts 2–12 × skirmish, Large, Huge, Gargantuan) over 200 seeds and asserts the PRD invariants, not fixed output. It passes today (80 generator and fixture tests in 7 s).

## Desired End State

The same sweep runs over 1000 seeds per combination and passes. The determinism and different-seeds tests keep 200 seeds. The test plan's cookbook (§6.1) describes how to add an invariant test for the generator.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Arena size rule | Larger in area (current) | Matches the generator's own cap and the existing assertion; no algorithm change | Plan (user answer) |
| Sweep seed count | 1000 per combination | Covers five times the maps at a small added run time | Plan (user answer) |
| Where the sweep lives | Extend the existing theory | One place for invariants; avoids duplicate coverage | Research |
| Half-cell check | No separate assertion | The integer grid cannot represent a half-cell; bounds and length checks cover the rest | Research |
| Determinism seed count | Stays at 200 | Phase 2 owns determinism; no change to its run time here | Plan |

## Scope

**In scope:** `SweepSeedCount = 1000` in the existing sweep; the §6.1 cookbook entry in `test-plan.md`.

**Out of scope:** any generator or fixture change; both-dimension arena rule; new test files; CI workflow changes; determinism and rate-limit tests.

## Architecture / Approach

One constant is added next to `SeedCount`, and the sweep's loop uses it. Nothing else in the test or the generator changes. If a seed in 201–1000 fails, the change stops and a follow-up is opened.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Extend the invariant sweep to 1000 seeds | The sweep over 5× seeds passes; §6.1 filled | A seed in 201–1000 breaks an invariant, which would stop the change |

**Prerequisites:** the current `api.Tests` suite is green (it is, per research).
**Estimated effort:** one short session, one phase.

## Open Risks & Assumptions

- The sweep's run time at 1000 seeds is unmeasured; the research estimated it from the 7 s total. CI cost is decided at review.
- The area-only arena rule can let a room exceed the arena in one dimension (example: 3×14 next to an 8×8 arena). Accepted by decision; recorded as a manual check.
- Integration tests in `api.Tests` need Docker; if it is unavailable, the full-suite criterion is reported as not run.

## Success Criteria (Summary)

- The sweep passes on all 44 combinations over 1000 seeds, with the existing invariants and arena rule.
- No generator or fixture change is needed; the fixture tests are unchanged and green.
- The §6.1 cookbook entry lets a new contributor add an invariant test without asking.
