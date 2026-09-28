<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First Map Download

- **Plan**: context/changes/first-map-download/plan.md
- **Scope**: Phases 1–2 of 4
- **Reviewed phases**: 1, 2
- **Date**: 2026-09-28
- **Verdict**: APPROVED
- **Findings**: 0 critical, 2 warnings, 1 observation

Note: the drift and safety passes were done in the main session by reading the files, not by sub-agents, because an auto-mode safety check outage blocked sub-agents and shell commands. Automated criteria come from earlier runs in the same session (Phase 1 at 5c4c76f; Phase 2 on the staged tree committed as 58d4ce6), not a re-run.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — PR CI doesn't run the deploy's build steps

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/ci.yml:19-34
- **Detail**: The `api` job runs only `dotnet test`. The migration bundle and `dotnet publish` first run in deploy.yml after the merge, which is how the missing restore broke the deploy after PR #1. Phase 2 makes every build run Program.cs for the OpenAPI file, including the ef bundle build, and nothing tests that path before merge.
- **Fix**: Add a step to the `api` job after tests: `dotnet tool restore`, `dotnet restore api`, `dotnet ef migrations bundle …` and `dotnet publish api -c Release -o publish`, with nothing uploaded. Put it on the Phase 2 branch so PR #3 exercises it.
  - Strength: "Merged means tested" then covers the deploy's build.
  - Tradeoff: About 1 min more CI per PR.
  - Confidence: HIGH — the same commands already run in deploy.yml.
  - Blind spot: Azure-side steps still run only on merge.
- **Decision**: FIXED (ci.yml `api` job: bundle + publish step)

### F2 — X-Forwarded-For spoofing of the rate limit is unverified

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:22-43; plan Progress 2.7
- **Detail**: ASPNETCORE_FORWARDEDHEADERS_ENABLED clears KnownProxies and KnownNetworks, so the rightmost X-Forwarded-For entry is trusted. If App Service's front end passes a client-supplied value through, each request can pick a fresh bucket. Check 2.7 doesn't test this.
- **Fix**: Extend 2.7: send 11 calls, each with a different `X-Forwarded-For: 203.0.113.<i>` header; the 11th must still get 429. If it gets 200, partition on App Service's own client-IP header instead.
- **Decision**: FIXED (2.7 extended in the plan; the check runs after the Phase 2 deploy)

### F3 — api/CLAUDE.md still says CI tests "before publishing"

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: api/CLAUDE.md:7
- **Detail**: Phase 1 moved tests to ci.yml on pull requests; the root docs were updated, but this file was missed.
- **Fix**: "CI (`ci.yml`) runs it on every pull request; deploys don't."
- **Decision**: FIXED
