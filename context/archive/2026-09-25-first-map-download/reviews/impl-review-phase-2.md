<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First Map Download (Phase 2 follow-up commits)

- **Plan**: context/changes/first-map-download/plan.md
- **Scope**: Phase 2 of 4, the commits that landed after `impl-review.md`: eabfaa4 (namespace rename), ec018cf (project, assembly and contract rename + deploy `startup-command`), a6819dc (deterministic tie-break in the corridor search, fixtures regenerated)
- **Reviewed phases**: 2
- **Date**: 2026-09-28
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Verified: 39/39 API tests (re-run by the drift reviewer), PR #3 CI green, deploy run 36447114747 green with both smoke tests, App Service `appCommandLine` = `dotnet BattleMapGenerator.Api.dll`, production seed 42 = fixture, rate limit 429 on the 11th call, including with spoofed `X-Forwarded-For`. Tie-break key: no overflow (max cost about 129,600), unique keys, total order; no other nondeterminism in BspGenerator. Migration IDs, Data Protection application name and the WebApplicationFactory content root are unaffected by the rename.

## Findings

### F1 — Rolling back to a build from before the rename won't start

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: context/foundation/infrastructure.md:157; .github/workflows/deploy.yml:159
- **Detail**: `startup-command` persists as the App Service `appCommandLine`. Redeploying any build older than ec018cf ships `battle-map-generator-api.dll` while the setting says `BattleMapGenerator.Api.dll`, and the old deploy.yml doesn't reset it, so the app fails to start.
- **Fix**: Add the rollback caveat: rolling back past ec018cf also needs `az webapp config set --startup-file "dotnet battle-map-generator-api.dll"`.
- **Decision**: FIXED (rollback caveat in infrastructure.md)

### F2 — Two docs still describe the old name as current

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: context/changes/deployment/deployment-plan.md:73; context/foundation/tech-stack-api.md:4
- **Detail**: The runbook still sets `--startup-file "dotnet battle-map-generator-api.dll"` by hand; the tech-stack hand-off says `project_name: battle-map-generator-api`.
- **Fix**: Add a "superseded by deploy.yml startup-command" note to the runbook; set `project_name: BattleMapGenerator.Api`.
- **Decision**: FIXED (runbook note, project_name)

### F3 — The tie-break rule lives only in one code comment

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architecture
- **Location**: api/Maps/BspGenerator.cs:279-283
- **Detail**: Future seeded code (S-03, web sprite variants) can depend on a library's order for equal keys the same way.
- **Fix**: Record a lesson: seeded code never relies on a library's order for equal keys; break ties explicitly.
- **Decision**: ACCEPTED-AS-RULE: Seeded code breaks ties explicitly, never by a library's ordering (code fix already in a6819dc)

### F4 — Stale build output from the old name on the developer machine

- **Severity**: OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/bin, api/obj, api.Tests/bin, api.Tests/obj (ignored, local only)
- **Detail**: Old `battle-map-generator-api.*` artifacts remain locally; CI is unaffected because it builds from a clean checkout.
- **Fix**: `rm -rf api/bin api/obj api.Tests/bin api.Tests/obj` once (owner, locally).
- **Decision**: SKIPPED (harmless, local only)
