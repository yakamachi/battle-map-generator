# Seed determinism (risk #2) — Plan Brief

> Full plan: `context/changes/testing-seed-determinism/plan.md`
> Research: `context/changes/testing-seed-determinism/research.md`

## What & Why

A DM must be able to reproduce a map they liked by its seed. Risk #2 in `context/foundation/test-plan.md` says that must hold across requests, processes and web re-renders. Today the generator is deterministic by construction, but only same-process and fixture checks prove it, and no test covers the endpoint round trip or interleaved web renders.

## Starting Point

- Generator: fresh seeded PRNG per call and an explicit corridor tie-break (`api/Maps/BspGenerator.cs:64`, `:445-449`).
- Tests: a same-process check (`api.Tests/MapGeneratorTests.cs:89`), five committed fixtures, and near-self-comparisons on the web side (`web/app/map/tileset.test.ts:169-183`).

## Desired End State

Three new layers fail if a seed stops reproducing its map: a cross-process API check, endpoint round-trip checks, and a web interleave check. §6.2 of the test plan documents them. No production code changes.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Cross-run mechanism | Child `dotnet test` process, no new fixtures | Catches runtime-level drift that a same-process test cannot, without growing the pinned set | Plan (user) |
| Endpoint round trip | Assert the omitted-seed echo reproduces the map | The seed a DM sees must be the one that reproduces their map | Plan (user) |
| Web interleave | New test beside the existing ones | Directly catches a shared PRNG and leaves working tests alone | Plan (user) |
| Re-render level | Pure `drawOps`, no `renderMap` or Playwright | Same property at millisecond cost, no canvas or CI image | Research |
| Oracle | `AGENTS.md` determinism rule and PRD FR-005 | Tests must not pin current output as truth | Research |

## Scope

**In scope:** API cross-run check (seeds 1–200, skirmish and Huge at 6 rooms); endpoint explicit-seed and omitted-seed round trip; web interleave for each fixture; §6.2 cookbook and §3 status.

**Out of scope:** generator, PRNG and fixture changes; `renderMap` re-render tests; Playwright visual checks; web client seed resolution; restart or DB tests; e2e.

## Architecture / Approach

Stage 1 adds a guarded test entry point and a helper that runs it in a child `dotnet test --no-build` process. Stages 2–4 are independent assertions on that harness and the endpoint and web layers. Stage 5 records the result in the test plan.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Cross-run harness (setup) | Child-process helper and guarded emit test | Child run adds CI time; recursion if the guard is missing |
| 2. API cross-run determinism | Seeds 1–200 equal across processes | Matrix cost; the check must be proven to fail on a shifted seed |
| 3. Endpoint round trip | Explicit-seed and omitted-seed equality | Two extra requests per test against the 10/min limit |
| 4. Web interleave | A, B, A equality per fixture | Test passes trivially if the fixtures do not vary by seed |
| 5. Cookbook and status | §6.2 filled, §3 Phase 1 and 2 status | Stale status text if not updated with the commit |

**Prerequisites:** Phase 1 (sweep) closed out, which it is at `1c5717f`. The Phase 1 impl-review is not on disk yet and must run before Phase 2 is implemented (repo phase gate).
**Estimated effort:** about 2 sessions across 5 phases.

## Open Risks & Assumptions

- The child-process fact adds a `dotnet test` start to CI; if its time is too high, the matrix is reduced in a follow-up.
- The cross-process check only catches runtime-level drift that the child run can reproduce; it cannot catch a difference between two machines (accepted: CI runs on one platform).
- The web interleave test checks the property through `drawOps` only; a regression confined to `renderMap` is not caught.

## Success Criteria (Summary)

- All five phases' automated criteria pass, and the deliberate-break checks fail as expected.
- §6.2 names a file and command for each determinism layer.
- Phase 1 sweep and Phase 2 determinism gates are both wired in CI (`ci.yml` `api` and `web-tests` jobs).
