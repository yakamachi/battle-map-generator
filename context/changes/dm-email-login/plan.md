# DM Email Login (S-04) Implementation Plan

## Overview

Roadmap slice S-04. The DM registers and logs in with email and password; a logged-out user sees only login and register and cannot generate a map, also when calling the API directly; each account may generate 10 maps per minute. The change runs in parallel with S-03 (`encounter-parameters`), so it stays out of the Generate handler and `home.tsx`.

## Current State Analysis

- The account store is complete: Identity tables and the Data Protection key ring are migrated (`api/Migrations/20260923154324_InitialAccountStore.cs`), and `UserManager` works end to end (`api.Tests/AccountStoreTests.cs:54-76`). No migration is needed.
- Only Identity's core services are registered (`api/Program.cs:62-64`). There is no cookie scheme, no `SignInManager`, no endpoint, and no `UseAuthentication`/`UseAuthorization`.
- Generation is public and limited to 10 per minute per client IP (`api/Program.cs:35-42`, `api/Maps/MapEndpoints.cs:15-17`). `app.UseRateLimiter()` runs where no user is known yet (`Program.cs:91`).
- `MapEndpointTests` runs every test against an unreachable database on purpose (`api.Tests/MapEndpointTests.cs:10-20`).
- The SPA has one route (`web/app/routes.ts:3`) and one API module (`web/app/api/client.ts`). `home.tsx` switches exhaustively over `GenerateError` (`:23-32`).
- The visual gate mocks only `**/api/maps/generate`; its server answers every other `/api/*` with 404 (`web/scripts/serve-spa.mjs:87`). 22 home baselines are committed.
- e2e starts the host in Production with no database (`e2e/playwright.config.ts:24-34`); the CI `e2e` job has none either (`.github/workflows/ci.yml:158-203`). Nothing outside the deploy job and the API test fixture applies migrations.

## Desired End State

- `POST /api/auth/register`, `POST /api/auth/login`, `POST /api/auth/logout` and `GET /api/auth/me` exist; login state is an HttpOnly cookie protected by the database key ring, so it survives a restart.
- `POST /api/maps/generate` answers 401 without a session and 429 after the 10th call in a minute by the same account; two accounts have separate budgets.
- Opening the app logged out lands on `/login`; `/register` creates an account and signs it in; the home view sits under a header showing the account email and a Log out button.
- `dotnet test api.Tests`, the web gates (`typecheck`, `ui:scan`, `visual`), and e2e in Chromium and Firefox pass, with e2e running against a real SQL Server locally and in CI.

Verify by running the automated criteria of phase 4 and its manual checks on the deployed app.

### Key Discoveries:

- An authenticated request must read the key ring from the database to decrypt its cookie, so "generation never touches the database" becomes "an anonymous request never touches the database" (`context/foundation/lessons.md`, "Only work that needs the database may touch it", already names this exception).
- Startup must stay free of configuration and I/O: the OpenAPI generator (`api/BattleMapGenerator.Api.csproj:8-12`) and the migrations bundle (`ci.yml:37-42`) both run `Program` without a connection string.
- From ASP.NET Core 10, cookie auth answers known API endpoints with 401/403 instead of a redirect (ASP.NET Core docs, `security/authentication/api-endpoint-auth`). The plan pins this with tests instead of trusting detection.
- In SPA mode a `clientLoader` is allowed on any route and `throw redirect("/login")` is the documented guard (React Router docs, `how-to/spa`, `start/framework/data-loading`).
- Playwright 1.63 documents a `setup` project that saves `storageState`, with browser projects depending on it (`docs/src/auth.md`).
- `TestServer` leaves the remote address null, so in API tests every per-IP limit shares the `"unknown"` bucket within one factory (`Program.cs:23-24`).

## What We're NOT Doing

- No stored or daily quota, and no migration. The limit is the in-memory limiter keyed by account; it resets when the app restarts.
- No email verification, password reset, password change, account deletion, two-factor login, external logins or bearer tokens. No `MapIdentityApi`.
- No roles. Every account has the same rights.
- No edits to `web/app/routes/home.tsx`, to the `Generate` handler in `api/Maps/MapEndpoints.cs`, or to the body of `e2e/tests/map.spec.ts`.
- No new `GenerateError` kind.
- No hand merge of `api/BattleMapGenerator.Api.json` or `web/app/api/schema.d.ts`. After any rebase onto S-03, regenerate both.
- No antiforgery tokens. The cookie is `SameSite=Lax` and every state-changing endpoint is a POST, which a cross-site page cannot send with the cookie.
- No change to `deploy.yml`. Setting `ASPNETCORE_FORWARDEDHEADERS_ENABLED` on the App Service, if the production check shows it is missing, is a manual step by the owner.

## Implementation Approach

Four phases, each leaving every CI job green. The API gets auth endpoints first while generation stays public. Then e2e gets its database and a saved session, which is harmless while generation is public. Only then is generation guarded, so `map.spec.ts` keeps passing unchanged. The login UI comes last. Between phases 3 and 4 the branch has a guarded API and no login page, so the pull request opens only after phase 4.

Decisions from planning:

| Decision | Choice |
| --- | --- |
| Generation limit | 10 per minute per account, fixed window, in memory |
| Register and login guard | Per-IP limit of 10 per minute across both, plus Identity lockout |
| Endpoints | Hand-written under `/api/auth` on `SignInManager` |
| 401 on generate mid-session | `client.ts` sends the browser to `/login` |
| Logout | Header row in the protected layout: account email and Log out |
| `client.ts` against S-03 | Auth calls live in a new `web/app/api/auth.ts`; `client.ts` changes by a few lines |
| e2e database | `compose.yaml` locally and in CI, schema through `dotnet ef database update` |
| Password rule | At least 8 characters, no composition rules |
| Register result | Signs the new account in |
| Duplicate email on register | 400 with a validation error naming the email as taken |
| Wrong password, unknown email, locked out | The same 401 for all three |
| Cookie | Persistent, 14 days sliding, HttpOnly, `SameSite=Lax`, `Secure` follows the request scheme |

## Critical Implementation Details

- **Middleware order.** `UseAuthentication`, then `UseAuthorization`, then `UseRateLimiter`. The limiter must see the user to partition by account, and an anonymous generate must be rejected with 401 before it spends a permit.
- **All Identity cookie schemes.** Register the cookies with `AddIdentityCookies()`, not only the application cookie: `SignInManager.SignOutAsync` also signs out the external and two-factor schemes, and a missing scheme may throw. The logout test in phase 1 proves it.
- **Auth-call budget in e2e.** Register and login share 10 calls per minute for one IP, and every e2e call comes from 127.0.0.1. A full run must stay at 6: the setup project makes at most 2, and the auth spec makes 2 per browser. Do not add a wrong-password case to e2e; it is covered by the API tests and the visual gate.

## Phase 1: API auth endpoints

### Overview

Add cookie authentication and the four auth endpoints, with their own per-IP limit. Generation stays public, so the SPA, the visual gate and e2e are unaffected.

### Changes Required:

#### 1. Auth services and pipeline

**File**: `api/Program.cs`

**Intent**: Turn the registered Identity services into a working cookie login, and guard the public auth endpoints against scripted use. Startup must still need no configuration and open no connection.

**Contract**:
- `AddIdentityCore<IdentityUser>` gains options: unique email required; password length at least 8 with digit, lowercase, uppercase and non-alphanumeric requirements off; lockout after 5 failed attempts for 5 minutes. Add `AddSignInManager()`.
- Authentication with `IdentityConstants.ApplicationScheme` as the default scheme and `AddIdentityCookies()`. Application cookie: HttpOnly, `SameSite=Lax`, `SecurePolicy=SameAsRequest`, 14 days, sliding expiration.
- `AddAuthorization()`.
- New rate-limit policy `auth`: fixed window, 10 permits per minute, no queue, partitioned by remote IP with the existing `"unknown"` fallback. The existing `OnRejected` (429 with `Retry-After`) applies to it.
- Pipeline: `UseAuthentication()` and `UseAuthorization()` directly before `UseRateLimiter()`.
- `app.MapAuthEndpoints()` next to `app.MapMapEndpoints()`, before the `/api` 404 fallback.
- Replace the comment "Identity services only; login endpoints come with S-04."

#### 2. Auth endpoints

**File**: `api/Auth/AuthEndpoints.cs` (new), `api/Auth/AuthModels.cs` (new)

**Intent**: The smallest contract the PRD needs: create an account, log in, log out, and ask who is logged in.

**Contract** (namespace `BattleMapGenerator.Api.Auth`, names set with `WithName` so the OpenAPI operation ids are stable):
- `POST /api/auth/register`, body `{ email, password }`, policy `auth`. Creates the user with the email as user name and signs it in with a persistent cookie. 200 with `AccountInfo { email }`. Identity errors (invalid email, short password, duplicate email) return 400 as a validation problem whose `errors` are keyed by `email` or `password`.
- `POST /api/auth/login`, body `{ email, password }`, policy `auth`. Password sign-in with `lockoutOnFailure: true` and a persistent cookie. 200 with `AccountInfo`. Wrong password, unknown email and a locked-out account all return 401 with no body.
- `POST /api/auth/logout`, no body, anonymous allowed. Signs out and returns 204, also when no one is logged in.
- `GET /api/auth/me`, requires authorization. 200 with `AccountInfo` for the caller's own account; 401 without a session.
- Declare the 400, 401 and 429 responses on the endpoints so they appear in the OpenAPI document.
- No endpoint returns or accepts another account's data.

#### 3. Logged-in client helper

**File**: `api.Tests/Infrastructure/ApiFactory.cs`

**Intent**: One place where tests obtain a client that is logged in, so existing and future tests do not repeat the register call.

**Contract**: `Task<HttpClient> CreateLoggedInClientAsync(string? email = null)` registers a unique account (`dm-<guid>@example.com` when no email is given) through `POST /api/auth/register` and returns the cookie-keeping client. Public constant for the test password. The factory must point at a migrated database. Each call costs one permit of the factory's `auth` bucket.

#### 4. Auth endpoint tests

**File**: `api.Tests/AuthEndpointTests.cs` (new, in `SqlServerCollection`)

**Intent**: Pin the contract above and the rules auth must not break.

**Contract**: each test builds its own `ApiFactory` on `sql.ConnectionString` (the `auth` bucket is per factory). Tests:
- register then `me` returns the registered email;
- register with an existing email, an invalid email and a 7-character password each return 400 with the matching `errors` key;
- login with the right password returns 200 and `me` then returns 200; wrong password and unknown email return an identical 401;
- the 6th login after 5 wrong passwords returns 401 even with the right password;
- logout returns 204 and `me` then returns 401; logout without a session returns 204;
- anonymous `me` returns 401 with no `Location` header and no SPA shell marker;
- the session cookie is HttpOnly and `SameSite=Lax`;
- a cookie issued by one factory is accepted by a second factory on the same database (restart simulation);
- two accounts each see only their own email from `me`;
- the 11th register-or-login call within a minute returns 429 with `Retry-After`;
- with an unreachable database, startup and an anonymous `me` answer 401 in under 5 seconds (not in the SQL collection's shared database; uses the unreachable connection string).

#### 5. Contract files and API docs

**File**: `api/BattleMapGenerator.Api.json`, `web/app/api/schema.d.ts`, `api/BattleMapGenerator.Api.http`, `api/AGENTS.md`

**Intent**: Keep the generated contract and the API notes in step with the new endpoints.

**Contract**: regenerate both contract files (`dotnet build api`, then `npm run api:types` in `web/`); never edit them by hand. Add register, login, `me` and logout requests to the `.http` file. In `api/AGENTS.md` add an "Authentication" section: the four routes, the cookie, the `auth` limit, and "only a request carrying a session cookie, register and login may touch the database".

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test api.Tests -c Release`
- The API builds without configuration and rewrites the OpenAPI document: `dotnet build api`
- Client types regenerate and the contract has no drift: `npm --prefix web run api:types && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts` (after committing the regenerated files)
- The migrations bundle still builds without a connection string: `dotnet tool restore && dotnet ef migrations bundle --project api --self-contained -r linux-x64 -o efbundle/efbundle --force`
- No new migration is pending: `dotnet ef migrations has-pending-model-changes --project api`
- Web still type-checks against the new schema: `npm --prefix web run typecheck`

#### Manual Verification:

- With the compose database running and `dotnet run` in `api/`, the requests in `BattleMapGenerator.Api.http` register an account, return it from `me`, and return 401 from `me` after logout

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review dm-email-login phase 1` before phase 2.

---

## Phase 2: e2e database and saved session

### Overview

Give e2e a SQL Server with the schema applied, locally and in CI, and log in once per run in a setup project that saves the session for both browsers. Generation is still public, so `map.spec.ts` passes either way; the setup project itself proves the session works.

### Changes Required:

#### 1. Database for e2e

**File**: `compose.yaml`, `e2e/scripts/db-up.mjs` (new), `e2e/package.json`

**Intent**: One definition of the database for development, local e2e and CI, and one command that makes it ready.

**Contract**:
- `compose.yaml`: add a `healthcheck` to `sqlserver` that runs a `SELECT 1` through the image's `sqlcmd`, so `docker compose up --wait` returns only when logins work.
- `db-up.mjs`, run from the repository root: `docker compose up -d --wait`, `dotnet tool restore`, then `dotnet ef database update --project api --connection <local connection string>`. The connection string is the one in `api/appsettings.Development.json:9`, exported from one shared module that `playwright.config.ts` also imports.
- `e2e/package.json`: script `db:up`.

#### 2. Host configuration and setup project

**File**: `e2e/playwright.config.ts`, `e2e/tests/auth.setup.ts` (new), `e2e/.gitignore`

**Intent**: Start the Production host with a database, and hand both browser projects one logged-in account without touching `map.spec.ts`.

**Contract**:
- `webServer.env` adds `ConnectionStrings__AppDb` with the shared local connection string.
- New project `setup` matching `*.setup.ts`. `chromium` and `firefox` get `dependencies: ["setup"]` and `storageState: "playwright/.auth/user.json"`.
- `auth.setup.ts` uses the `request` fixture, not a page: register the fixed account `e2e@example.com`; if that returns 400, log in instead; assert `GET /api/auth/me` returns that email; save `request.storageState({ path })`. At most 2 auth calls.
- `e2e/.gitignore`: add `/playwright/.auth/`.
- Update the config's header comment: a database is required; the generate limit is still described as it is until phase 3.

#### 3. CI

**File**: `.github/workflows/ci.yml`

**Intent**: The required `e2e` job gets the same database as a local run.

**Contract**: in job `e2e` (name unchanged), after dependencies are installed and before Playwright runs, add a step running `npm run db:up` in `e2e/`. Update the job's comment ("No database: ...") to say it runs against SQL Server from `compose.yaml`.

#### 4. e2e docs

**File**: `e2e/AGENTS.md`, `e2e/CLAUDE.md` if it lists commands

**Intent**: The rule "Production environment with no database" is no longer true.

**Contract**: replace it with: the host runs in Production against the compose SQL Server; run `npm run db:up` once before `npm test`; tests start logged in as `e2e@example.com` through `tests/auth.setup.ts`; a spec that needs a logged-out browser resets `storageState` itself.

### Success Criteria:

#### Automated Verification:

- The database comes up healthy and migrated: `npm --prefix e2e run db:up`
- e2e specs type-check: `npm --prefix e2e run typecheck`
- e2e passes in both browsers with the setup project: `npm --prefix e2e run build:app && npm --prefix e2e test`
- A second run straight after also passes (the account already exists, so setup takes the login path): `npm --prefix e2e test`
- The saved session file is ignored: `git status --short e2e/playwright` prints nothing
- `ci.yml` parses: `python3 -c "import yaml,sys; yaml.safe_load(open('.github/workflows/ci.yml'))"`

#### Manual Verification:

- After pushing the branch, the `e2e` job run by `workflow_dispatch` on the branch is green and its log shows the setup project passing

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review dm-email-login phase 2` before phase 3.

---

## Phase 3: Guard generation, limit per account

### Overview

Require a session for generation and key its limit by account. The API tests for generation move onto the logged-in client; e2e keeps passing because phase 2 gave it a session.

### Changes Required:

#### 1. Route chain

**File**: `api/Maps/MapEndpoints.cs`

**Intent**: Refuse anonymous generation, also for callers that skip the UI.

**Contract**: add `.RequireAuthorization()` to the `MapPost("/api/maps/generate", ...)` chain. The only other edit in this file is the comment above `MapMapEndpoints` ("Public until S-04 adds login..."), which becomes false; reword it to say generation requires a session and is limited per account. The `Generate` handler and `NewSeed` are not touched.

#### 2. Limiter keyed by account

**File**: `api/Program.cs`

**Intent**: One account gets one budget wherever it connects from, and accounts do not share a budget behind one IP.

**Contract**: the `generate` policy's partition key becomes the caller's `ClaimTypes.NameIdentifier`, with `"anonymous"` as the fallback (unreachable while authorization runs first, kept so a null key can never throw). Limit and window stay 10 per minute, no queue. Update the comment block at `Program.cs:22-25`.

#### 3. Generation tests

**File**: `api.Tests/MapEndpointTests.cs`

**Intent**: Keep every existing assertion about generation, now for a logged-in account, and pin the new access rule.

**Contract**: the class joins `SqlServerCollection`; tests that generate use `CreateLoggedInClientAsync` on `sql.ConnectionString`. Changes per test:
- the grid test and the empty-body test: logged-in client, assertions unchanged;
- `The_eleventh_generate_within_a_minute_returns_429_with_retry_after`: same account, and afterwards a second account in the same factory still gets 200;
- `Generate_succeeds_quickly_with_an_unreachable_database` is replaced by: with an unreachable database, startup and an anonymous generate answer 401 in under 5 seconds;
- new: anonymous generate, with a seed and with an empty body, returns 401 with no `Location` header and no SPA shell marker, and does not count against any account;
- `Unknown_maps_paths_return_404` stays as it is, anonymous and without a database.

Update the class comment, which says generation never needs the database.

#### 4. e2e access check

**File**: `e2e/tests/access.spec.ts` (new)

**Intent**: Prove on the deployed shape that a request without a session cannot generate.

**Contract**: one test using a fresh request context with empty storage state: `POST /api/maps/generate` returns 401. It makes no auth call and spends no generate permit.

#### 5. Docs that name the limit

**File**: `api/AGENTS.md`, `api/BattleMapGenerator.Api.http`, `e2e/AGENTS.md`, `e2e/playwright.config.ts`, `context/foundation/infrastructure.md`, `context/foundation/lessons.md`

**Intent**: Every place that says "10 per minute per IP" or "public" describes the new rule.

**Contract**:
- `api/AGENTS.md:18`: 10 per minute per account, 401 without a session; forwarded headers now matter for the `auth` limit and the cookie's `Secure` flag.
- `.http` file: the generate request's comment; it now needs the login request run first.
- `e2e/AGENTS.md:10` and the config comment: the e2e account makes 4 generations per run out of 10; register and login share 10 per minute per IP and a run uses at most 6.
- `infrastructure.md:165`: the "In place" note for the quota risk.
- `lessons.md`, "Only work that needs the database may touch it": append that S-04 landed the exception (requests carrying a session cookie, register and login) and that anonymous requests, startup and liveness stay database-free. Append only; do not rewrite the lesson.

If regenerating the contract after this phase changes `BattleMapGenerator.Api.json` or `schema.d.ts`, commit the regenerated files.

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test api.Tests -c Release`
- The contract has no drift after a rebuild: `dotnet build api && npm --prefix web run api:types && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts`
- e2e passes in both browsers, `map.spec.ts` unchanged: `npm --prefix e2e run build:app && npm --prefix e2e test`
- `map.spec.ts` has no diff against `main`: `git diff --exit-code main -- e2e/tests/map.spec.ts`
- The `Generate` handler has no diff against `main`: `git diff main -- api/Maps/MapEndpoints.cs` shows only the added `.RequireAuthorization()` line and the reworded comment

#### Manual Verification:

- With the API running locally, `curl -i -X POST http://localhost:5108/api/maps/generate -H 'Content-Type: application/json' -d '{}'` prints `401` and no `Set-Cookie` or `Location`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase. Then run `/10x-impl-review dm-email-login phase 3` before phase 4.

---

## Phase 4: Login UI

### Overview

Add the login and register pages, put the home view behind a layout route that checks the session, and add the header with logout. Extend the visual gate and e2e to cover them.

### Changes Required:

#### 1. API modules

**File**: `web/app/api/auth.ts` (new), `web/app/api/client.ts`

**Intent**: Keep auth calls out of the file S-03 edits, and send the DM to the login page when a session ends mid-use.

**Contract**:
- `client.ts`: export the `openapi-fetch` client so `auth.ts` shares it. In `generateMap`, a 401 response calls `window.location.assign("/login?expired=1")` (phase 3 review F3: a 401 can also come from a database that was still resuming, so the login page shows "Your session ended, or the server was waking up. Please log in again." when `expired` is set) and returns the existing `{ ok: false, error: { kind: "http", status: 401 } }`. `GenerateError`, `GenerateResult` and the function signature do not change. These are the only edits; if S-03 has merged first, rebase and reapply them around its changes.
- `auth.ts`: `getSession()` returning the account or `null` on 401 and throwing on anything else; `register(email, password)`, `login(email, password)` and `logout()` returning result unions. Error kinds: `invalid-credentials` (login 401), `validation` with per-field messages (register 400), `rate-limited` (429), `network`, `http` with status.

#### 2. Routes

**File**: `web/app/routes.ts`, `web/app/routes/protected.tsx` (new), `web/app/routes/login.tsx` (new), `web/app/routes/register.tsx` (new)

**Intent**: A logged-out visitor sees only login and register; a logged-in one never sees them. `home.tsx` is not edited.

**Contract**:
- `routes.ts`: `layout("routes/protected.tsx", [index("routes/home.tsx")])`, `route("login", "routes/login.tsx")`, `route("register", "routes/register.tsx")`.
- `protected.tsx`: `clientLoader` calls `getSession()`; `null` throws `redirect("/login")`; an account is returned as loader data. A `HydrateFallback` renders an empty `main` with the page background so nothing flashes. The component renders a header row (app-wide, above the outlet) with the account email and a "Log out" button, then `<Outlet />`. Log out calls `logout()` and navigates to `/login`.
- `login.tsx` and `register.tsx`: each has a `clientLoader` that redirects to `/` when `getSession()` returns an account. A form with labelled "Email" and "Password" fields and a submit button named "Log in" or "Create account"; a link to the other page. While submitting, the button is disabled and reads "Logging in…" or "Creating account…". Errors show in an `Alert`: invalid credentials, the API's validation messages, rate-limited, network, and a generic HTTP error. (Phase 1 review, F7 and F8: any 5xx reads "The server is not ready yet. Try again in a minute.", since a waking database surfaces as a 500; and a 400 without an `errors` field, which a malformed body gets, falls back to the generic HTTP error.) Register shows the hint "At least 8 characters". Success navigates to `/`. Each route exports `meta` with a title.
- Accessible names above are the contract e2e locates by; keep them exact.

#### 3. UI primitives and preview height

**File**: `web/app/components/ui/input.tsx` (new), `web/app/components/ui/label.tsx` (new), `web/app/app.css`

**Intent**: Forms built from the design system, and a preview that still fits the viewport under the new header.

**Contract**: add `Input` and `Label` the way `web/AGENTS.md:26-29` describes (`npx shadcn@latest add input label`, then review the `app.css` diff and remove any `.dark` block or `@custom-variant`). If S-03 has already added either file, reuse it. Raise the fixed offset in `map-preview-fit` (`app.css:86-88`) by the header's height, and update the comment above it to name the header as one of the rows. No literal colours, palette classes, `dark:` classes or arbitrary values in the views.

#### 4. Visual gate

**File**: `web/visual/home.visual.spec.ts`, `web/visual/auth.visual.spec.ts` (new), `web/visual/__screenshots__/**`

**Intent**: The home baselines keep pinning the home view (now with its header), and the new pages get baselines.

**Contract**: the home spec answers `**/api/auth/me` with a fixed account for every test (in the shared fixture, next to the font routes). Regenerate the 22 home baselines with `npm run visual:update`. The new spec, with `me` answering 401, pins in all three projects: login idle, login with the invalid-credentials error, register idle, register with a validation error (12 new baselines). Share the hermetic-font fixture between the two specs instead of copying it.

#### 5. e2e auth flow

**File**: `e2e/tests/auth.spec.ts` (new)

**Intent**: Prove the whole access flow on the deployed shape in both browsers.

**Contract**: `test.use({ storageState: { cookies: [], origins: [] } })`. One test per browser: open `/` and land on `/login`; go to register, create a unique account (`dm-<timestamp>-<project>@example.com`), land on the home view with that email in the header; log out and land on `/login`; log in with the same account and see the Generate button. Exactly 2 auth calls per browser. No generate call.

#### 6. Docs

**File**: `web/AGENTS.md`, `e2e/AGENTS.md`, `AGENTS.md`

**Intent**: The next agent knows where auth lives and which rules the pages follow.

**Contract**: `web/AGENTS.md`: a short "Access" section (protected pages go inside the `protected.tsx` layout; auth calls live in `app/api/auth.ts`; a row added to the layout header counts against `map-preview-fit`), and the visual gate line now names both specs. `e2e/AGENTS.md`: list `auth.spec.ts` and the auth-call budget. Root `AGENTS.md`: no change unless a statement there became false.

### Success Criteria:

#### Automated Verification:

- Web type-checks and builds: `npm --prefix web run typecheck && npm --prefix web run build`
- No hardcoded UI values: `npm --prefix web run ui:scan`
- Web rendering tests still pass: `npm --prefix web test`
- The visual gate passes with the regenerated and new baselines: `npm --prefix web run visual:docker`
- e2e passes in both browsers, including `auth.spec.ts`: `npm --prefix e2e run build:app && npm --prefix e2e test`
- `home.tsx` and `map.spec.ts` have no diff against `main`: `git diff --exit-code main -- web/app/routes/home.tsx e2e/tests/map.spec.ts`
- API tests and the contract check still pass: `dotnet test api.Tests -c Release && dotnet build api && npm --prefix web run api:types && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts`

#### Manual Verification:

- Locally in Chrome and Firefox: register, generate, download, log out, log in again; the header and both forms read correctly in light and dark mode and at phone width
- Locally: with devtools, delete the session cookie on the home view and click Generate; the browser lands on `/login`
- The regenerated home baselines differ from the old ones only by the header and the preview's size
- On the deployed app after merge: register and log in work; the `Set-Cookie` response header carries `Secure` and `HttpOnly` (if `Secure` is missing, the owner sets `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` on the App Service)
- On the deployed app: after `az webapp restart`, reloading the page keeps the DM logged in
- On the deployed app: `curl -i -X POST https://<app>/api/maps/generate -H 'Content-Type: application/json' -d '{}'` prints `401`

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the local manual testing was successful. Then run `/10x-impl-review dm-email-login phase 4` before opening the pull request. The three deployed-app checks are done after the merge and closed in the closeout pull request.

---

## Testing Strategy

### Unit Tests:

- None new. The auth logic is Identity's; what is ours is wiring, and that is tested through the hosted app.

### Integration Tests:

- `api.Tests/AuthEndpointTests.cs`: the endpoint contract, lockout, the `auth` limit, cookie flags, session across a restart, account isolation, and the database-free anonymous path.
- `api.Tests/MapEndpointTests.cs`: generation for a logged-in account, 401 for anonymous, separate budgets per account.
- `web/visual/`: home with header, login and register states, three projects.
- `e2e/`: saved session (`auth.setup.ts`), generate and download unchanged (`map.spec.ts`), anonymous API call refused (`access.spec.ts`), register, logout and login through the UI (`auth.spec.ts`), in Chromium and Firefox.

### Manual Testing Steps:

1. Run the local flow in both browsers, both themes and at phone width (phase 4).
2. Delete the session cookie and click Generate (phase 4).
3. After the merge, run the three deployed-app checks (phase 4).

## Performance Considerations

- Register and login hash a password, which is CPU on the F1 plan; the `auth` limit bounds it per IP.
- The first request with a session cookie in a process loads the key ring from the database, and Identity revalidates the security stamp periodically. After an auto-pause, that request or a login waits for the database to resume (about a minute, up to three). The forms stay in their submitting state and show a network or HTTP error if the retry window runs out.
- Anonymous requests, startup and liveness still open no connection; two tests pin it.

## Migration Notes

- No schema change and no data to migrate; production has no accounts yet.
- Merge order with S-03: whichever branch merges second rebases, reruns `dotnet build api` and `npm run api:types`, and commits the regenerated contract files. If S-03 merges first, reapply the `client.ts` edits from phase 4 around its changes and reuse any `Input`/`Label` it added.
- Rollback is redeploying the previous artifact. Accounts created meanwhile stay in the database and are harmless to the older build.

## References

- Related research: `context/changes/dm-email-login/research.md`
- Brief and constraints: `context/changes/dm-email-login/change.md`
- Roadmap item: `context/foundation/roadmap.md:127-138`
- Account store this builds on: `context/archive/2026-09-23-account-store-foundation/plan.md`
- Test patterns to follow: `api.Tests/HealthProbeTests.cs`, `api.Tests/Infrastructure/ApiFactory.cs:35-40`
- Visual gate pattern: `web/visual/home.visual.spec.ts`
- ASP.NET Core docs: `security/authentication/api-endpoint-auth`, `performance/rate-limit`
- React Router docs: `how-to/spa`, `start/framework/data-loading`
- Playwright 1.63 docs: `docs/src/auth.md`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: API auth endpoints

#### Automated

- [x] 1.1 API tests pass — d6adc22
- [x] 1.2 The API builds without configuration and rewrites the OpenAPI document — d6adc22
- [x] 1.3 Client types regenerate and the contract has no drift — d6adc22
- [x] 1.4 The migrations bundle still builds without a connection string — d6adc22
- [x] 1.5 No new migration is pending — d6adc22
- [x] 1.6 Web still type-checks against the new schema — d6adc22

#### Manual

- [x] 1.7 The requests in the .http file register an account, return it from me, and return 401 from me after logout — d6adc22

### Phase 2: e2e database and saved session

#### Automated

- [x] 2.1 The database comes up healthy and migrated — d6adc22
- [x] 2.2 e2e specs type-check — d6adc22
- [x] 2.3 e2e passes in both browsers with the setup project — d6adc22
- [x] 2.4 A second run straight after also passes — d6adc22
- [x] 2.5 The saved session file is ignored — d6adc22
- [x] 2.6 ci.yml parses — d6adc22

#### Manual

- [x] 2.7 The e2e job run on the branch is green and its log shows the setup project passing — d6adc22

### Phase 3: Guard generation, limit per account

#### Automated

- [x] 3.1 API tests pass — d6adc22
- [x] 3.2 The contract has no drift after a rebuild — d6adc22
- [x] 3.3 e2e passes in both browsers, map.spec.ts unchanged — d6adc22
- [x] 3.4 map.spec.ts has no diff against main — d6adc22
- [x] 3.5 The Generate handler has no diff against main — d6adc22

#### Manual

- [x] 3.6 A local curl to generate without a session prints 401 and no Set-Cookie or Location — d6adc22

### Phase 4: Login UI

#### Automated

- [x] 4.1 Web type-checks and builds — d6adc22
- [x] 4.2 No hardcoded UI values — d6adc22
- [x] 4.3 Web rendering tests still pass — d6adc22
- [x] 4.4 The visual gate passes with the regenerated and new baselines — d6adc22
- [x] 4.5 e2e passes in both browsers, including auth.spec.ts — d6adc22
- [x] 4.6 home.tsx and map.spec.ts have no diff against main — d6adc22
- [x] 4.7 API tests and the contract check still pass — d6adc22

#### Manual

- [x] 4.8 Locally in Chrome and Firefox: register, generate, download, log out, log in again, in both themes and at phone width — d6adc22
- [x] 4.9 Locally: deleting the session cookie and clicking Generate lands on /login — d6adc22
- [x] 4.10 The regenerated home baselines differ only by the header and the preview's size — d6adc22
- [x] 4.11 On the deployed app: register and log in work and the cookie carries Secure and HttpOnly — verified against production 2026-10-02 (curl; a first register attempt hit a 500 from the Azure SQL free-tier database resuming from auto-pause, a retry 2 minutes later returned 200 with `Secure; HttpOnly; SameSite=Lax`)
- [x] 4.12 On the deployed app: the session survives az webapp restart — verified against production 2026-10-02 (`az webapp restart`; the saved cookie kept authenticating after the restart, once the Azure SQL database resumed from auto-pause)
- [x] 4.13 On the deployed app: generate without a session prints 401 — verified against production 2026-10-02 (curl; 401, no Set-Cookie or Location)
