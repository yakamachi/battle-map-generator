<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Encounter Parameters (S-03)

- **Plan**: context/changes/encounter-parameters/plan.md
- **Scope**: Phase 3 of 4
- **Reviewed phases**: 3
- **Date**: 2026-09-30
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Evidence

- Reviewed inline against commit fb86cb1: `web/app/api/maps.ts`, `web/app/components/ui/{label,native-select}.tsx`, `web/app/routes/home.tsx`, `web/app/app.css`, `web/app/components/map-preview.tsx`, `web/app/map/render.ts`, `web/app/map/render.test.ts`, `web/visual/home.visual.spec.ts`, and the regenerated screenshots.
- Every Changes Required item is present: the request module mirrors `client.ts`'s result and error mapping and carries the merge note; the form has the three labelled controls with Boss size shown only for a boss fight; button names, the single canvas and `Seed: N` are unchanged; the height budget grows for the form row (and the wrapped row on phones); the empty-state text names no size; the area guard exists with a test.
- Deviations from the plan's wording, all within intent: primitives copied by hand rather than via `shadcn add` (the CLI output would add `radix-ui` and `lucide-react`); `idle-focus` presses Tab three times because the form precedes Generate; the `ready-boss` visual test also asserts the posted body.
- Automated criteria 3.1–3.6 green before the commit (web tests 58, visual 28 passed / 2 skipped, e2e 2 passed); break-check on the area guard went red and was restored. Manual 2.6 and 3.7–3.9 confirmed by the owner.

## Findings

### F1 — Native select options use system colours

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/components/ui/native-select.tsx:44-50
- **Detail**: `NativeSelectOption` uses `bg-[Canvas] text-[CanvasText]`, CSS system colours carried over from shadcn. They are not tokens and not literal colours, and `ui:scan` accepts them; they make the browser's native option list follow the OS theme, which tokens cannot style reliably. Nothing says why they are there, so a later reader may "fix" them into tokens.
- **Fix**: Add a one-line comment explaining the system colours.
- **Decision**: FIXED — comment added above `NativeSelectOption`.

### F2 — Room-count limits are duplicated in the web app

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architecture
- **Location**: web/app/api/maps.ts:14-16
- **Detail**: `MIN_ROOM_COUNT`, `MAX_ROOM_COUNT` and `DEFAULT_ROOM_COUNT` mirror `MapSize` by hand. The OpenAPI document now carries `minimum`/`maximum` for `roomCount`, but openapi-typescript does not emit them as constants, so there is no generated source to import. If the API limits change, the form drifts; the API's 400 is the backstop and the comment names the source.
- **Fix**: None now; note it for the `client.ts` merge after S-04.
- **Decision**: ACCEPTED — no code change; recorded in `change.md` Notes for the merge of `maps.ts` into `client.ts` after S-04.
