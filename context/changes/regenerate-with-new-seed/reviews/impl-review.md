<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Generate Button Dice States (S-02)

- **Plan**: context/changes/regenerate-with-new-seed/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-19
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Evidence: branch `6b6939f..0587d29`. Every planned change in both phases matches the plan with its Addendum applied, and phase 1 review fixes F1–F4 are in HEAD. The F1 class string is correct since 0013153. Baselines changed only for the home shots, `save-dialog` and the 3 new `ready-params-changed` shots. There is no API, e2e or fixture change, and `saved-map.tsx` is untouched. Every other Button usage was checked: none sets `aria-disabled` or `aria-invalid`. Gates on the final code: typecheck, `ui:scan`, 108/108 tests, build, the Docker visual gate (61 passed in three runs) and e2e (11/11). The one 31.9 s `loading-regenerate` failure came during the deliberately broken run. It was the 30 s test timeout, after the broken build's chained expects ran long, not a race with the minimum roll.

## Findings

### F1 — The status region repeats an old "New map generated" message after an error and during a redraw

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality (accessibility)
- **Location**: web/app/routes/home.tsx:291-297
- **Detail**: The text is derived as `loading ? "Generating a map…" : flash.count > 0 ? "New map generated, seed <flash.seed>" : ""`, and `flash` is never cleared. A failed Generate after a successful one goes from "Generating a map…" back to the previous "New map generated, seed <old>". That is a text change, so it is announced together with the error alert. A successful second Generate briefly shows the previous seed's message while the new map draws (status `ready`, `drawnMap` still null), and only then the new seed.
- **Fix**: Keep the announcement in its own state: set "Generating a map…" in `onGenerate`, set "New map generated, seed N" in the effect's `drawnMap` branch, and clear it in the error branch so the Alert speaks alone.
- **Decision**: FIXED: an `announcement` state, set to "Generating a map…" in onGenerate and "New map generated, seed N" when the map is drawn, cleared on error; `flash` reduced to `flashCount`

### F2 — Manual check 1.9 predates the dimming fix, and no screenshot pins the reduced-motion busy state

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: web/visual/home.visual.spec.ts; plan.md Progress 1.9 (— 09a1096)
- **Detail**: 1.9 ("Ready, Rolling and Reroll stay distinguishable under reduced motion") was confirmed on 09a1096. The dimming that makes that true landed later (F1 of the phase 1 review, effective from 0013153). The `loading-*` baselines run without reduced motion, so the reduced-motion busy look (dimmed d20 showing "20") is checked by nobody.
- **Fix**: Add one `loading-first-reduced-motion` shot using `test.use({ reducedMotion: "reduce" })`, in all three projects.
- **Decision**: FIXED: new `loading-first-reduced-motion` shot (reducedMotion: "reduce") with 3 baselines, showing the dimmed d20 with its resting "20"

### F3 — `ready-params-changed` takes its screenshot without asserting the state change

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: web/visual/home.visual.spec.ts:110-119
- **Detail**: The test is deterministic, but only the pixels prove that Generate left Reroll. A regression baselined with `visual:update` would pass silently.
- **Fix**: Assert the Generate button's `data-variant` before the shot: `default` in `ready-params-changed`, `accent` in `ready`.
- **Decision**: FIXED: `ready` asserts `data-variant="accent"` and `ready-params-changed` asserts `data-variant="default"` before the shot (the redundant mouse move was dropped)

### F4 — web/AGENTS.md says the dice pop replays through counters

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence (docs)
- **Location**: web/AGENTS.md:42
- **Detail**: Only the ring pulse, the glow and the shake use counters. The pop plays when the dice icon remounts on the swap, and only while `popDice` is set (after a Generate).
- **Fix**: Reword it: "the ring pulse, glow and shake replay through counters; the dice pop plays on the icon swap only after a Generate (`popDice`)".
- **Decision**: FIXED: web/AGENTS.md reworded: counters drive the pulse, glow and shake; the pop plays on the swap only after a Generate (`popDice`)

### F5 — change.md Notes still describe the pre-plan idea

- **Severity**: 💬 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence (docs)
- **Location**: context/changes/regenerate-with-new-seed/change.md (Notes)
- **Detail**: The Notes mention a "~600ms flash", a "dice+↻ icon", "disabled" and an announcement without the seed. What shipped is D4 (a pop into two dice, `aria-disabled`, a seeded announcement and a 0.9 s minimum roll), recorded in the plan Addendum.
- **Fix**: Append one line to Notes: "Shipped as prototype D4; see plan.md Addendum for the deviations."
- **Decision**: FIXED: added a pointer to the D4 prototype and the plan Addendum at the end of change.md Notes
