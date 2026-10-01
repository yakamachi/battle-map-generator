<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Encounter Parameters (S-03)

- **Plan**: context/changes/encounter-parameters/plan.md
- **Scope**: Full plan (phases 1–3 committed through fb86cb1; phase 4 staged, not yet committed)
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-10-01
- **Verdict**: NEEDS ATTENTION
- **Findings**: 1 critical (fixed during review), 2 warnings, 6 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | FAIL (F1, fixed during review) |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | WARNING |

## Evidence

- Two reviewers: plan drift across all four phases, and safety/pattern with the web side, the validation rework and the e2e spec in focus. No functional drift; every Changes Required item is present; API → contract → `maps.ts` → form → renderer → PNG agree; boundary files identical to `main`; contract files look generated.
- All automated criteria of phases 1–4 re-run on the current tree: API 118 (128 after the F1 fix), contract no drift, web type-check/build/ui:scan, web 58, visual 28 passed / 2 skipped, atlas unchanged, e2e 4 passed, e2e type-check.
- On a real host (Production, port 5109): before the fix `{"encounter":"ambush"}` and `{"encounter":1}` returned 400 with an empty body, and `{"encounter":"boss","bossSize":"huge, gargantuan"}` returned 500.

## Findings

### F1 — A comma-list boss size reaches the generator (500); binding errors return an empty 400

- **Severity**: ❌ CRITICAL
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Maps/MapModels.cs (GenerateMapRequest), api/Maps/MapEndpoints.cs
- **Detail**: The enum converter reads `"huge, gargantuan"` as flags 1|2 = `(BossSize)3`, undefined; nothing validated it and `MapSize.MinArenaSide` threw. Separately, values JSON binding rejects got a 400 with no body, contradicting the staged `api/AGENTS.md`.
- **Fix**: The handler reads the body itself and maps a `JsonException` to a validation problem naming the field (`request` for broken JSON); `.Accepts<GenerateMapRequest>` keeps the OpenAPI request schema; `[EnumDataType]` on both enums rejects undefined flag combinations.
- **Decision**: FIXED during review at the owner's request ("fix the enum 400 to return a proper error body"); 11 new endpoint test cases; break-checked; `api/AGENTS.md` corrected.

### F2 — The e2e boss spec would pass with a blank canvas

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: e2e/tests/parameters.spec.ts:35-51
- **Detail**: It checks only canvas attributes and the PNG's IHDR dimensions, which are set before drawing and hold for a blank image. `map.spec.ts` guards this with distinct-pixel sampling and a minimum PNG size; the new spec, drawing the largest canvas in e2e (6160×3360), drops both.
- **Fix**: Add the sampled distinct-pixel check and a PNG size floor measured for this map.
- **Decision**: FIXED — the spec samples distinct pixels on the canvas and requires the PNG to be over twice the size of a blank PNG of the same dimensions, encoded by the same browser. Break-check: a renderer that draws only paper turned it red in both browsers.

### F3 — The "largest map" render test cannot fail; WebKit caps canvases lower

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: web/app/map/render.test.ts:85-92, web/app/map/render.ts:8-13
- **Detail**: The test reads back the canvas size it just assigned, which holds even when allocation fails silently — the failure the guard exists for. WebKit (Safari and every iOS browser) caps a canvas at 16,777,216 px; a 9-room skirmish (36×26, 18.3 M px), a 7-room Large boss (17.2 M px) and the largest map (31.8 M px) exceed it and would preview blank there. The PRD requires only Chrome and Firefox.
- **Fix A ⭐ Recommended**: Make the test read a pixel after rendering (paper white, not transparent), and have `renderMap` probe a pixel after allocating and throw a clear "too large for this browser" error if the canvas did not allocate.
  - Strength: A failed allocation becomes a visible error everywhere, not a blank preview; the test then protects something.
  - Tradeoff: One `getImageData` per render; a few lines in `render.ts`.
  - Confidence: HIGH — a failed canvas reads back transparent.
  - Blind spot: Not tried on a real iOS device.
- **Fix B**: Strengthen the test only and record WebKit/iOS as unsupported above ~855 squares in `web/AGENTS.md`.
  - Strength: No product code change.
  - Tradeoff: On Safari the preview still fails silently.
  - Confidence: HIGH.
  - Blind spot: None significant.
- **Decision**: FIXED via Fix A — `renderMap` reads back one pixel after filling the paper and throws "too large to render in this browser" when it is transparent; the largest-map test now checks an opaque paper pixel in the far corner, and a new test simulates an unallocated canvas. Break-check: removing the probe turned the new test red.

### F4 — Validation messages and multiple errors after the rework

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/MapModels.cs
- **Detail**: `[Range]` reported "The field RoomCount must be between 2 and 12."; and `Validate` (boss needs a size) does not run when a property rule fails, so one error is reported at a time.
- **Fix**: Set `ErrorMessage`; accept one-error-at-a-time.
- **Decision**: FIXED (message) as part of the F1 fix; one-error-at-a-time ACCEPTED.

### F5 — A rule without a member name would be dropped

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/MapEndpoints.cs (Validate)
- **Detail**: A `ValidationResult` without `MemberNames` added no error, so the request went on to the generator.
- **Fix**: Report it under `request`.
- **Decision**: FIXED as part of the F1 fix.

### F6 — A 400 tells the user to try again

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/home.tsx:47-56, web/app/api/maps.ts:46-49
- **Detail**: A 400 shows "The server could not generate a map (error 400). Please try again."; retrying cannot help. The form cannot produce a 400 today, so this matters only if API and web limits drift.
- **Fix**: Give 400 its own message ("These map settings are not supported.").
- **Decision**: FIXED — `home.tsx` shows "These map settings are not supported. Change them and generate again." for a 400 (inside the existing `http` case, so `client.ts` stays untouched).

### F7 — The label's disabled styling never applies

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/components/ui/label.tsx:11, web/app/routes/home.tsx
- **Detail**: `peer-disabled:` and `group-data-[disabled=true]:` have no matching `peer` or `group` in `home.tsx`, so while busy the select dims but its label does not.
- **Fix**: Mark each label+select wrapper in `home.tsx` as `group` with `data-disabled` while busy.
- **Decision**: FIXED — each label+select wrapper in `home.tsx` is a `group` with `data-disabled` while busy, so labels dim with their selects; the six `loading-*` screenshots were re-baselined for that.

### F8 — Stale text outside the code

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: e2e/playwright.config.ts:8-9, context/changes/encounter-parameters/plan-brief.md
- **Detail**: The config comment says "two calls per browser" (now three, six per run). The brief's "Open Risks" are resolved (optionality verified, size table held), it mentions one extra fixture instead of two, and it does not describe the validation rework.
- **Fix**: Update the comment and the stale brief lines.
- **Decision**: FIXED — `playwright.config.ts` says three calls per browser, six per run; `plan-brief.md` marks the two risks resolved, records the validation rework and names both extra fixtures.

### F9 — `generateMap` in client.ts is unused

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architecture
- **Location**: web/app/api/client.ts:19
- **Detail**: Nothing imports it; intended until `maps.ts` merges into `client.ts` after S-04 (recorded in `change.md`).
- **Fix**: None now.
- **Decision**: ACCEPTED — merge after S-04.
