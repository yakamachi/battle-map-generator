# Battle Map Generator End-to-End Tests

Read the root `../AGENTS.md` first. This package tests the whole app the way it is deployed: the ASP.NET Core host in `../api/` serving the built SPA from `../web/`. Tests of one side stay with that side (`../api.Tests/`, `../web/app/**/*.test.ts`).

## Rules

- Playwright in **Chromium and Firefox**; both are required by the PRD.
- Run against the real .NET host in the Production environment and the SQL Server from `../compose.yaml` (`playwright.config.ts` starts the host on port 5108; its connection string lives in `local-db.ts`). Run `npm run db:up` once before `npm test`. Never mock the API here; mocked, SPA-only tests belong in `../web/`.
- Tests start logged in as `e2e@example.com`: the `setup` project (`tests/auth.setup.ts`) registers or logs in that account and saves the session for both browser projects. A spec that needs a logged-out browser resets `storageState` itself. If setup reports that the account exists with another password, reset the local database with `docker compose down -v` (from the repo root) and rerun `npm run db:up`.
- Locate elements the way a user finds them (role and accessible name, visible text), so a restyle in `web/` doesn't break the tests, and a broken flow does.
- The generate endpoint allows 10 calls per minute per account, and both browser projects use the one e2e account: keep a full run well under that (today 4 generations). Register and login share 10 calls per minute per IP (every call comes from 127.0.0.1): a full run uses at most 6 (the `setup` project's up to 2, plus `tests/auth.spec.ts`'s 2 per browser = 4, plus `tests/access.spec.ts`'s 0), so do not add auth calls lightly. `tests/access.spec.ts` checks that a request without a session gets 401; it makes no auth call and spends no generate permit. `tests/auth.spec.ts` resets `storageState` to a logged-out browser and proves the whole flow through the UI: register, see the home view, log out, log in again; it makes no generate call.
- Changing a locator here because the UI changed on purpose is fine; loosening an assertion to make a run green is not.

## Commands

- `npm ci` once, then `npx playwright install chromium firefox`.
- `npm run db:up`: starts the compose SQL Server (Docker), waits until it is healthy and applies the migrations. Safe to rerun. Needs Node 22.18 or later (it imports `local-db.ts` directly). Every checkout shares one container (compose project `battle-map-generator`).
- `npm run build:app`: builds `../web` and copies `build/client` into `../api/wwwroot` (gitignored). Rerun it after any web change.
- `npm test`: runs Playwright; it starts the API itself (`dotnet run`), or reuses one already on 5108 locally. Stop any API on 5108 that was not started against the compose database, or setup fails with a 500. Another checkout or worktree running e2e uses the same port, so outside CI a run can silently test the other checkout's API; check what listens on 5108, or run with `CI=1`, which refuses to reuse a server.
- `npm run typecheck`: `tsc` over the specs and config.
