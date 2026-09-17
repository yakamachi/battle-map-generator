---
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
---

## Why this stack

The solo developer's strong .NET experience and five-week, after-hours MVP favor ASP.NET Core. The official webapi starter passes all four registry quality gates and has verified scaffolding confidence. This repository owns authentication, procedural BSP map generation, PNG rendering and the API; React Router with TypeScript belongs to the separate battle-map-generator-web repository. ASP.NET Core will serve that frontend's compiled SPA assets, avoiding a second application server. Email/password authentication requires implementation; ASP.NET Core Identity with EF Core and Azure SQL's free offer are proposed, not already configured. Azure App Service F1 is accepted for learning/demo with CPU quotas, idle unloading and no SLA, not supported production workloads. Database pause-on-limit and included GitHub Actions allowances support the low-cost goal without guaranteeing zero total costs. GitHub Actions on standard Linux runners should check and deploy merges to main; frontend artifact delivery requires explicit integration. AI, payments, realtime, background jobs and a saved-map library remain outside MVP. This hand-off adapts the original .NET selection in the 10x-project planning repository to the separately named backend repository.
