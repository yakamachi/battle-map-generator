---
starter_id: react-router
package_manager: npm
project_name: battle-map-generator-web
hints:
  language_family: js
  team_size: solo
  deployment_target: self-host
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

React Router with TypeScript is the explicitly selected frontend for a solo developer familiar with React and experienced in .NET, delivering a small-scale MVP in five after-hours weeks. Its official starter passes all four registry quality gates and has verified scaffolding confidence. Use SPA mode, not a React server backend: ASP.NET Core lives in a separate repository and serves the compiled static frontend on Azure App Service. The registry-compatible self-host target denotes this application-owned hosting, not an additional VPS or Node.js service. GitHub Actions on standard Linux runners should check changes and automatically deploy approved merges to main; transferring the frontend build to the backend deployment requires explicit downstream integration. Frontend scope covers email/password login UI, encounter parameters, generation/regeneration requests, browser-side canvas rendering of the API's tile grid, PNG preview and download. Authentication must integrate with the .NET API; it is not supplied by this starter. AI, payments, realtime, background jobs and a saved-map library are outside MVP. The accompanying PRD is an unchanged snapshot of the whole-product PRD in the 10x-project planning repository, not a requirement to implement the backend here. SPA mode and combined hosting must be explicitly applied during bootstrap because its machine-readable contract does not encode them.

## Revisions

- 2026-09-21 — This app now lives in `web/` of the `battle-map-generator` monorepo; see the revisions in `tech-stack.md`. References to separate repositories above are historical.
