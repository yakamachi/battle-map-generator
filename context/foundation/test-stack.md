# Test stack

## E2E

<!-- Written by /10x-e2e-setup. Re-run it to change this section; other skills only read it. -->

- runner: Playwright Test, @playwright/test 1.63.0
- config: e2e/playwright.config.ts
- single-spec command: `cd e2e && npx playwright test tests/map.spec.ts`
- full-suite command: `cd e2e && npm test` (both browsers; `npm run db:up` must have run first)
- base URL: http://localhost:5108
- port: 5108 (hardcoded as `BASE_URL` in e2e/playwright.config.ts; not read from `E2E_PORT` — this project predates that convention and the config was left as-is rather than migrated without being asked)
- web server command: `dotnet run --project ../api --no-launch-profile --urls http://localhost:5108` with `ASPNETCORE_ENVIRONMENT=Production` and `ConnectionStrings__AppDb` pointed at the compose SQL Server (`../compose.yaml`, started by `npm run db:up`); reuseExistingServer outside CI
- auth setup project: setup (tests/auth.setup.ts), fixed account `e2e@example.com` with a hardcoded password (not `E2E_USERNAME`/`E2E_PASSWORD` — deliberate for this project, documented in `e2e/AGENTS.md`)
- storageState: playwright/.auth/user.json (gitignored)
- seed: tests/map.spec.ts ("generate, preview and download a map, then a second one") — protects the critical login→generate→regenerate→download flow (AGENTS.md's e2e scope, wired as the CI `e2e` job). Not tied to a single numbered risk in test-plan.md: every one of that plan's 6 risks resolves to unit, integration, or browser-mode Vitest under its own cost×signal rule, so no numbered risk is the seed's risk. Treated as the exemplar in place of a new file because it already carries every seed property (role locators, self-contained, waits for state, business assertions) — no second file was created to avoid duplicating it.
- browser CLI: playwright-cli via `npx -y @playwright/cli` (global `npm i -g` blocked by EACCES on this machine), command skill at `.claude/skills/playwright-cli/SKILL.md`
- updated: 2026-10-11
