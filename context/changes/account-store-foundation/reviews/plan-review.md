<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Cloud Account Store and Persistent Sessions

- **Plan**: context/changes/account-store-foundation/plan.md
- **Mode**: Deep
- **Date**: 2026-09-23
- **Verdict**: REVISE (after fixes: SOUND)
- **Findings**: 0 critical, 3 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | WARNING |
| Lean Execution | PASS |
| Architectural Fitness | WARNING |
| Blind Spots | WARNING |
| Plan Completeness | WARNING |

## Grounding
5/5 paths ✓, 4/4 symbols ✓ (Program.cs:38/42/43, deploy.yml:78), brief↔plan ✓, Progress 24/24 rows ✓. Verified: SQL Server Contributor = `Microsoft.Sql/servers/*` (includes firewallRules); EF Core SqlServer 10.0.12 → Microsoft.Data.SqlClient 6.1.6; `az sql db create --help` free-offer example uses serverless flags. Codebase verification done inline (repo is a small skeleton).

## Findings

### F1 — Deploy identity could delete the database

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Architectural Fitness
- **Location**: Phase 3 §3 — role assignment
- **Detail**: SQL Server Contributor grants `Microsoft.Sql/servers/*`, so the CI deploy identity could delete the server or database; dropping a database is human-only per AGENTS.md.
- **Fix A ⭐ Recommended**: Custom role with only `servers/read` + `servers/firewallRules/{read,write,delete}`
  - Strength: CI can open and close its rule and nothing else.
  - Tradeoff: One extra `az role definition create` for the owner.
  - Confidence: HIGH — those are the only actions firewall-rule create/delete use.
  - Blind spot: None significant.
- **Fix B**: Keep SQL Server Contributor and record the risk
  - Strength: Built-in role.
  - Tradeoff: The deploy identity can drop the database.
  - Confidence: HIGH
  - Blind spot: None significant.
- **Decision**: FIXED (Fix A)

### F2 — The free-database create command lacks serverless flags

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Phase 3 §1
- **Detail**: The CLI's free-offer example uses `-e GeneralPurpose -f Gen5 -c 2 --compute-model Serverless`; the plan omitted them, risking a rejected or billed provisioned database.
- **Fix**: Use the full flag set plus `--auto-pause-delay 60`.
- **Decision**: FIXED

### F3 — The idle test doesn't prove the database resumed from pause

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: End-State Alignment
- **Location**: Phase 4, Manual 4.7
- **Detail**: The app unloads after 20 min, but the database pauses only after its 60-minute delay, so the 40613 resume path was usually not exercised.
- **Fix**: Wait more than 60 minutes, require `az sql db show --query status` = `Paused` before calling `/ready`, and record the time taken (row 4.7 and its phase bullet updated together, pre-implementation).
- **Decision**: FIXED

### F4 — A public /ready can keep the database awake

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Performance Considerations / brief Open Risks
- **Detail**: Anything calling `/ready` more often than the pause delay keeps the DB awake and burns the free vCore-seconds; AutoPause then takes it offline until the 1st.
- **Fix**: Record the risk and add a free-limit usage check.
- **Decision**: FIXED (differently) — the user asked who the probes are for. F1 cannot restrict single paths by network, so `/ready` is now key-gated (`X-Health-Key` against `Health:ReadyKey`, 404 otherwise, fail closed); the key is an App Service setting + GitHub environment secret `HEALTH_READY_KEY`; `/live` stays public. The risk and a post-launch usage check are also recorded.

### F5 — Data Protection keys are stored unencrypted

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 1 §3 / What We're NOT Doing
- **Detail**: Without `ProtectKeysWith*` the key XML is plain text and startup logs "No XML encryptor configured".
- **Fix**: State in What We're NOT Doing that this is accepted (Entra-only access is the boundary) and the warning is expected.
- **Decision**: FIXED

### F6 — Upgrading SqlClient to 7.x would break Entra auth

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: Critical Implementation Details
- **Detail**: EF Core 10 brings SqlClient 6.1.6 with Entra auth built in; SqlClient 7.x moved it to `Microsoft.Data.SqlClient.Extensions.Azure`.
- **Fix**: Note it in Critical Implementation Details.
- **Decision**: FIXED
