<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Seed determinism (risk #2)

- **Plan**: context/changes/testing-seed-determinism/plan.md
- **Scope**: Phase 3 of 5
- **Reviewed phases**: 3
- **Date**: 2026-10-10
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Comment cites a plan section number instead of behavioral rationale

- **Severity**: 👁 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/MapEndpointTests.cs:407
- **Detail**: The comment `// Manual verification (plan §3.4): the echoed seed is a plain JSON number in uint range.` cites a planning-document section number. Every other comment in this file explains behavioral/business rationale (rate limiting, DB unreachability, etc.) without referencing plan/doc sections — this one leaks a planning artifact into the test file, which will read oddly once the plan is archived.
- **Fix**: Reword to describe the behavior/rationale only, e.g. "the echoed seed is a plain JSON number in uint range, so it can be resent as-is."
- **Decision**: FIXED — reworded the comment to drop the plan-section citation. Verified: `dotnet build api.Tests` clean, `dotnet test api.Tests --no-build --filter "FullyQualifiedName~MapEndpointTests"` → 36/36 passed.

## Notes

- Plan-drift sub-agent verdict: MATCH. Both facts implement the plan's contract exactly — fact 1 compares `cells`/`rooms`/`parameters` via `JsonElement.DeepEquals` for two identical requests; fact 2 compares `cells`/`rooms` after resending the echoed seed, and automatically asserts the echoed `seed` is a JSON number parsing as `uint` (stronger than the plan's "inspected once by hand"). Each fact uses one `ApiFactory` + one logged-in client and 2 HTTP requests, well under the 4-request/10-per-minute budget. `git diff --stat` against `api/` is empty — no production code touched. No scope creep: only the two named facts were added.
- Safety/pattern sub-agent: no reliability, rate-limiting, or resource-leak issues. `JsonElement.DeepEquals` for structural JSON comparison is a good fit and arguably stronger than prior patterns in the file (observation only, not a defect). `HttpResponseMessage` instances aren't disposed, but that matches every other test in this file — not a deviation introduced here.
- Success criteria verified directly: `dotnet test api.Tests --filter "FullyQualifiedName~MapEndpointTests"` → 36/36 passed (includes the rate-limit fact, 3.1/3.2). 3.3 (wrong echoed seed fails the fact) and 3.4 (echoed seed is a uint-range JSON number) were verified by the implementer before this review per `change.md`'s Phase 3 notes and plan.md's checked boxes; both have observable evidence in the diff (an automated `Assert.Equal(JsonValueKind.Number, ...)` + `GetUInt32()` check for 3.4, not rubber-stamped).
