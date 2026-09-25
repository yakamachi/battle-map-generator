<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Cloud Account Store and Persistent Sessions

- **Plan**: context/changes/account-store-foundation/plan.md
- **Scope**: Phase 2 of 4
- **Reviewed phases**: 2
- **Date**: 2026-09-25
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 2 warnings, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Evidence:

- **Plan adherence:** all 4 planned items and every contract bullet match, and each one is really asserted.
  - "No pending migrations" is asserted at `AccountStoreTests.cs:22`.
  - The restart simulation disposes the first factory before building the second (`:35-42`).
  - "No check runs" on a 404 is proven by a key count of 0 (`HealthProbeTests.cs:84`).
- **Adaptations accepted:** `ApiFactory.cs` as a separate file; a fresh database for each test that counts keys; the `Testing` environment; xUnit v2; one extra 404 case.
- **Success criteria:**
  - `dotnet test api.Tests`: 13/13 passed (re-run during this review).
  - No `bin/` or `obj/` is tracked.
  - Manual check 2.3 is backed by CI run 36130320959 (`Test API` green before `Publish API`, then the smoke test green). The follow-up docs push (run 36131041768) was green too.

## Findings

### F1 — The /api 404 test cannot catch its regression in CI

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api.Tests/HealthProbeTests.cs:87-96 (`Removed_and_unknown_api_paths_return_404`); api/Program.cs:103; .github/workflows/deploy.yml:41-47
- **Detail**: The test is meant to pin the lesson "Unknown /api routes return 404, never the SPA shell". It asserts only `404`. In CI, `Test API` runs before `Copy SPA into API wwwroot`, so the test host has no `index.html`, and the SPA fallback also answers 404. Verified during this review by removing `MapFallback("/api/{**rest}", …)`:
  - With the local `wwwroot` (which has `index.html`), 2 of 2 fail, as they should.
  - With `ASPNETCORE_WEBROOT` pointing at an empty directory (CI conditions), 2 of 2 **pass**.

  So the regression reaches production, where `wwwroot` is filled and the SPA shell would answer `200 text/html`. Phase 2's deliberate-break check didn't cover this line.
- **Fix**: Give the test host its own web root holding a stub `index.html`, for example a temp directory set through `builder.UseWebRoot(...)` in `ApiFactory`. Then add a deep-link case (`/some/page` → 200 with the stub's content) next to the `/api` 404 cases, so the test checks both halves of the lesson's rule whatever CI's `wwwroot` holds.
- **Decision**: FIXED — `ApiFactory` serves its own web root with a stub `index.html` (deleted on dispose), and a new deep-link test checks that the SPA shell answers 200. Verified: with the `/api` guard removed and an empty real web root, both `/api` cases fail; the suite passes 14/14.

### F2 — No timeout on the build job, which now pulls a 2.3 GB image on every run

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/deploy.yml:17-18 (build job), :41-42 (Test API)
- **Detail**: Testcontainers pulls `mcr.microsoft.com/mssql/server:2022-latest` (2.34 GB) on every run of a fresh `ubuntu-latest` runner. Neither the job nor the step sets `timeout-minutes`, so GitHub's 360-minute default applies. Because of `concurrency: deploy-production` with `cancel-in-progress: false` (`:12-14`), a stalled pull or a hung test would hold every later push to `main` in the queue. The runs so far took 3m00s and 1m59s end to end.
- **Fix**: Add `timeout-minutes: 20` to the `build` job.
- **Decision**: FIXED — `timeout-minutes: 20` on the `build` job, with a comment giving the reason.

### F3 — id-token: write is granted to the build job too

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/deploy.yml:8-10
- **Detail**: `permissions: id-token: write` is set for the whole workflow. Only the `deploy` job's `azure/login` needs it. The `build` job now runs third-party test dependencies (NuGet packages and a container) with the right to mint an OIDC token. The setting predates Phase 2, but Phase 2 raised its exposure.
- **Fix**: Keep `contents: read` at workflow level and grant `id-token: write` only on the `deploy` job.
- **Decision**: FIXED — the workflow level keeps only `contents: read`; the `deploy` job grants `id-token: write` and `contents: read`. It will be proven by the next deploy run (azure/login must still succeed).
