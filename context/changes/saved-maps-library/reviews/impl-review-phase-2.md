<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Phase 2 of 5
- **Reviewed phases**: 2
- **Date**: 2026-10-17
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Notes:
- **Plan drift:** every planned item in commit 161484e is a MATCH (§1–§7, F4–F8). The disclosed adaptations are benign extras:
  - the capped read plus `WithBodySizeLimit()` for F6, which was the plan's own fallback;
  - `JsonEnumNameConverter`;
  - room bounds checks in the codec;
  - the exact-bytes test.

  The wire format, OpenAPI and fixtures are untouched (`git diff 3e9afdf 161484e -- api/BattleMapGenerator.Api.json web/ fixtures/` is empty), and the Phase 1 test names are unchanged.
- **Verified clean:**
  - Body cap: chunked and Content-Length paths, the exact 16 KB boundary, and bounded buffering were checked with a throwaway test. The filter catches only the 413.
  - Ownership: one query per route, with no load-then-compare.
  - Rename: the concurrency catch is narrow.
  - Migration: it only adds things, and the column lengths are enough.
  - Titles: `char.IsControl` covers C0, DEL and C1.
  - Patterns match `MapEndpoints` and `AuthEndpoints`.
- **Success criteria:**
  - 2.1 `dotnet test api.Tests`: 207/207.
  - 2.2 Contract drift: clean after `--no-incremental` rebuild and `api:types`.
  - 2.3 Break-check: swapping Floor and Wall turned the enum-pin and exact-bytes tests red. Round-trip stays green by design; the criterion text was corrected.
  - 2.4 Manual check ran against the local SQL Server: save, list, get, rename, delete, 413, no-Content-Type 404, control-character 400. The stored row showed 1056 cell bytes and 40 room bytes matching the API's rooms.
- **For the PR description:** the unmerged Phase 1 migration was regenerated, so it now has a new timestamp. A local database that applied `20261017101522_SavedMaps` must drop the table and its history row. The developer's database never applied it.

## Findings

### F1 — Rename stores the new title before decoding the grid

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapEndpoints.cs:191-201
- **Detail**: Rename sets the title, calls `SaveChangesAsync`, and only then runs `ToSavedMap(row)`, which decodes `Cells`/`Rooms`. On a row whose bytes don't decode, the rename is committed but the caller gets a 500, so the client believes it failed when it didn't.
- **Fix**: Build the response with `ToSavedMap(row)` after setting the title and `UpdatedAt` but before `SaveChangesAsync`, so a broken map refuses the rename without writing anything.
- **Decision**: FIXED: Rename builds the response before SaveChangesAsync; the corrupted-Cells test now also checks that a rename of the broken map fails and stores nothing (break-check: red when reverted).

### F2 — An oversized generate body spends a rate-limit permit, undocumented

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/MapEndpoints.cs:32-33; api/Maps/RequestBody.cs:19-30; api/AGENTS.md (Map generation contract)
- **Detail**:
  - `UseRateLimiter` runs before endpoint filters. A generate request over 16 KB therefore spends one of the 10/min `generate` permits before the 413 is returned; the reviewer verified this (10 oversized requests followed by a normal one got 429).
  - This is defensible, but AGENTS.md only states that a 401 spends no permit.
  - 413 is also not declared with `.Produces(413)`, which keeps OpenAPI unchanged as the plan required.
- **Fix**: Add one sentence to api/AGENTS.md: a body over 16 KB answers 413 after the rate limiter, so on generate it spends a permit, and 413 is deliberately absent from OpenAPI.
- **Decision**: FIXED: api/AGENTS.md says the 413 runs after the rate limiter (an oversized generate spends a permit) and is deliberately absent from OpenAPI.

### F3 — An unreadable Encounter/BossSize value breaks the whole list

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Data/AppDbContext.cs:49-52; api/AGENTS.md (Saved maps)
- **Detail**:
  - The converter uses `Enum.Parse(name, ignoreCase: true)`. It throws `ArgumentException` on an unknown string, and it accepts numeric strings ("7") and comma lists, which produce undefined values.
  - The list projects these columns, so one bad value turns the whole list into a 500.
  - AGENTS.md's "one unreadable grid fails only its own GET" holds for `Cells`/`Rooms`, not for these columns. This is only reachable through a corrupted or hand-edited row, or a future enum rename.
- **Fix**: Make the converter strict, with a non-numeric `TryParse` plus `Enum.IsDefined` that throws `InvalidDataException` otherwise. Add one AGENTS.md line saying an unreadable parameter column fails the list.
- **Decision**: FIXED: the converter reads only exact JSON names, else InvalidDataException; new theory A_row_with_an_unreadable_encounter_fails_to_open ("7", "Boss", "skirmish,boss"; break-check: red with Enum.Parse); AGENTS.md notes that a bad parameter column fails the list.

### F4 — Decoding does not check Width/Height or that rooms lie inside the map

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/GridCodec.cs:29-47, 70-97
- **Detail**:
  - `DecodeCells` computes `width * height` unchecked. Corrupted columns such as 65536×65536 overflow to 0 and match an empty `Cells`.
  - Width and Height are never checked against 1..60, and rooms are not checked against the map bounds.
  - This is reachable only through a corrupted database; allocations are bounded by the database's byte length, never by a client.
- **Fix**: In `DecodeCells`, require 1 ≤ width, height ≤ 60 before the length check, and add one codec test for it.
- **Decision**: FIXED: DecodeCells requires 1 to 60 for width and height before the length check; new theory Sizes_outside_the_map_cap_are_rejected (includes the 65536x65536 overflow).

### F5 — The 413 test only covers the Content-Length branch

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: api.Tests/SavedMapEndpointTests.cs (`A_body_over_16_kb_is_refused_on_save_rename_and_generate`)
- **Detail**: `StringContent` always sends a Content-Length, so the streaming cap in `RequestBody.ReadAsync` (the path a chunked body takes) is untested. The reviewer's throwaway test showed that it works.
- **Fix**: Add a chunked case using `StreamContent` over a non-seekable stream (16 KB + 1 byte gives 413 and stores nothing).
- **Decision**: FIXED: the 413 test adds a chunked Save (StreamContent over a non-seekable stream, no Content-Length) and still stores nothing.

### F6 — Titles accept invisible format and bidi characters

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapModels.cs:43-46
- **Detail**: `char.IsControl` doesn't cover format characters (U+200B zero-width space, U+202E right-to-left override) or the U+2028/U+2029 separators, so these get through. The risk is low: a user only ever sees their own titles, and React renders them as text.
- **Fix**: Also reject `UnicodeCategory.Format`, `LineSeparator` and `ParagraphSeparator` under the same `title` message, and add the cases to the title test.
- **Decision**: FIXED: titles also refuse Format, LineSeparator and ParagraphSeparator characters; the title test adds U+200B, U+202E and U+2028. Side effect: emoji sequences joined by U+200D (ZWJ) are refused too.
