<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Cloud Account Store and Persistent Sessions

- **Plan**: context/changes/account-store-foundation/plan.md
- **Scope**: Phase 3 of 4
- **Reviewed phases**: 3
- **Date**: 2026-09-25
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 1 observation

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Evidence (read-only `az` and `gh` commands only; no secret values were read):

- **Plan adherence:** Steps 1, 2 and 4 match the contract.
  - Server: Entra-only, with the owner (sid `838c576a-…`) as admin.
  - Database: `useFreeLimit=true`, `AutoPause`, `autoPauseDelay=60`, `GP_S_Gen5` with 2 vCores, in `polandcentral`, the same region as the app.
  - Firewall: only `AllowAzureServices`.
  - `AppDb` is `SQLAzure`, has no password, and matches the contract exactly.
  - 3 repository variables are set, and `HEALTH_READY_KEY` is a `production` environment secret. There are no secrets at repository level.
- **Step 3 on the Azure side matches.**
  - The custom role has exactly the 4 actions, assignable at `rg-battlemap`, and is assigned on the SQL server only.
  - The app identity has no role assignments.
  - `Website Contributor` on the deploy identity predates this phase (`plan.md:13`).
- **Adaptation:** the `Microsoft.Sql` provider had to be registered first. It is recorded in `change.md`.
- **Safety:**
  - TLS 1.2 minimum.
  - The deploy identity has no ARM permission to delete the server or the database.
  - The OIDC subject uses the immutable-ID form, scoped to `environment:production` (lesson).
  - The app is HTTPS-only with FTPS disabled.
  - The resource group holds nothing billable beyond the F1 plan and the free database.
  - Auditing and threat protection are off. That's accepted for the MVP; they would need spend on storage or Defender.
- **Phase 4 readiness:** the deploy identity holds `firewallRules/write` on the server, so there is no blocker.

## Findings

### F1 — AllowAzureServices admits every Azure tenant; the risk isn't written down

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: sql-battlemap-2a28f4 firewall rule `AllowAzureServices`; context/foundation/infrastructure.md (risk register); plan.md:39
- **Detail**: The 0.0.0.0 rule allows network connections from Azure-hosted resources in *any* tenant, not just this subscription. The design choice itself is documented ("No VNet/private endpoint", `plan.md:39`). The any-tenant scope, and why it's acceptable, appears in neither `infrastructure.md` nor the plan. In practice the risk is low: authentication is Entra-only with no SQL password, the database users have least privilege, and a VNet or private endpoint isn't available on F1 without extra cost.
- **Fix**: Add a risk-register row to `infrastructure.md`: "AllowAzureServices admits any Azure tenant's traffic — L/M — accepted: Entra-only auth, least-privilege DB users, no VNet on F1; revisit with B1/VNet integration."
- **Decision**: FIXED — added an accepted-risk row to the `infrastructure.md` risk register (Entra-only, least-privilege DB users, no VNet on F1; revisit with B1).

### F2 — Check 3.7 proves the database users exist, not their roles

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: plan.md:264 (3.7); sqldb-battlemap
- **Detail**: The pasted output shows both users as `EXTERNAL_USER`. Their memberships (`db_datareader`/`db_datawriter` for the app, plus `db_ddladmin` for the deploy identity) can't be read without SQL access. Phase 4 will exercise them anyway: the migration needs `db_ddladmin`, and `/ready` writes the first key.
- **Fix**: In the Query editor, run `SELECT m.name AS member, r.name AS role FROM sys.database_role_members rm JOIN sys.database_principals r ON r.principal_id = rm.role_principal_id JOIN sys.database_principals m ON m.principal_id = rm.member_principal_id WHERE m.type = 'E';` and expect 5 rows.
- **Decision**: FIXED — the owner ran the role-membership query on 2026-09-25 and got exactly the 5 expected rows (app: db_datareader, db_datawriter; deploy: db_datareader, db_datawriter, db_ddladmin). The temporary `owner-temp` firewall rule was added for the query and removed afterwards; only `AllowAzureServices` remains.
