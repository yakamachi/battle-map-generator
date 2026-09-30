# Encounter Parameters (S-03) — Plan Brief

> Full plan: `context/changes/encounter-parameters/plan.md`
> Research: `context/changes/encounter-parameters/research.md`

## What & Why

The DM chooses how many rooms the map has (2–12, default 6) and whether it is a skirmish or a boss fight with a boss size. A boss fight turns one room into a marked arena big enough for the boss. This is FR-002 and the PRD's business rule, and it replaces the single fixed 30×20 map from S-01.

## Starting Point

The request carries only a seed, the handler always generates 30×20, and the generator cannot target a room count: at 30×20 it gives 5–8 rooms, 6 in 681 of 1000 seeds. There is no arena concept in the grid and no form in the home view.

## Desired End State

The home view has Rooms, Encounter and Boss size controls. Generate returns a map with exactly the chosen number of rooms, sized from that number, and on a boss fight one room is a visibly different arena of at least 8×8, 10×10 or 12×12. The default request still gives a 6-room 30×20 map, so the existing end-to-end test passes untouched.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Sending parameters with `client.ts` off limits | New module `web/app/api/maps.ts`, merged into `client.ts` after S-04 | Keeps the parallel boundary intact | Research (owner) |
| Does the arena count toward N | Yes, it is one of the N rooms | Matches "exactly as many rooms as chosen" | Research (owner) |
| Shared UI files with S-04 | S-03 adds what it needs; the second branch to merge rebases and re-baselines | Neither slice waits for the other | Research (owner) |
| Arena mark in the grid | New cell kind `bossArena` plus a room kind | The renderer sees it without reading rooms, and tests find the arena directly | Plan (owner) |
| Map size | Table by room count, smaller and larger than 30×20; a boss widens the map | Rooms stay a similar size at every count | Plan (owner) |
| Arena look | Existing `tiles_decorative` atlas piece, removed as a random variant on plain floors | Visible mark with no new art or atlas rebuild | Plan (owner) |
| Boss fight without a boss size | 400 | The PRD makes boss size part of a boss fight | Plan |
| Room count control | A select of 2–12 | No invalid value can be entered | Plan |
| Request fields | All optional in OpenAPI | The untouched `client.ts` must keep type-checking | Research |

## Scope

**In scope:**
- Request parameters, validation (400) and parameters echoed in the response
- Generator: exact room count, size table, arena placement
- `bossArena` rendering, one boss fixture, re-pinned default fixtures
- Form in `home.tsx`, label and native select primitives, preview height budget, canvas area guard
- New e2e spec, updated agent docs

**Out of scope:**
- `api/Program.cs`, `web/app/api/client.ts`, `web/app/routes.ts`, existing `MapEndpointTests` methods, `e2e/tests/map.spec.ts`
- Regenerate flow (S-02), login and limits (S-04)
- New tileset art, boss-specific layout rules, loops, decorations
- Raising the 60×60 cap

## Architecture / Approach

Contract outward. The C# records gain the parameters, a `bossArena` cell kind and a room kind; the OpenAPI document and TypeScript types are regenerated, never hand-merged. The generator splits until it has exactly N leaves, reserving one arena-sized leaf on a boss fight, with explicit tie-breaks. Map size comes from a table: 20×14 for 2 rooms, 30×20 for 6, 42×30 for 12; a boss adds 8, 10 or 12 squares of width, so the largest map is 54×30. The web renderer maps `bossArena` to an existing piece, and `home.tsx` sends the form through its own small request module.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Contract and generator | API accepts and echoes parameters; exactly N rooms; arena; fixtures and baselines re-pinned | New request fields come out `required` and break `client.ts`; a table row cannot always hold its rooms |
| 2. Arena in the picture | `bossArena` drawn differently; boss fixture pinned in both browsers | The decorative piece does not read clearly as an arena |
| 3. Parameters form | Controls in the home view, new request module, screenshots | Overlap with S-04 in `app.css`, `components/ui` and screenshots |
| 4. End to end and docs | Boss flow tested in Chrome and Firefox; agent docs updated | Rate limit shared with `map.spec.ts` (6 of 10 calls per run) |

**Prerequisites:** branch `feat/encounter-parameters` from `main` at 2fc7bda; Docker for the API tests and the visual gate.
**Estimated effort:** about 4 sessions, one per phase, each followed by `/10x-impl-review`.

## Open Risks & Assumptions

- Not yet verified: whether a defaulted record parameter is non-required in the build-time OpenAPI document. It is the first check in Phase 1, with a fallback shape.
- The size table is derived from leaf arithmetic, not from a run of the new algorithm. Phase 1's seed tests confirm it; a failing row is adjusted there (6 → 30×20 is fixed).
- Both branches regenerate the contract files and screenshots; the second to merge must rebase and regenerate, and fold S-04's `generateMap` changes into `maps.ts`.
- Every seed's map changes, which the project rules allow.

## Success Criteria (Summary)

- A DM picks 2–12 rooms and gets exactly that many, on a map sized to match.
- A boss fight shows one clearly marked, largest room that meets the minimum for the boss size, in the preview and in the PNG.
- Nothing S-04 owns was touched, and the existing default flow and its tests are unchanged.
