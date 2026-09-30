# Battle Map Generator End-to-End Tests

Read the root `../AGENTS.md` first. This package tests the whole app the way it is deployed: the ASP.NET Core host in `../api/` serving the built SPA from `../web/`. Tests of one side stay with that side (`../api.Tests/`, `../web/app/**/*.test.ts`).

## Rules

- Playwright in **Chromium and Firefox**; both are required by the PRD.
- Run against the real .NET host in the Production environment with no database (`playwright.config.ts` starts it on port 5108). Never mock the API here; mocked, SPA-only tests belong in `../web/`.
- Locate elements the way a user finds them (role and accessible name, visible text), so a restyle in `web/` doesn't break the tests, and a broken flow does.
- The generate endpoint allows 10 calls per minute per IP, and both browser projects hit the same server: keep a full run well under that (today 4 calls).
- Changing a locator here because the UI changed on purpose is fine; loosening an assertion to make a run green is not.

## Commands

- `npm ci` once, then `npx playwright install chromium firefox`.
- `npm run build:app`: builds `../web` and copies `build/client` into `../api/wwwroot` (gitignored). Rerun it after any web change.
- `npm test`: runs Playwright; it starts the API itself (`dotnet run`), or reuses one already on 5108 locally.
- `npm run typecheck`: `tsc` over the specs and config.
