# Cloud Account Store and Persistent Sessions — Plan Brief

> Full plan: `context/changes/account-store-foundation/plan.md`

## What & Why

Roadmap F-01: give the API a cloud account store (Azure SQL free offer) holding the ASP.NET Core Identity schema and the Data Protection key ring. On .NET 10 Linux App Service, keys are otherwise lost on every restart or idle unload, which would log users out, and Azure SQL's free offer fails the first connection after auto-pause. Fixing both now keeps S-04 (login) from turning into infrastructure debugging.

## Starting Point

The API is a skeleton: `GET /api/health`, SPA serving and the `/api/*` 404 guard. It has no EF Core, no Identity and no test project. Azure has the F1 app, `rg-battlemap` in Poland Central and the OIDC deploy identity `id-battlemap-deploy`, but no SQL server or database.

## Desired End State

The deployed app reads and writes an Entra-only Azure SQL free-offer database (auto-pause at limit) as its own managed identity. Every push to `main` tests the store against real SQL Server, applies migrations through an EF bundle, deploys and smoke-tests `/api/health/live` and `/api/health/ready`. After `az webapp restart` or an idle unload, the same single Data Protection key remains in the database.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) |
| --- | --- | --- |
| Schema scope | Identity tables + `DataProtectionKeys`, `AddIdentityCore`, no endpoints | F-01 delivers the real account store; S-04 only adds endpoints and UI. |
| Production SQL auth | Managed identity, Entra-only server (`Active Directory Default`) | No password to store or rotate, matching the secretless OIDC deploy. |
| Migrations | EF bundle in CI, run as `id-battlemap-deploy` through a temporary runner firewall rule; the identity gets a custom firewall-rules-only role on the SQL server | A failed migration blocks the deploy instead of breaking the running app, and CI cannot drop the database. |
| Persistence proof | Key-ring check (same single key before/after restart) + two-instance integration test | Tests the exact failure mode without a production probe endpoint. |
| Health | Kubernetes-style `/api/health/live` (public, no DB) and `/api/health/ready` (DB + Data Protection round trip, gated by an `X-Health-Key` header, 404 without it); `/api/health` removed | A paused DB never makes the app look down, `ready` proves app→DB as its own identity, and strangers cannot wake the DB (F1 cannot restrict single paths by network). |
| Testing | xUnit + Testcontainers MsSql in `api.Tests/`, `dotnet test` gates CI | Same engine as Azure SQL, so the real migration is covered. |
| Local dev | SQL Server in Docker via `compose.yaml` | `api/AGENTS.md`: dev matches Azure SQL. |

## Scope

**In scope:** EF Core SQL Server with retry on failure; `AppDbContext`; Identity core registration; Data Protection keys in the DB with a fixed application name; initial migration; `live`/`ready` probes; local Docker SQL; integration tests and CI test gate; Azure SQL server, database, identities, DB users and RBAC (owner-run); migration bundle and dual smoke test in CI; production restart and idle proof.

**Out of scope:** login, registration, cookies and account creation (S-04); authorization; map storage; migrate-at-startup; SQL passwords in Azure; VNet or private endpoints; plan-tier change or "continue with charges".

## Architecture / Approach

The code is the same in every environment; only `ConnectionStrings:AppDb` differs. Locally it uses SQL auth to Docker. In Azure it uses `Authentication=Active Directory Default`, which resolves to the app's system-assigned identity at runtime and to the Azure CLI login of `id-battlemap-deploy` in CI. The CI build job runs the tests and builds both the app and a self-contained `efbundle`. The deploy job logs in, opens the SQL firewall for the runner's IP, migrates, always closes the firewall again, deploys, then checks `live` and `ready` (with the key).

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. Local account store and probes | DbContext, Identity core, DB-backed keys, migration, `live`/`ready`, Docker SQL | The design-time bundle build must tolerate a missing connection string |
| 2. Integration tests and CI test gate | Testcontainers tests for migration, key persistence, probes; `dotnet test` in CI | Slower CI (container start) |
| 3. Azure provisioning (owner) | Entra-only SQL server, free serverless DB in Poland Central, managed identity, DB users, custom firewall-rules role, connection string, ready key, GitHub variables and secret | The free offer may not be available in Poland Central; billing choice is irreversible |
| 4. Migrations in CI and production proof | Bundle in deploy with temporary firewall rule, dual smoke test, restart and pause/resume key-ring proof | The firewall rule must always be removed |

**Prerequisites:** owner signed in with `az` and `gh`; Docker running locally; the owner is available to run the Phase 3 commands (creating resources and granting roles is human-only).
**Estimated effort:** ~3 sessions: Phases 1–2 in one or two, Phases 3–4 together in one.

## Open Risks & Assumptions

- Assumes the Azure SQL free offer is available in Poland Central. If not, Phase 3 stops for an owner decision, because app and database must share a region.
- Assumes `Active Directory Default` in SqlClient picks up the `azure/login` CLI session on the runner. If it does not, fall back to fetching an access token with `az account get-access-token` for the bundle.
- The deploy identity gains a custom firewall-rules-only role on the SQL server and `db_ddladmin` in the database, which widens its reach beyond the web app (but not to dropping the database).
- One low-value secret returns: the ready key (App Service setting + GitHub environment secret), rotated by hand.
- The persistence proof is indirect until S-04 checks a real login cookie across a restart.
- Anything calling `ready` more often than the 60-minute pause delay would burn the free vCore-seconds; the key gate limits callers to CI and the owner, and free-limit usage is checked after launch. S-04's public login will reopen this exposure.
- Data Protection keys are stored unencrypted in the DB; Entra-only access is the boundary.
- Entra auth relies on SqlClient 6.x (from EF Core 10); a 7.x upgrade needs `Microsoft.Data.SqlClient.Extensions.Azure`.

## Success Criteria (Summary)

- A restart or idle unload of the deployed app leaves the Data Protection key ring unchanged, so S-04 logins will survive them.
- The first request after the database auto-pauses succeeds within the retry window instead of failing with 40613.
- Deploys stay green without SQL passwords: migrations applied by CI, the app connected as its own identity, and no firewall rule left open.
