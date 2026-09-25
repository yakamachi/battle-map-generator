# Cloud Account Store and Persistent Sessions Implementation Plan

## Overview

Roadmap item F-01. Give the API a cloud account store: EF Core on the Azure SQL free offer, holding the ASP.NET Core Identity schema and the Data Protection key ring. The connection survives the database waking from auto-pause, and a restart or idle unload of the app does not replace the key ring (which would log users out once S-04 adds login). Migrations run from CI as an EF bundle under the existing deploy identity. The app reaches the database with its own managed identity, so there is no SQL password anywhere. Kubernetes-style `live` and `ready` probes expose the app's health and its database health separately. No login endpoints: those belong to S-04 (`dm-email-login`).

## Current State Analysis

- `api/Program.cs` is a skeleton: OpenAPI (Development only), static SPA files, `GET /api/health` (`api/Program.cs:38`), the `/api/{**rest}` 404 guard (`:42`) and the SPA fallback (`:43`). No EF Core, no Identity, no Data Protection configuration, no connection string (`api/appsettings*.json`).
- `api/battle-map-generator-api.csproj` references only `Microsoft.AspNetCore.OpenApi` 10.0.12; SDK pinned by `global.json` (10.0.100, `latestFeature`; local 10.0.112).
- There is no test project and no test step in CI.
- `.github/workflows/deploy.yml`: `build` job (web typecheck/build, copy SPA into `api/wwwroot`, `dotnet publish`) → `deploy` job (`environment: production`, `azure/login@v2` OIDC with `vars.AZURE_*`, `azure/webapps-deploy@v3`, smoke test `curl … /api/health` at `:78`, 10 retries × 15 s).
- Azure (`context/changes/deployment/deployment-plan.md`, "Wynik"): `rg-battlemap` in **Poland Central**, plan `plan-battlemap` (F1 Linux), app `battle-map-generator`, user-assigned identity `id-battlemap-deploy` with Website Contributor on the app only. OIDC subject uses the immutable-ID form (lesson "Take the GitHub OIDC subject from GitHub"). `az sql server list` is empty: no database exists.
- Local: Docker is available (`/usr/bin/docker`); `sqlcmd` is not installed.
- Known traps (`context/foundation/infrastructure.md` risk register): Data Protection keys are not persisted on .NET 10 Linux App Service (H/H); Azure SQL free offer fails the first connection after auto-pause with error 40613 (H/L); the free database must be in the same region as the app; "continue with charges" is irreversible and human-only.

## Desired End State

- The deployed app on `battle-map-generator.azurewebsites.net` connects to an Azure SQL free-offer database in Poland Central (auto-pause at limit) using its system-assigned managed identity. The database holds the Identity tables and `DataProtectionKeys`, created by the initial migration.
- Every push to `main` runs integration tests, applies pending migrations through an EF bundle as `id-battlemap-deploy` (temporary firewall rule for the runner, always removed), deploys, and smoke-tests `/api/health/live` and `/api/health/ready`.
- `GET /api/health/live` → 200 without touching the database (public). `GET /api/health/ready` requires the `X-Health-Key` header (404 without a matching key, so strangers cannot wake the database); with the key → 200 only when the database answers and a Data Protection round trip succeeds, otherwise 503.
- After `az webapp restart` and after an idle unload, `DataProtectionKeys` still holds the same single key and `/api/health/ready` returns 200: no new key ring was generated.
- Verify with: `dotnet test api.Tests`, a green deploy run, the key-ring query in Phase 4.

### Key Discoveries:

- The `/api/{**rest}` 404 guard must stay before the SPA fallback (`api/Program.cs:42-47`, lesson "Unknown /api routes return 404"); the new probes live under `/api/health/*`.
- The smoke test path is hard-coded in `.github/workflows/deploy.yml:78` and `api/battle-map-generator-api.http:3`; both change with the probe rename.
- `api/.gitignore` ignores `[Bb]in/`/`[Oo]bj/` only inside `api/`; a sibling test project needs its own ignore entries.
- The deploy identity is user-assigned and already trusted through OIDC; after `azure/login`, `Authentication=Active Directory Default` in SqlClient picks up the Azure CLI credential on the runner, so no extra CI secret is needed.

## What We're NOT Doing

- No login, registration, logout, cookie authentication or account creation endpoints/UI (S-04; PRD Open Question #3 decides how accounts are created).
- No authorization on existing or future endpoints.
- No map persistence (maps are not stored in the MVP).
- No migration at app startup and no manual migration scripts; CI's bundle is the only production migration path.
- No SQL password: no SQL-auth logins in Azure, Entra-only server. (Local Docker uses SQL auth; that is dev-only.)
- No VNet/private endpoint; the app reaches SQL through "Allow Azure services".
- No health-check UI or detailed JSON health payloads.
- No encryption of Data Protection keys at rest: Entra-only database access is the boundary. The startup warning "No XML encryptor configured" is expected; do not add a certificate or Key Vault to silence it.
- No plan-tier change, no "continue with charges" (human-only per `AGENTS.md`).

## Implementation Approach

Build and prove everything locally first (Phase 1–2: code, Docker SQL Server, Testcontainers tests, CI test gate); each of these phases is safe to push because production only smoke-tests `/live` until Phase 4. Then the owner provisions Azure (Phase 3) using commands the plan prepares. Phase 4 wires the migration bundle into the deploy and proves persistence on the real deployment. Auth is split by environment: SQL auth to the local container, managed identity (`Active Directory Default`) in Azure; the code does not branch on it, only the connection string differs.

## Critical Implementation Details

- **Design-time vs. runtime connection string.** `dotnet ef migrations bundle` builds the app host in CI, where no connection string exists. Registration must tolerate a missing `ConnectionStrings:AppDb` at design time (the bundle gets it at run time through `--connection`), but the running app must fail readiness, not crash at startup, when it is missing.
- **Shared application name.** `SetApplicationName` must be a fixed string. Otherwise the key ring's purpose isolation depends on the content root path, and a key written by one deployment may not decrypt payloads in another (and the two-instance test would pass for the wrong reason).
- **Retry vs. readiness timeout.** `EnableRetryOnFailure` can hold a request for tens of seconds while the database resumes. That is intended in production (the smoke test retries for about 150 s). Tests that assert a 503 against an unreachable database should use a short `Connect Timeout` or a zero-retry override so the suite stays fast.
- **Firewall cleanup ordering.** The step that removes the runner's firewall rule must run with `if: always()` so a failed migration never leaves the server open to that IP.
- **SqlClient version.** Entra auth (`Active Directory Default`) comes built into `Microsoft.Data.SqlClient` 6.x, which EF Core SqlServer 10.0.x brings with it (6.1.6 for 10.0.12). SqlClient 7.x moved it to `Microsoft.Data.SqlClient.Extensions.Azure`. Do not pin SqlClient 7.x without that package, or production fails at connect with an unsupported-authentication error.
- **Ready key.** `/api/health/ready` fails closed: when `Health:ReadyKey` is not configured, or the `X-Health-Key` header does not match (constant-time comparison), it returns 404 without running any check.

## Phase 1: Local Account Store and Probes

### Overview

The API gets EF Core SQL Server, the Identity schema, database-backed Data Protection keys, the initial migration and live/ready probes, runnable against SQL Server in Docker.

### Changes Required:

#### 1. Packages and tools

**File**: `api/battle-map-generator-api.csproj`, `.config/dotnet-tools.json` (new)

**Intent**: Add the EF Core, Identity, Data Protection and health-check packages the store needs, and pin `dotnet-ef` as a local tool so CI and developers use the same version.

**Contract**: Package references at the 10.0.x line matching the runtime: `Microsoft.EntityFrameworkCore.SqlServer`, `Microsoft.EntityFrameworkCore.Design` (private assets), `Microsoft.AspNetCore.Identity.EntityFrameworkCore`, `Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`, `Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore`. Tool manifest with `dotnet-ef` 10.0.x.

#### 2. DbContext

**File**: `api/Data/AppDbContext.cs` (new)

**Intent**: One context for the account store: the Identity tables and the Data Protection key table.

**Contract**: `AppDbContext : IdentityDbContext<IdentityUser>, IDataProtectionKeyContext` with `DbSet<DataProtectionKey> DataProtectionKeys`. Default Identity schema (string keys).

#### 3. Service registration and probes

**File**: `api/Program.cs`

**Intent**: Register the store, Identity core services (no endpoints), key persistence and health checks; replace `/api/health` with `live` and `ready` probes while keeping the 404 guard and SPA fallback order.

**Contract**:
- `AddDbContext<AppDbContext>` with `UseSqlServer(<ConnectionStrings:AppDb>, o => o.EnableRetryOnFailure())`, tolerant of a missing connection string at design time (see Critical Implementation Details).
- `AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<AppDbContext>()`.
- `AddDataProtection().SetApplicationName("battle-map-generator").PersistKeysToDbContext<AppDbContext>()`.
- `AddHealthChecks()` with a `DbContextCheck<AppDbContext>` and a Data Protection check (protect then unprotect a fixed payload with a dedicated purpose; the first call writes the default key to the database), both tagged `ready`.
- `GET /api/health/live` → maps health checks with a predicate that runs no checks (always 200 while the process serves); public. `GET /api/health/ready` → only `ready`-tagged checks; 200 Healthy, 503 Unhealthy; gated by the `X-Health-Key` header against configuration `Health:ReadyKey` (404 otherwise, see Critical Implementation Details).
- `GET /api/health` is removed; unknown `/api/*` (including `/api/health`) returns 404.
- `public partial class Program` so tests can use `WebApplicationFactory<Program>`.

#### 4. Data Protection health check

**File**: `api/Health/DataProtectionHealthCheck.cs` (new)

**Intent**: Prove the key ring loads from, or is created in, the database and can round-trip a payload.

**Contract**: `IHealthCheck` using `IDataProtectionProvider`; Unhealthy (with the exception) when protect/unprotect throws.

#### 5. Initial migration

**File**: `api/Migrations/*` (generated)

**Intent**: Create the Identity tables and `DataProtectionKeys` in one additive initial migration.

**Contract**: Migration named `InitialAccountStore`, generated with `dotnet ef migrations add InitialAccountStore --project api`.

#### 6. Local SQL Server and configuration

**File**: `compose.yaml` (new, repo root), `api/appsettings.Development.json`

**Intent**: Run SQL Server locally in Docker so development matches Azure SQL (`api/AGENTS.md`), and point Development at it.

**Contract**: One `sqlserver` service (`mcr.microsoft.com/mssql/server`, 2022 or later, port 1433, `ACCEPT_EULA=Y`, a local-only SA password, named volume). `ConnectionStrings:AppDb` in `appsettings.Development.json` targeting `localhost,1433`, database `battlemap`, `TrustServerCertificate=True`, plus a local-only `Health:ReadyKey`. Both values are clearly local-only; no Azure credential is committed.

#### 7. Callers of the old health path

**File**: `.github/workflows/deploy.yml`, `api/battle-map-generator-api.http`

**Intent**: Keep the deploy smoke test green after the rename and update the request samples.

**Contract**: The smoke test curls `/api/health/live` (same retries). The `.http` file has `live` and `ready` requests (the latter with the local `X-Health-Key`).

### Success Criteria:

#### Automated Verification:

- API builds: `dotnet build api`
- Migration exists and generates a script: `dotnet tool restore && dotnet ef migrations script --project api` lists `AspNetUsers` and `DataProtectionKeys`
- Migration applies to local SQL Server: `docker compose up -d sqlserver && dotnet ef database update --project api` exits 0

#### Manual Verification:

- With the container up and `dotnet run --project api`: `/api/health/live` → 200, `/api/health/ready` with the local `X-Health-Key` → 200 and without it → 404, `/api/health` and `/api/nope` → 404, a deep link returns `index.html` (with `wwwroot` populated)
- With the container stopped: `/api/health/live` → 200, `/api/health/ready` with the key → 503 (after the retry window)
- After the first `ready` call, `SELECT Id, FriendlyName FROM DataProtectionKeys` in the container shows exactly one key; restarting the API and calling `ready` again leaves the same single key

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Integration Tests and CI Test Gate

### Overview

A test project exercises the store against real SQL Server through Testcontainers, and CI runs it before building the deploy artifact.

### Changes Required:

#### 1. Test project

**File**: `api.Tests/battle-map-generator-api.Tests.csproj` (new, sibling of `api/`), `api.Tests/.gitignore` (new)

**Intent**: A home for API tests (S-01 adds algorithm tests here too), outside `api/` so the Web SDK's default globbing does not compile test files into the app.

**Contract**: xUnit project on `net10.0` referencing `../api/battle-map-generator-api.csproj`, `Microsoft.AspNetCore.Mvc.Testing` and `Testcontainers.MsSql`. `.gitignore` covers `bin/`, `obj/`, `TestResults/`.

#### 2. Test fixture

**File**: `api.Tests/Infrastructure/SqlServerFixture.cs` (new)

**Intent**: One SQL Server container per test run, migrated once, shared by the store tests.

**Contract**: xUnit collection fixture that starts an `MsSqlContainer`, applies migrations through `AppDbContext.Database.MigrateAsync()`, and exposes a connection string. A `WebApplicationFactory<Program>` subclass takes the connection string and overrides `ConnectionStrings:AppDb`.

#### 3. Store and probe tests

**File**: `api.Tests/AccountStoreTests.cs`, `api.Tests/HealthProbeTests.cs` (new)

**Intent**: Pin the guarantees F-01 exists for.

**Contract**:
- Migrations apply to an empty database and leave no pending migrations.
- A payload protected by one factory instance is unprotected by a second, freshly built factory instance on the same database, and `DataProtectionKeys` holds exactly one key afterwards (the restart simulation).
- An Identity user created through `UserManager<IdentityUser>` in one instance is found by e-mail from another (the store is wired, not only migrated).
- `/api/health/live` → 200 and `/api/health/ready` → 200 against the container; with an unreachable connection string, `live` → 200 and `ready` → 503. Without `X-Health-Key`, with a wrong key, or with no `Health:ReadyKey` configured, `ready` → 404 and no check runs.
- `/api/health` and an unknown `/api/*` path → 404.

#### 4. CI test step

**File**: `.github/workflows/deploy.yml`

**Intent**: A failing test blocks the deploy.

**Contract**: In the `build` job, after `setup-dotnet` and before `Publish API`, a step runs `dotnet test api.Tests -c Release` (Docker is available on `ubuntu-latest`).

### Success Criteria:

#### Automated Verification:

- Tests pass locally: `dotnet test api.Tests`
- Test project files are not tracked as build output: `git status --porcelain api.Tests` shows no `bin/` or `obj/` paths

#### Manual Verification:

- After pushing, the GitHub Actions run shows the test step green before `Publish API`, and the smoke test on `/api/health/live` passes

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Azure Provisioning (Owner Runs or Approves Each Command)

### Overview

Create the database and the identities and permissions the app and CI need. Every command here creates or grants something in Azure, so the owner runs or approves each one; the agent prepares them and checks results. Variables: `RG=rg-battlemap`, `LOC=polandcentral`, `APP=battle-map-generator`, `SQL=sql-battlemap-<suffix>` (globally unique), `DB=sqldb-battlemap`, `DEPLOY_ID=id-battlemap-deploy`.

### Changes Required:

#### 1. SQL server and free-offer database

**File**: none (Azure resources)

**Intent**: An Entra-only SQL server with the owner as Entra admin, and one free-offer database in the app's region that pauses at the free limit instead of billing.

**Contract**: `az sql server create -g $RG -n $SQL -l $LOC --enable-ad-only-auth --external-admin-principal-type User --external-admin-name <owner UPN> --external-admin-sid <owner object id>`. `az sql db create -g $RG -s $SQL -n $DB -e GeneralPurpose -f Gen5 -c 2 --compute-model Serverless --auto-pause-delay 60 --use-free-limit --free-limit-exhaustion-behavior AutoPause` (the free-offer example from `az sql db create --help`; without the serverless flags the database would not be the free offer; if Poland Central does not offer the free database, stop and decide with the owner, because app and database must share a region). Never choose "continue with charges".

#### 2. Network access

**File**: none

**Intent**: Let the App Service reach the server without a VNet; CI adds its own temporary rule in Phase 4.

**Contract**: `az sql server firewall-rule create -g $RG -s $SQL -n AllowAzureServices --start-ip-address 0.0.0.0 --end-ip-address 0.0.0.0`. The owner's current IP gets a temporary rule for the next step and removes it afterwards.

#### 3. Identities and database users

**File**: none

**Intent**: The app connects as itself with data-only rights; the deploy identity can change the schema.

**Contract**:
- `az webapp identity assign -g $RG -n $APP` (system-assigned; its database user name is the app name).
- As the Entra admin (portal Query editor on `$DB`, or `sqlcmd` with Entra auth):
  `CREATE USER [battle-map-generator] FROM EXTERNAL PROVIDER;` + `db_datareader`, `db_datawriter`;
  `CREATE USER [id-battlemap-deploy] FROM EXTERNAL PROVIDER;` + `db_ddladmin`, `db_datareader`, `db_datawriter`.
- A custom role `SQL Firewall Rule Operator` (`az role definition create`, assignable at the resource group scope) with only `Microsoft.Sql/servers/read` and `Microsoft.Sql/servers/firewallRules/{read,write,delete}`, then `az role assignment create --assignee <deploy identity principalId> --role "SQL Firewall Rule Operator" --scope <SQL server resource id>`. Not `SQL Server Contributor`: that role includes `Microsoft.Sql/servers/*` and would let CI drop the database, which is human-only.

#### 4. App and GitHub configuration

**File**: none

**Intent**: Give the app its passwordless connection string and give CI the names it needs.

**Contract**: `az webapp config connection-string set -g $RG -n $APP -t SQLAzure --settings AppDb="Server=tcp:$SQL.database.windows.net,1433;Database=$DB;Authentication=Active Directory Default;Encrypt=True;"` (surfaces as `ConnectionStrings:AppDb`). GitHub repository variables `AZURE_RESOURCE_GROUP=rg-battlemap`, `AZURE_SQL_SERVER=$SQL`, `AZURE_SQL_DATABASE=$DB` (variables, not secrets: none of these are credentials). A random ready key (`openssl rand -hex 32`) stored twice: App Service app setting `Health__ReadyKey` and GitHub environment secret `HEALTH_READY_KEY` (`production` environment). It only gates a health probe; rotating it is a manual, human-only step (set both places, then redeploy or restart).

### Success Criteria:

#### Automated Verification:

- Database exists with the free offer: `az sql db show -g rg-battlemap -s $SQL -n sqldb-battlemap --query "{free:useFreeLimit,onLimit:freeLimitExhaustionBehavior,loc:location}"` shows `true`, `AutoPause`, `polandcentral`
- Server is Entra-only: `az sql server ad-only-auth get -g rg-battlemap -n $SQL` shows `azureAdOnlyAuthentication: true`
- App identity and connection string are set: `az webapp identity show -g rg-battlemap -n battle-map-generator` returns a principalId, and `az webapp config connection-string list -g rg-battlemap -n battle-map-generator` lists `AppDb` of type `SQLAzure`
- CI variables exist: `gh variable list` shows `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVER`, `AZURE_SQL_DATABASE`, and `gh secret list --env production` shows `HEALTH_READY_KEY`

#### Manual Verification:

- In the Azure portal, the database's free-offer setting reads "Auto-pause the database until next month" (not "continue with charges")
- The owner's temporary firewall rule is removed; only `AllowAzureServices` remains
- `SELECT name FROM sys.database_principals WHERE type = 'E'` in the database lists both `battle-map-generator` and `id-battlemap-deploy`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Migrations in CI and Production Proof

### Overview

Every deploy applies pending migrations before the new app starts, then proves both probes; the owner confirms on the real deployment that restarts keep the key ring.

### Changes Required:

#### 1. Build the migration bundle

**File**: `.github/workflows/deploy.yml` (`build` job)

**Intent**: Produce one self-contained executable that applies the migrations, built from the same commit as the app.

**Contract**: After tests, `dotnet tool restore` and `dotnet ef migrations bundle --project api --self-contained -r linux-x64 -o efbundle/efbundle --force`; upload `efbundle/` as a second artifact (`migrations`).

#### 2. Apply migrations in the deploy job

**File**: `.github/workflows/deploy.yml` (`deploy` job)

**Intent**: Migrate as `id-battlemap-deploy` through a firewall rule that exists only for the runner and only during the step, before the new app version is deployed.

**Contract**: After `azure/login` and before `azure/webapps-deploy`:
1. Download the `migrations` artifact and make it executable.
2. Read the runner's public IPv4 and create firewall rule `gh-runner-${{ github.run_id }}` on `vars.AZURE_SQL_SERVER`.
3. Run `./efbundle --connection "Server=tcp:<server>.database.windows.net,1433;Database=<db>;Authentication=Active Directory Default;Encrypt=True;"`. The bundle's retry strategy covers a paused database.
4. Delete the firewall rule in a step with `if: always()`.

#### 3. Smoke test both probes

**File**: `.github/workflows/deploy.yml`

**Intent**: A deploy is green only when the app is up and can use its database as its own identity.

**Contract**: Keep the `/api/health/live` curl; add the same retrying curl for `/api/health/ready` with header `X-Health-Key: ${{ secrets.HEALTH_READY_KEY }}` (10 × 15 s covers the resume from auto-pause).

### Success Criteria:

#### Automated Verification:

- Workflow is valid YAML and lists the new steps: `gh workflow view deploy.yml` succeeds, and `grep -c "if: always()" .github/workflows/deploy.yml` returns at least 1
- Bundle builds locally: `dotnet ef migrations bundle --project api --self-contained -r linux-x64 -o /tmp/efbundle --force` exits 0
- Tests still pass: `dotnet test api.Tests`

#### Manual Verification:

- The deploy run on `main` is green: migration step applied `InitialAccountStore`, the firewall rule step ran, and both smoke tests passed
- After the run, `az sql server firewall-rule list -g rg-battlemap -s $SQL -o table` shows only `AllowAzureServices`
- Key-ring proof: `SELECT Id, FriendlyName, LEN(Xml) FROM DataProtectionKeys` shows exactly one key; `az webapp restart -g rg-battlemap -n battle-map-generator`; `/api/health/ready` (with the key) → 200; the query shows the same single key
- Idle proof: after more than 60 minutes without traffic, with `az sql db show -g rg-battlemap -s $SQL -n sqldb-battlemap --query status` returning `Paused`, `/api/health/ready` (with the key) returns 200 within the retry window (record the time taken) and the same single key remains
- A second push to `main` with no new migration still deploys green (the bundle is a no-op)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Testing Strategy

### Unit Tests:

- None beyond the integration tests; F-01 has no pure logic worth isolating.

### Integration Tests:

- Testcontainers SQL Server: migrations apply cleanly; key persistence across two app instances on one database; Identity user round trip across instances; `live`/`ready` behaviour with and without a reachable database; `/api/health` and unknown `/api/*` return 404.

### Manual Testing Steps:

1. Local: run the API against Docker SQL Server, stop the container, check that `live` stays 200 and `ready` turns 503.
2. Production: key-ring query before and after `az webapp restart`.
3. Production: the same after an idle unload and database auto-pause.
4. Production: firewall rule list is clean after a deploy.

## Performance Considerations

- `ready` wakes a paused database, and anything calling it more often than the 60-minute auto-pause delay keeps the database awake and burns the free vCore-seconds (then AutoPause takes it offline until the 1st). That is why `ready` is key-gated and only CI and the owner call it; `live` is the endpoint for any monitoring. A few days after launch, the owner checks free-limit usage in the portal (database → Overview → free offer usage).
- The migration step adds bundle download and one short connection to each deploy; CI's test step adds a SQL Server container start (tens of seconds) to each run.

## Migration Notes

- First migration on an empty database; no data to move.
- Future migrations stay additive (`infrastructure.md` operational story). Rollback is redeploying an older artifact; the schema is not rolled back automatically, so a migration must not break the previous app version.
- If a migration fails, the deploy stops before the new app is published, and the previous version keeps running on the unchanged or partially migrated schema. Recovery is a fix-forward commit.

## References

- Roadmap item: `context/foundation/roadmap.md` (F-01)
- Risk register and operational story: `context/foundation/infrastructure.md`
- Existing Azure resources and OIDC setup: `context/changes/deployment/deployment-plan.md`
- Backend rules: `api/AGENTS.md`
- Lessons: `context/foundation/lessons.md` (OIDC subject, /api 404 guard, tooling out of the repo)
- Current pipeline: `api/Program.cs:1-48`, `.github/workflows/deploy.yml:1-78`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Local Account Store and Probes

#### Automated

- [x] 1.1 API builds: `dotnet build api` — 8f89390
- [x] 1.2 Migration exists and generates a script: `dotnet tool restore && dotnet ef migrations script --project api` lists `AspNetUsers` and `DataProtectionKeys` — 8f89390
- [x] 1.3 Migration applies to local SQL Server: `docker compose up -d sqlserver && dotnet ef database update --project api` exits 0 — 8f89390

#### Manual

- [x] 1.4 With the container up and `dotnet run --project api`: `/api/health/live` → 200, `/api/health/ready` with the local `X-Health-Key` → 200 and without it → 404, `/api/health` and `/api/nope` → 404, a deep link returns `index.html` (with `wwwroot` populated) — 8f89390
- [x] 1.5 With the container stopped: `/api/health/live` → 200, `/api/health/ready` with the key → 503 (after the retry window) — 8f89390
- [x] 1.6 After the first `ready` call, `SELECT Id, FriendlyName FROM DataProtectionKeys` in the container shows exactly one key; restarting the API and calling `ready` again leaves the same single key — 8f89390

### Phase 2: Integration Tests and CI Test Gate

#### Automated

- [x] 2.1 Tests pass locally: `dotnet test api.Tests` — e41f94b
- [x] 2.2 Test project files are not tracked as build output: `git status --porcelain api.Tests` shows no `bin/` or `obj/` paths — e41f94b

#### Manual

- [x] 2.3 After pushing, the GitHub Actions run shows the test step green before `Publish API`, and the smoke test on `/api/health/live` passes — e41f94b

### Phase 3: Azure Provisioning (Owner Runs or Approves Each Command)

#### Automated

- [x] 3.1 Database exists with the free offer: `az sql db show -g rg-battlemap -s $SQL -n sqldb-battlemap --query "{free:useFreeLimit,onLimit:freeLimitExhaustionBehavior,loc:location}"` shows `true`, `AutoPause`, `polandcentral` — 42c9e88
- [x] 3.2 Server is Entra-only: `az sql server ad-only-auth get -g rg-battlemap -n $SQL` shows `azureAdOnlyAuthentication: true` — 42c9e88
- [x] 3.3 App identity and connection string are set: `az webapp identity show -g rg-battlemap -n battle-map-generator` returns a principalId, and `az webapp config connection-string list -g rg-battlemap -n battle-map-generator` lists `AppDb` of type `SQLAzure` — 42c9e88
- [x] 3.4 CI variables exist: `gh variable list` shows `AZURE_RESOURCE_GROUP`, `AZURE_SQL_SERVER`, `AZURE_SQL_DATABASE`, and `gh secret list --env production` shows `HEALTH_READY_KEY` — 42c9e88

#### Manual

- [x] 3.5 In the Azure portal, the database's free-offer setting reads "Auto-pause the database until next month" (not "continue with charges") — 42c9e88
- [x] 3.6 The owner's temporary firewall rule is removed; only `AllowAzureServices` remains — 42c9e88
- [x] 3.7 `SELECT name FROM sys.database_principals WHERE type = 'E'` in the database lists both `battle-map-generator` and `id-battlemap-deploy` — 42c9e88

### Phase 4: Migrations in CI and Production Proof

#### Automated

- [x] 4.1 Workflow is valid YAML and lists the new steps: `gh workflow view deploy.yml` succeeds, and `grep -c "if: always()" .github/workflows/deploy.yml` returns at least 1 — 1c149ee
- [x] 4.2 Bundle builds locally: `dotnet ef migrations bundle --project api --self-contained -r linux-x64 -o /tmp/efbundle --force` exits 0 — 1c149ee
- [x] 4.3 Tests still pass: `dotnet test api.Tests` — 1c149ee

#### Manual

- [x] 4.4 The deploy run on `main` is green: migration step applied `InitialAccountStore`, the firewall rule step ran, and both smoke tests passed — 1c149ee
- [x] 4.5 After the run, `az sql server firewall-rule list -g rg-battlemap -s $SQL -o table` shows only `AllowAzureServices` — 1c149ee
- [x] 4.6 Key-ring proof: `SELECT Id, FriendlyName, LEN(Xml) FROM DataProtectionKeys` shows exactly one key; `az webapp restart -g rg-battlemap -n battle-map-generator`; `/api/health/ready` (with the key) → 200; the query shows the same single key — 1c149ee
- [x] 4.7 Idle proof: after more than 60 minutes without traffic, with `az sql db show -g rg-battlemap -s $SQL -n sqldb-battlemap --query status` returning `Paused`, `/api/health/ready` (with the key) returns 200 within the retry window (record the time taken) and the same single key remains — 1c149ee
- [x] 4.8 A second push to `main` with no new migration still deploys green (the bundle is a no-op) — 1c149ee
