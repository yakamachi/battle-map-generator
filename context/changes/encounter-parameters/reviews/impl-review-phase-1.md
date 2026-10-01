<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Encounter Parameters (S-03)

- **Plan**: context/changes/encounter-parameters/plan.md
- **Scope**: Phase 1 of 4
- **Reviewed phases**: 1
- **Date**: 2026-09-30
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

## Evidence

- All eight automated criteria re-run on commit 8b348ba: API tests 109 passed; no contract drift; web type-check, build and 36 tests pass; visual gate 22 passed; `map.spec.ts` unchanged and green in both browsers; boundary files identical to `main`.
- Reviewer probe (scratch only, not committed): all 44 parameter combinations × seeds 0–20000 plus five seeds near the `uint` limits, 880,264 maps, 0 exceptions and 0 invariant failures. The free-size overload over 300,000 random combinations: 240,551 generated with 0 failures, 59,449 rejected, all with `ArgumentOutOfRangeException`.
- Cost at 12 rooms, Gargantuan, 54×30: 0.63 ms average over 2,000 seeds.
- Every planned Contract bullet was checked against the code and matches. Arena placement (a full-height strip on the left or right edge) and the splitting rule are choices the plan left open.

## Findings

### F1 — Enum binding accepts numbers and comma lists the contract does not allow

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Maps/MapModels.cs (MapJson.Configure), api/Maps/MapEndpoints.cs:33-45
- **Detail**: The OpenAPI contract says `encounter` and `bossSize` are string enums, but the converter also accepts `{"encounter":1}`, `{"encounter":"1"}` and comma lists: `{"bossSize":"large, huge"}` is read as Huge and `"large,gargantuan"` as Gargantuan. None is a 500, but a comma list silently gives a different map from what was written. The handler's `Enum.IsDefined` checks (not in the plan) only catch out-of-range numbers, and make a skirmish with an undefined numeric `bossSize` a 400 instead of ignoring it. The only test is `"encounter": 7`.
- **Fix A ⭐ Recommended**: Make the binding strict: disallow integer values in the converter, reject comma lists, drop the then-unneeded `IsDefined` checks, and add tests for a number, a numeric string and a comma list (all 400).
  - Strength: The API accepts exactly what the contract documents; one rule instead of a partial guard.
  - Tradeoff: The converter is shared with `CellKind` output and the fixture files, so the OpenAPI document and fixtures must be regenerated and shown unchanged.
  - Confidence: MED — `allowIntegerValues: false` is standard; whether it also stops comma lists is not verified and may need a small strict converter.
  - Blind spot: Behaviour for `"1"` and comma lists after the switch is unverified until run.
- **Fix B**: Accept the leniency: keep the code, update the comment and the plan to say numbers and comma lists are tolerated, and add tests pinning that.
  - Strength: No code risk.
  - Tradeoff: The API stays looser than its contract, and the comma-list surprise remains.
  - Confidence: HIGH — documentation only.
  - Blind spot: None significant.
- **Decision**: FIXED in part via Fix A — integer values are now rejected (`allowIntegerValues: false`), the `IsDefined` checks are removed, and a new test covers a number and a numeric string for both enums (400). Comma lists are NOT fixed: the built-in converter still accepts them, and wrapping it to reject them made the OpenAPI generator drop the `CellKind`, `EncounterType` and `BossSize` enum schemas (verified by a build), which cannot be corrected without `api/Program.cs`. The leniency is documented in the comment on `MapJson`. Revisit after S-04 merges, when `Program.cs` is free. Follow-up (2026-09-30, owner's request): the request rules moved onto `GenerateMapRequest` as data annotations (`[Range]` on `RoomCount`, `IValidatableObject` for the boss-size rule), run by hand from `MapEndpoints.cs` until `AddValidation()` can be registered in `Program.cs` after S-04. Binding the two enums as text with `[AllowedValues]` was tried and does reject comma lists, but the OpenAPI generator then types them as plain `string`, so the enum properties were kept and comma lists remain accepted.

### F2 — No fixture pins the new seeded code

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Success Criteria
- **Location**: api.Tests/MapFixtureTests.cs:15-21, fixtures/grids/
- **Detail**: All three fixtures are the default 6-room skirmish. Arena reservation, the arena-first draw order, the trim loop and non-default room counts are pinned by no fixture; the determinism test compares two runs in one process, so it cannot catch ordering drift across a runtime upgrade (the lesson "Seeded code breaks ties explicitly" came from exactly that). The plan adds one boss fixture in Phase 2 and no non-default skirmish fixture at all.
- **Fix A ⭐ Recommended**: Add one small non-default skirmish fixture now (for example seed 42, 3 rooms, 24×14) with its render hashes; keep the boss fixture in Phase 2 as planned, where the renderer learns `bossArena`.
  - Strength: Pins the new splitting code today without drawing a cell kind the renderer does not know yet.
  - Tradeoff: One more fixture and two more render hashes to re-baseline on later algorithm changes.
  - Confidence: HIGH — the render test globs fixtures automatically.
  - Blind spot: None significant.
- **Fix B**: Defer: note in the plan that Phase 2 adds both a boss fixture and a non-default skirmish fixture.
  - Strength: No Phase 1 rework.
  - Tradeoff: The new code stays unpinned until Phase 2 lands.
  - Confidence: HIGH.
  - Blind spot: None significant.
- **Decision**: FIXED via Fix A — `fixtures/grids/seed-42-rooms-3.json` (seed 42, 3 rooms, 24×14) pinned by `Three_room_map_matches_its_fixture` and by a render hash per browser. The boss fixture stays in Phase 2.

### F3 — No test that an empty body or `{}` gives the defaults

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: api.Tests/MapEndpointTests.cs:153-168
- **Detail**: The new defaults test sends `{ seed: 42 }`; the untouched empty-body test checks only the cell count. Nothing asserts that an empty body and `{}` return 6 rooms and echo 6 / skirmish / null, which is what the unchanged `client.ts` and `map.spec.ts` rely on.
- **Fix**: Add one new endpoint test asserting both requests return 6 rooms, 30×20 and the default parameters.
- **Decision**: FIXED — new theory `An_empty_body_or_a_body_without_parameters_gives_the_default_map` (empty body, `{}`, `{ "seed": null }`).

### F4 — Clarity: unstated tie-break, a guarantee held by cost, and a misnamed exception parameter

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/Maps/BspGenerator.cs:298-308, :23-26, :101-105, :160-164
- **Detail**: (1) `Connect` resolves equal distances by strict `<` in tree order; deterministic and ours, but unlike the other tie-breaks it is not stated. (2) "Corridors are one cell wide" holds only through the `AlongsideCorridorCost` penalty, not by construction; it held on 1.12 M probed maps but a cost tweak could break it, and nothing says so. (3) The "size cannot hold the rooms" and "arena does not fit" failures throw `ArgumentOutOfRangeException(nameof(roomCount), …)` although the cause is the width and height.
- **Fix**: Add the two one-line comments, and name the size in the two exceptions.
- **Decision**: FIXED — two comments added; both exceptions now name the size (`width`) and the message names the room count.

## Notes for later phases

- Phase 3: the generated `BossSize` type in `schema.d.ts` includes `null`, so the form needs `NonNullable<>` or its own union.
- The arena is always exactly the minimum width and sits on the left or right edge; a product call, not a defect.
