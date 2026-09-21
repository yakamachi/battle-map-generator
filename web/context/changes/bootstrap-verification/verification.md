---
bootstrapped_at: 2026-09-17T15:38:03Z
starter_id: react-router
starter_name: React Router (formerly Remix)
project_name: battle-map-generator-web
language_family: js
package_manager: npm
cwd_strategy: subdir-then-move
bootstrapper_confidence: verified
phase_3_status: ok
audit_command: npm audit --json
---

## Hand-off

```yaml
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
```

### Why this stack

React Router with TypeScript is the explicitly selected frontend for a solo developer familiar with React and experienced in .NET, delivering a small-scale MVP in five after-hours weeks. Its official starter passes all four registry quality gates and has verified scaffolding confidence. Use SPA mode, not a React server backend: ASP.NET Core lives in a separate repository and serves the compiled static frontend on Azure App Service. The registry-compatible self-host target denotes this application-owned hosting, not an additional VPS or Node.js service. GitHub Actions on standard Linux runners should check changes and automatically deploy approved merges to main; transferring the frontend build to the backend deployment requires explicit downstream integration. Frontend scope covers email/password login UI, encounter parameters, generation/regeneration requests, PNG preview and download. Authentication must integrate with the .NET API; it is not supplied by this starter. AI, payments, realtime, background jobs and a saved-map library are outside MVP. The accompanying PRD is an unchanged snapshot of the whole-product PRD in the 10x-project planning repository, not a requirement to implement the backend here. SPA mode and combined hosting must be explicitly applied during bootstrap because its machine-readable contract does not encode them.

## Pre-scaffold verification

| Signal      | Value                                           | Severity | Notes                                                                    |
| ----------- | ----------------------------------------------- | -------- | ------------------------------------------------------------------------ |
| npm package | create-react-router v8.4.0 published 2026-09-15 | fresh    | resolved from cmd_template                                               |
| GitHub repo | not run                                         | —        | card docs_url is reactrouter.com (not GitHub); `gh` CLI not installed |

Toolchain: node v26.8.2, npm 12.0.2.

## Scaffold log

**Resolved invocation**: `npx create-react-router@latest .bootstrap-scaffold --yes --package-manager npm`
**Strategy**: subdir-then-move
**Exit code**: 0
**Files moved**: 13 top-level entries (.agents/, app/, public/, node_modules/, Dockerfile, .dockerignore, .gitignore, package.json, package-lock.json, react-router.config.ts, README.md, tsconfig.json, vite.config.ts)
**Conflicts (.scaffold siblings)**: none
**.gitignore handling**: moved silently (absent in cwd)
**Nested .git**: the CLI ran `git init` + initial commit inside `.bootstrap-scaffold/`; that `.git/` was deleted before move-up so starter history does not leak into the existing repo.
**.bootstrap-scaffold cleanup**: deleted

CLI output:

```
npm notice run npx
npm notice run 'create-react-router' .bootstrap-scaffold --yes --package-manager npm
         create-react-router v8.4.0
      ◼  Shell is not interactive. Using default options. This is equivalent to running with the --yes flag.
      ◼  Directory: Using .bootstrap-scaffold as project directory
      ◼  Using default template See https://github.com/remix-run/react-router-templates for more
      ✔  Template copied
      ◼  Agent skill: Included React Router agent skill
      ✔  Dependencies installed
      ✔  Git initialized
  done   That's it!
         Enter your project directory using cd ./.bootstrap-scaffold
         Check out README.md for development and deploy instructions.
         Join the community at https://remix.run/discord
```

Prior attempt (same day) exited 1 at the CLI's git-init step because git user.name/user.email were unset; resolved by configuring git identity and re-running.

## Post-scaffold audit

**Tool**: npm audit --json
**Exit code**: 0
**Summary**: 0 CRITICAL, 0 HIGH, 0 MODERATE, 0 LOW (0 info)
**Direct vs transitive**: 0/0/0/0 direct of total 0/0/0/0
**Dependency counts**: {"prod":97,"dev":131,"optional":50,"peer":0,"peerOptional":0,"total":227}

#### CRITICAL findings

None.

#### HIGH findings

None.

#### MODERATE findings

None.

#### LOW / INFO findings

None.

## Hints recorded but not acted on

| Hint                    | Value                |
| ----------------------- | -------------------- |
| bootstrapper_confidence | verified             |
| quality_override        | false                |
| path_taken              | custom               |
| self_check_answers      | typed: true, from_official_starter: true, conventions: true, docs_current: true, can_judge_agent: true |
| team_size               | solo                 |
| deployment_target       | self-host            |
| ci_provider             | github-actions       |
| ci_default_flow         | auto-deploy-on-merge |
| has_auth                | true                 |
| has_payments            | false                |
| has_realtime            | false                |
| has_ai                  | false                |
| has_background_jobs     | false                |

Not applied from the hand-off body (manual follow-up): SPA mode (`ssr: false` in `react-router.config.ts`) and hosting the static build from the ASP.NET Core app on Azure App Service (the scaffold ships a Node `Dockerfile` that assumes SSR hosting).

## Next steps

Next: a future skill will set up agent context (CLAUDE.md, AGENTS.md). For now, your project is scaffolded and verified — happy hacking.

Useful manual steps in the meantime:
- `git init` (if you have not already) to start your own repo history.
- Review any `.scaffold` siblings the conflict policy created and decide which version of each file to keep.
- Address audit findings per your project's risk tolerance — the full breakdown is in this log.
