<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Phase 1 of 4
- **Reviewed phases**: 1
- **Date**: 2026-10-17
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 5 observations

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
- **Plan drift:** every planned change in commit 3e9afdf is a MATCH. The two EXTRA items are benign: the test `Save_without_a_seed_or_with_a_body_that_is_not_an_object_is_refused`, and the test-clock support (`ManualTimeProvider`, `ApiFactory.Time`).
- **Out-of-scope list:** nothing on "What We're NOT Doing" was violated.
- **Generate refactor:** generate's errors stay byte-identical after moving the helpers into `RequestBody.cs`.
- **Verified clean:**
  - IDOR: every query filters by owner, and every route has `RequireAuthorization`.
  - CSRF: form, text, multipart and a missing Content-Type all get 404 (checked with a throwaway app).
  - Database touch: `AppDbContext` is a handler parameter, resolved only after authorization.
  - The migration only adds things.
  - The `uint`↔`bigint` seed conversion can't overflow.
- **Success criteria:**
  - 1.1 `dotnet test api.Tests` passed 189/189.
  - 1.2 Contract drift check: PASS.
  - 1.3 Web typecheck: PASS.
  - 1.4 Deliberate break went red on the isolation test.
  - 1.5 Manual check confirmed by the user.

## Findings

### F1 — The list loads and parses every map's full grid to read its parameters

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapEndpoints.cs:73-85
- **Detail**:
  - `List` reads `MapJson` (nvarchar(max)) for every row and deserializes the whole `GeneratedMap`, only to use `.Parameters`. That is up to 100 rows of ~10–25 KB stored per call, and the cookie gate allows 60 calls per minute per IP.
  - One unreadable row (corrupted, or an old row after a future enum change) turns the whole list into a 500.
  - The plan's Performance Considerations said "the list reads summary columns only, never `MapJson`", but its table had no parameter columns. That is a gap in the plan.
- **Fix A ⭐ Recommended**: Add `RoomCount`, `Encounter` and `BossSize` columns, filled on Save, and project the list with `.Select(...)` so `MapJson` is never read.
  - Strength: Matches the plan's stated intent. The list becomes a narrow index-backed query, and one bad row can't break it. The migration hasn't been merged or deployed, so it can be regenerated in place.
  - Tradeoff: Three more columns, a regenerated migration, and the parameters stored twice (columns plus JSON).
  - Confidence: HIGH — the migration exists only on this branch, so changing it costs nothing.
  - Blind spot: How the enums are stored (as strings or ints) needs choosing. Strings via EF value conversion would match the JSON.
- **Fix B**: Keep the row read, but project only `MapJson` plus the summary columns, and skip and log a row that fails to deserialize.
  - Strength: No schema change.
  - Tradeoff: Still reads every grid on each list call.
  - Confidence: MED.
  - Blind spot: The cost of reading every grid on the free plan's database hasn't been measured.
- **Decision**: FIXED (differently): user ruled out JSON in SQL; new Phase 2 "Compact storage" stores parameters as columns and cells/rooms as bytes; the list projects summary columns only. Wire format stays JSON (bytes on the wire recorded as a 10xArchitect candidate).

### F2 — Save runs the generator outside the generate limits, reopening the gap the global ceiling closed

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapEndpoints.cs:137; api/AGENTS.md (Saved maps section)
- **Detail**:
  - Save calls `BspGenerator.Generate` but carries no `generate` policy, so neither the 10/min per-account limit nor the 50/min global ceiling (test-plan Phase 3) applies to it. The only bound is SessionCookieGate's 60/min per IP.
  - A save/delete loop regenerates maps without any limit across accounts or IPs.
  - The plan chose "no new rate-limit policy, Save spends no generate permit" on purpose, to keep the e2e budget intact. But its "What We're NOT Doing" list doesn't record this side effect, and the infrastructure docs don't either.
  - One generation takes well under 1 ms, so the CPU risk is small.
- **Fix A ⭐ Recommended**: Keep the behaviour, and record the residual in the infrastructure.md risk register in Phase 4 (unbounded save regeneration, small per-call cost, revisit with test-plan Phase 4). Add one sentence to the plan's "NOT doing" list.
  - Strength: Respects the decision already taken ("cap only") and costs nothing before 2026-11-04.
  - Tradeoff: The aggregate-CPU gap stays open, though it's documented.
  - Confidence: HIGH that a generation is cheap (the earlier measurement was 1000 generations in under 1 s, locally); MED for the F1 host.
  - Blind spot: CPU per generation hasn't been measured on F1 (that is test-plan Phase 4).
- **Fix B**: Give Save its own `save` policy that chains only the shared global generate limiter (50/min across all accounts), with no per-account permit.
  - Strength: Restores the aggregate ceiling without touching the e2e generate budget per account.
  - Tradeoff: More policy plumbing and a new ceiling test, and saves and generates now compete for one global bucket.
  - Confidence: MED — the global limiter is captured in a closure in Program.cs and would need sharing across two policies.
  - Blind spot: How the shared bucket interacts with the abuse-resistant quota tests.
- **Decision**: ACCEPTED via Fix A: Save keeps running outside the generate limits; recorded in the plan's not-doing list, api/AGENTS.md (Phase 2) and the infrastructure.md risk register (Phase 5).

### F3 — The planned storage-size numbers ignore nvarchar's UTF-16 doubling

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Data/SavedMap.cs (MapJson nvarchar(max)); plan.md Phase 4 §3
- **Detail**:
  - `MapJson` is stored as `nvarchar` (UTF-16), so a ~4.8–12 KB JSON map takes ~10–25 KB on disk.
  - The plan's Phase 4 wording ("~5–12 KB each") understates it.
  - The storage growth residual from many scripted accounts × 100 maps is already planned for the infrastructure risk register. It needs the corrected numbers.
- **Fix**: When writing the Phase 4 infrastructure.md row, use the stored size (~10–25 KB/map, ~1–2.5 MB per account at the cap), and state that registration (50/min globally) bounds how fast accounts can be created, not total storage.
- **Decision**: FIXED (differently): moot once Phase 2 removes the JSON column; Phase 5 infrastructure numbers updated to ~0.7–2 KB per map.

### F4 — Rename races with a concurrent delete and returns 500

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapEndpoints.cs:180-188
- **Detail**: Rename loads a tracked row and then calls `SaveChanges`. If a delete lands in between, EF throws `DbUpdateConcurrencyException`, which becomes a 500 rather than a 404.
- **Fix**: Catch `DbUpdateConcurrencyException` around `SaveChangesAsync` in Rename and return `NotFound()`.
- **Decision**: FIXED: scheduled in Phase 2 (Rename catches DbUpdateConcurrencyException → 404).

### F5 — The CSRF test doesn't cover a request with no Content-Type

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: api.Tests/SavedMapEndpointTests.cs:321-347
- **Detail**: A cross-site `fetch(..., {mode: 'no-cors', body: blob})` can send a body with no Content-Type and no preflight. Today that gets 404 (verified with a throwaway app), but nothing pins it, and the comment at `MapEndpointTests.cs:51-52` claims it without a test.
- **Fix**: Add a no-Content-Type case (POST and PATCH with a JSON payload in a `ByteArrayContent` with no header) to `Form_and_text_bodies_are_refused_on_save_and_rename`.
- **Decision**: FIXED: scheduled in Phase 2 (no-Content-Type case added to the CSRF test).

### F6 — Request bodies are buffered without a small size cap

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/RequestBody.cs:16-17
- **Detail**: The body is copied into a `MemoryStream` with no limit of its own. Kestrel's 30 MB default still applies (a 31 MB body was verified to get 413), but a library request can buffer ~30 MB, while real bodies are under 1 KB. The same was true of generate before this change.
- **Fix**: Put `.WithMetadata(new RequestSizeLimitAttribute(16 * 1024))` on generate, Save and Rename (or cap inside `RequestBody.ReadAsync`).
- **Decision**: FIXED: scheduled in Phase 2 (16 KB request size limit on generate, Save and Rename, verified end to end).

### F7 — The test clock override also freezes the framework's clock

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/Infrastructure/ApiFactory.cs:50-53
- **Detail**: Replacing the DI `TimeProvider` also changes the clock that cookie authentication, Identity lockout and the security stamp use. The current tests are fine because everything runs on the same frozen clock, but the coupling is easy to miss.
- **Fix**: Add one sentence to the `Time` property comment saying the override also drives the cookie, lockout and security-stamp clocks.
- **Decision**: FIXED: scheduled in Phase 2 (ApiFactory.Time comment).

### F8 — The title accepts control and invisible characters

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Maps/SavedMapModels.cs:42-45
- **Detail**: The title is only trimmed and length-checked, so newlines, control characters and zero-width or bidi characters get through. It's safe as long as `web/` renders titles as text, which React does, but a list could show odd or spoofed titles.
- **Fix**: Reject titles containing control characters (`char.IsControl`) under the same `title` message.
- **Decision**: FIXED: scheduled in Phase 2 (control characters rejected in titles).
