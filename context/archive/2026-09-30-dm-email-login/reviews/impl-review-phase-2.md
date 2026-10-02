<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: DM Email Login (S-04)

- **Plan**: context/changes/dm-email-login/plan.md
- **Scope**: Phase 2 of 4
- **Reviewed phases**: 2
- **Date**: 2026-09-30
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Commits 27ca344 and 5ca3041. Automated criteria passed locally before the commit (db:up, typecheck, two full e2e runs, ignore check, ci.yml parse). Manual row 2.7: CI run 36757801684 on 5ca3041 is green in all six jobs, and the e2e log shows the setup project passing. The first run (36757295784) failed on NETSDK1004, fixed in 5ca3041.

## Findings

### F1 — Setup treats any 400 from register as "account exists", and a wrong password locks the account

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: e2e/tests/auth.setup.ts:16-18
- **Detail**: Register also answers 400 for validation errors. On a developer's database that already holds `e2e@example.com` with another password, or after a password-policy change, setup falls back to login and fails with a misleading 401. Login uses lockout, so five such runs lock the account for 5 minutes, and each failing run spends 2 of the 10 auth permits.
- **Fix**: Fall back to login only when the 400's `errors.email` says the email is taken; otherwise fail and print the body. Give the login failure a hint (the account exists with another password: reset the database with `docker compose down -v`) and document the reset in e2e/AGENTS.md.
- **Decision**: FIXED

### F2 — The required e2e check depends on a path inside a floating image tag

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: compose.yaml:5, :16
- **Detail**: `mssql/server:2022-latest` is not pinned, and the healthcheck hardcodes `/opt/mssql-tools18/bin/sqlcmd`. Microsoft has moved the tools path before. If an update moves it, the container never turns healthy and `db:up` fails after about 170 s on every pull request. Unconfirmed: nothing has failed yet. The API tests (`SqlServerFixture.cs:13`) use the same floating tag, but without a healthcheck path.
- **Fix A ⭐ Recommended**: Pin a cumulative-update tag in compose.yaml and SqlServerFixture.cs together, and bump it deliberately
  - Strength: The required check changes only when someone changes the tag; dev, e2e and API tests stay on one version.
  - Tradeoff: Security patches arrive only when someone bumps the tag.
  - Confidence: HIGH — Microsoft publishes CU tags for 2022 on mcr.microsoft.com.
  - Blind spot: Which CU tag is current has not been looked up yet.
- **Fix B**: Accept the floating tag and note the failure mode in e2e/AGENTS.md
  - Strength: Always the latest patched image, no bumps to remember.
  - Tradeoff: A path move breaks every pull request until someone fixes it.
  - Confidence: MEDIUM.
  - Blind spot: How often the path changes.
- **Decision**: FIXED via Fix A — pinned `2022-CU27-ubuntu-22.04` (same image ID as `2022-latest` on 2026-09-30) in compose.yaml and SqlServerFixture.cs

### F3 — api/CLAUDE.md still says e2e runs from web/ with no database

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/CLAUDE.md:9
- **Detail**: "The web e2e tests (`npm run e2e` in `../web/`) start this app ... and no database." Both parts are false now: e2e moved to `../e2e/` earlier, and this phase gave it a database.
- **Fix**: Reword to: the e2e tests (`npm test` in `../e2e/`, after `npm run db:up`) start this app with `dotnet run --no-launch-profile` in the Production environment against the compose SQL Server.
- **Decision**: FIXED

### F4 — The config and the setup resolve the session file differently

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: e2e/playwright.config.ts:6, e2e/tests/auth.setup.ts:7-8
- **Detail**: The setup writes to a path fixed to e2e/, but the config passes a relative path that Playwright reads from the working directory. `npm --prefix e2e test` works; `npx playwright test -c e2e/playwright.config.ts` from the repo root gets ENOENT in both browsers (confirmed in Playwright's source).
- **Fix**: Export the resolved path once (for example from local-db.ts or a small sibling module) and use it in both files.
- **Decision**: FIXED

### F5 — A failed db:up step prints the connection string, password included

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: e2e/scripts/db-up.mjs:8, :14
- **Detail**: Node's error for a failed `execFileSync` includes the full argv, so a failed migration prints the SA password unmasked in the CI log (reproduced with a scratch command). Nothing new is exposed today: the password is a committed local-only throwaway. The pattern would leak a real secret if one were ever passed this way.
- **Fix**: Catch the error in `run` and print only the command name and exit code before exiting non-zero.
- **Decision**: FIXED (verified with a fake failing `dotnet ef`: only the command and exit code are printed)

### F6 — A reused local server started without the database fails setup with a bare 500

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: e2e/playwright.config.ts:44
- **Detail**: Locally, `reuseExistingServer` takes whatever listens on 5108. A host left over from before this phase (no connection string) makes setup fail with "register … 500".
- **Fix**: One line in e2e/AGENTS.md and the config comment: stop any API on 5108 that was not started against the compose database.
- **Decision**: FIXED

### F7 — Two checkouts on one machine cannot both run db:up

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: compose.yaml
- **Detail**: The compose project name comes from the directory, so each worktree gets its own container and volume, and both bind 127.0.0.1:1433. `db:up` in a second checkout fails with "port is already allocated" while another's container runs. CI is unaffected.
- **Fix**: Set a fixed top-level `name: battle-map-generator` in compose.yaml so every checkout shares one container and volume (the main checkout's existing one).
- **Decision**: FIXED

### F8 — db-up.mjs needs Node 22.18 or later, and nothing says so

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: e2e/scripts/db-up.mjs:5, e2e/package.json
- **Detail**: Importing `../local-db.ts` from an .mjs file relies on Node's built-in type stripping, on by default from 22.18 / 23.6. CI (Node 24) and local (Node 26) are fine; an older Node 22 fails with ERR_UNKNOWN_FILE_EXTENSION.
- **Fix**: Add `"engines": { "node": ">=22.18" }` to e2e/package.json.
- **Decision**: FIXED

## Noted, no action proposed

- The auth-call budget (≤ 6 per run; a quick local rerun on a reused server can reach 12 and get a 429) is documented only in auth.setup.ts. Phase 4 already updates e2e/AGENTS.md and the config comment with it.
- Checked and fine: the session file is ignored, the healthcheck's `$$` escape and timings, `execFileSync` without a shell (the `;` in the connection string would break a shell string), step order matching deploy.yml, the non-Secure cookie over http://localhost working from storageState in both browsers, and style matching build-app.mjs and map.spec.ts.
