---
starter_id: dotnet
package_manager: dotnet
project_name: battle-map-generator-dla-d-d-dm-toolkit
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

The solo developer's strong .NET experience and five-week, after-hours MVP favor ASP.NET Core over learning another backend. The selected backend starter passes all four registry quality gates. The explicitly chosen frontend is React Router with TypeScript in SPA mode, using its official starter and npm; ASP.NET Core can serve the built static assets without a separate Node.js server. Email/password authentication still needs implementation, with ASP.NET Core Identity and EF Core as the proposed approach. Azure App Service F1 is accepted for learning/demo, not supported production workloads: CPU quotas, idle unloading, and no SLA are understood. Azure SQL's free offer with pause-on-limit is the proposed account database; these choices do not guarantee zero total costs. GitHub Actions on standard Linux runners will check and deploy merges to main within the account's included allowance. Procedural BSP generation and PNG export require no AI, payments, realtime, or job queue; map storage is outside MVP. Registry confidence is verified for the .NET starter only, not the combined stack. This hand-off encodes one starter: the React frontend and integration must be explicitly included during scaffolding because the bootstrapper does not parse this rationale.

## Revisions

- 2026-09-21 — **Monorepo.** The separate `battle-map-generator-api` and `battle-map-generator-web` repositories were merged, with history, into the `battle-map-generator` monorepo as `api/` and `web/`. Per-app hand-offs live in `tech-stack-api.md` and `tech-stack-web.md`; mentions of "separate repositories" in them are historical.
- 2026-09-21 — **Map delivery.** The API returns a JSON semantic grid and never renders images; `web/` renders the map on a canvas and exports the PNG. See `infrastructure.md`.
