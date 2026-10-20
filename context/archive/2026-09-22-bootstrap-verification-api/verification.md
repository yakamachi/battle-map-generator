---
bootstrapped_at: 2026-09-17T16:01:21Z
starter_id: dotnet
starter_name: .NET (ASP.NET Core webapi)
project_name: battle-map-generator-api
language_family: dotnet
package_manager: dotnet
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: dotnet list package --vulnerable
---

## Hand-off

```yaml
starter_id: dotnet
package_manager: dotnet
project_name: battle-map-generator-api
hints:
  language_family: dotnet
  team_size: solo
  deployment_target: azure-app-service
  ci_provider: github-actions
  ci_default_flow: auto-deploy-on-merge
  bootstrapper_confidence: verified
  path_taken: custom
  quality_override: false
  self_check_answers:
    typed: true
    from_official_starter: true
    conventions: true
    docs_current: true
    can_judge_agent: true
  has_auth: true
  has_payments: false
  has_realtime: false
  has_ai: false
  has_background_jobs: false
```

### Why this stack

The solo developer's strong .NET experience and five-week, after-hours MVP favor ASP.NET Core. The official webapi starter passes all four registry quality gates and has verified scaffolding confidence. This repository owns authentication, procedural BSP map generation, PNG rendering and the API; React Router with TypeScript belongs to the separate battle-map-generator-web repository. ASP.NET Core will serve that frontend's compiled SPA assets, avoiding a second application server. Email/password authentication requires implementation; ASP.NET Core Identity with EF Core and Azure SQL's free offer are proposed, not already configured. Azure App Service F1 is accepted for learning/demo with CPU quotas, idle unloading and no SLA, not supported production workloads. Database pause-on-limit and included GitHub Actions allowances support the low-cost goal without guaranteeing zero total costs. GitHub Actions on standard Linux runners should check and deploy merges to main; frontend artifact delivery requires explicit integration. AI, payments, realtime, background jobs and a saved-map library remain outside MVP. This hand-off adapts the original .NET selection in the 10x-project planning repository to the separately named backend repository.

## Pre-scaffold verification

| Signal      | Value   | Severity | Notes                                                                                      |
| ----------- | ------- | -------- | ------------------------------------------------------------------------------------------ |
| npm package | not run | —        | non-JS starter (`dotnet new`), no npm package to check                                     |
| GitHub repo | not run | —        | card `docs_url` is `https://learn.microsoft.com/aspnet/core` (not GitHub); no recency signal |
| Local SDK   | .NET SDK 10.0.112 | — | informational; template produced `net10.0`, `Microsoft.AspNetCore.OpenApi` 10.0.12 |

## Scaffold log

**Resolved invocation**: `dotnet new webapi -n battle-map-generator-api -o .bootstrap-scaffold --no-restore`
**Strategy**: subdir-then-move
**Session override**: registry template is `dotnet new webapi -n {name} --no-restore`. Because `-n` also sets the .NET project name and root namespace, substituting `{name}=.bootstrap-scaffold` would have produced `.bootstrap-scaffold.csproj`. With user confirmation, `-n` kept `project_name` and `-o .bootstrap-scaffold` was added for the temp output folder. Hand-off file unchanged.
**Exit code**: 0 (CLI also printed a non-fatal notice: "An issue was encountered verifying workloads")
**Files moved**: 6 — `Program.cs`, `appsettings.json`, `appsettings.Development.json`, `battle-map-generator-api.csproj`, `battle-map-generator-api.http`, `Properties/launchSettings.json`
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: absent in scaffold (`webapi` template ships no `.gitignore`)
**.bootstrap-scaffold cleanup**: deleted

## Post-scaffold audit

**Tool**: `dotnet list package --vulnerable --include-transitive`
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW
**Direct vs transitive**: 0/0/0/0 direct of total 0/0/0/0. Packages audited: direct `Microsoft.AspNetCore.OpenApi` 10.0.12; transitive `Microsoft.OpenApi` 2.12.0.
**Source**: https://api.nuget.org/v3/index.json
**Audited at**: 2026-09-17T16:04:05Z (re-run)

Tool output: "The given project `battle-map-generator-api` has no vulnerable packages given the current sources." (exit code 0)

**Re-run note**: the first audit attempt at 2026-09-17T16:01:21Z failed. `dotnet restore` stopped with `NETSDK1226: Prune Package data not found .NETCoreApp 10.0 Microsoft.AspNetCore.App` because the ASP.NET Core runtime and targeting pack were not installed. After the user installed `aspnet-runtime` and `aspnet-targeting-pack` (`Microsoft.AspNetCore.App` 10.0.12), `dotnet restore` succeeded, the audit ran clean, and `dotnet build` passed with 0 warnings and 0 errors.

#### CRITICAL findings

None.

#### HIGH findings

None.

#### MODERATE findings

None.

#### LOW / INFO findings

None.

## Hints recorded but not acted on

| Hint                               | Value                |
| ---------------------------------- | -------------------- |
| bootstrapper_confidence            | verified             |
| quality_override                   | false                |
| path_taken                         | custom               |
| self_check_answers.typed           | true                 |
| self_check_answers.from_official_starter | true           |
| self_check_answers.conventions     | true                 |
| self_check_answers.docs_current    | true                 |
| self_check_answers.can_judge_agent | true                 |
| team_size                          | solo                 |
| deployment_target                  | azure-app-service    |
| ci_provider                        | github-actions       |
| ci_default_flow                    | auto-deploy-on-merge |
| has_auth                           | true                 |
| has_payments                       | false                |
| has_realtime                       | false                |
| has_ai                             | false                |
| has_background_jobs                | false                |

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- Add a .NET `.gitignore` (`dotnet new gitignore`); the webapi template does not ship one, so `bin/` and `obj/` are currently not ignored.
- `git init` is already done; commit the scaffold when ready.
- Address audit findings per your project's risk tolerance (none at scaffold time; re-run the audit as dependencies are added).
