<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Abuse-Resistant Quota

- **Plan**: context/changes/testing-abuse-resistant-quota/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2
- **Date**: 2026-10-17
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Success criteria re-run during review: `dotnet build api` 0 errors; `dotnet test api.Tests` 164/164 passed (`AbuseResistantQuotaTests` 2/2). In Phase 2's break-check, both new tests went red when the global limiters were unchained in Program.cs. Phase 1's review findings (impl-review-phase-1.md) were all fixed in 92eb830 and still hold.

Cross-phase notes:
- Phase 2 met the plan's forwarded-headers verification without the plan's fallback. The test host sets `ForwardedHeaders_Enabled`, the key behind production's `ASPNETCORE_FORWARDEDHEADERS_ENABLED`, and this was verified with a throwaway probe. `api/Program.cs` is untouched, which avoids the fallback's production risk (default KnownProxies would ignore App Service's XFF).
- Header-less test clients keep the shared "unknown" bucket, so no existing test changed.
- The fixed windows start when the limiters are created (app start, or a key's first request), not on wall-clock minutes, and each test takes about 2 s. Mid-test window rollover is therefore not a practical flake risk.

## Findings

### F1 — test-plan.md still shows Phase 3 as "not started" and §6.3 as TBD

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: context/foundation/test-plan.md:85, :161-163
- **Detail**: Rollout rows 1 and 2 were set to `complete` with their change folder, and §6.2 was written up as a how-to when those changes closed. Row 3 still reads `not started | —`, and §6.3 "Adding a rate-limit ceiling test" is still TBD. The test-plan is the cookbook the next test change reads.
- **Fix**: Set row 3 to `complete | context/changes/testing-abuse-resistant-quota/`, and write §6.3 in §6.2's shape: reference tests, run command, the mechanism (`CreateClientFromIp` + `ForwardedHeaders_Enabled`, "unknown" bucket for header-less clients), how to add a ceiling test, and what is out of scope.
- **Decision**: FIXED — row 3 set to complete with its change folder; §6.3 written in §6.2's shape

### F2 — api/AGENTS.md doesn't mention the simulated-IP test seam

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/AGENTS.md:24
- **Detail**: AGENTS.md documents `CreateLoggedInClientAsync` and its auth-permit cost, but not `ApiFactory.CreateClientFromIp`. It also doesn't say that the test host now honours `X-Forwarded-For`, so a header-less client lands in the shared "unknown" auth bucket.
- **Fix**: Add one sentence after the `CreateLoggedInClientAsync` line.
- **Decision**: FIXED — sentence added to api/AGENTS.md after the CreateLoggedInClientAsync line

### F3 — The per-key limit 10 is a literal in Program.cs and again in the test

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/AbuseResistantQuotaTests.cs:21, api/Program.cs (both per-key `PermitLimit = 10`)
- **Detail**: The global caps have named constants, but the per-key 10 doesn't, so the test mirrors it as `PerCallerLimit = 10`. If Program.cs changes the 10, the tests fail loudly, so they don't silently pass. The cost is only that a deliberate change has to touch two places.
- **Fix**: Optional. Add `PerAccountGenerateLimit` / `PerIpAuthLimit` constants next to the global ones and use them in Program.cs and the test, or accept the literal.
- **Decision**: FIXED — MapEndpoints.PerAccountGenerateLimit and AuthEndpoints.PerIpAuthLimit added and used in Program.cs and AbuseResistantQuotaTests (existing 'eleventh' tests keep their literals unmodified)
