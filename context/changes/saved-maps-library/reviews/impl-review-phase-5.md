<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Phase 5 of 5
- **Reviewed phases**: 5
- **Date**: 2026-10-18
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Notes:
- Every Phase 5 item in commit de54b5e is done:
  - **PRD v3:** US-02, and FR-009 added without renumbering FR-001–FR-008. The library line left Non-Goals; the hierarchy line stays. The secondary criterion is set, and the version change carries dated notes.
  - **Shape-notes:** dated reversals.
  - **Roadmap:** S-05 in At-a-glance, the Slices body, Streams and Backlog Handoff, in the template shape. The Parked line is gone and `updated` is bumped.
  - **Tech-stack docs:** all three updated.
  - **infrastructure.md:** the database line, backup row, two risk rows and the note that the free-offer storage limit isn't recorded.
  - **test-plan.md:** the IDOR pointer, §7, and a freshness-ledger entry. The rollout phases are unchanged.
- Nothing was written to `context/archive/`.
- **Every number and test name the docs cite matches the code:**
  - routes and status codes; the owner filter in one query; cap 100 with the race;
  - the migration's column types; 44×24 = 1 056 cell bytes + 40 room bytes;
  - rate limits 60/10/50/50;
  - `Another_accounts_map_is_404_for_get_rename_and_delete` and `List_shows_only_own_maps_newest_first`.
- After the user's reading (5.3), on 2026-10-18:
  - the PRD title rule gained "ani niewidocznych";
  - M-1's intent now includes the library;
  - S-02's Unknowns note that Generate already rerolls and raise whether to keep showing the seed.
- **Success criteria:**
  - 5.1: the stale-wording grep finds only the dated historical notes (shape-notes:27, :296).
  - 5.2: the roadmap matches at the At-a-glance row, the S-05 body and Backlog Handoff.
  - 5.3: confirmed by the user, with the three edits above.

## Findings

### F1 — The title rule is under-described in api/AGENTS.md and the dialog comment

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/AGENTS.md:48; web/app/components/map-title-dialog.tsx:19-21
- **Detail**: Both still say "no control characters". Since phase 2 review F6, the code (`SavedMapModels.cs` `IsRefused`) also refuses Unicode Format characters (zero-width, bidi overrides, the emoji joiner) and line/paragraph separators. The PRD now says so; the API rules file that the docs point to does not.
- **Fix**: Change both to "no control characters, invisible format characters (zero-width, bidi) or line/paragraph separators".
- **Decision**: FIXED: api/AGENTS.md and the map-title-dialog.tsx comment name control characters, invisible format characters (zero-width, bidi, the emoji joiner) and line/paragraph separators.

### F2 — The per-map size range is loose at both ends

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: context/foundation/infrastructure.md:38, :171
- **Detail**:
  - Smallest map (2 rooms, 20×14): 280 + 10 bytes, plus row and index overhead, about 0.55–0.75 KB.
  - Largest (12 rooms with a gargantuan boss, 54×30): 1 620 + 60 bytes plus overhead, about 2.2 KB, so up to ~220 KB per account.
  - "About 0.7–2 KB" understates the top end.
- **Fix**: Say "about 0.6–2.2 KB (the largest map is 54×30)" and "≤ ~220 KB per account" in both places.
- **Decision**: FIXED: infrastructure.md says about 0.6â2.2 KB per map (largest 54Ã30 = 1 620 + 60 bytes) and â¤ ~220 KB per account, in the database line and the storage risk row.

### F3 — "Registration's 50/min global limit" is register and login combined

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: context/foundation/infrastructure.md:171
- **Detail**: `AuthEndpoints.GlobalAuthLimit` (50/min) is shared by register and login. It still bounds account creation, but the wording names only registration.
- **Fix**: "the 50/min global register-and-login limit".
- **Decision**: FIXED: the storage risk row says „the 50/min global register-and-login limit”.

### F4 — S-02 does not list S-05 as parallel

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: context/foundation/roadmap.md:110
- **Detail**: S-05's body says "Parallel with: S-02", but S-02 still reads "F-01, S-03, S-04". The other slices list each other in both directions.
- **Fix**: Add S-05 to S-02's "Parallel with" line.
- **Decision**: FIXED: S-02 now lists S-05 under Parallel with.

### F5 — "Po przycięciu spacji" calques "trim"

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: context/foundation/prd.md:104
- **Detail**: "Po przycięciu spacji" is a loan translation of "trim". It is also slightly wrong: the code trims all leading and trailing whitespace, not only spaces.
- **Fix**: "po usunięciu białych znaków z początku i końca".
- **Decision**: FIXED: PRD US-02 says „po usuniÄciu biaÅych znakÃ³w z poczÄtku i koÅca”.
