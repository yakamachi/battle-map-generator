---
date: 2026-09-30T16:15:43+02:00
researcher: Claude (Opus 5.5) for Karol Mitek
git_commit: 2fc7bda142eba3031df5a7447f07afb11a9677cb
branch: feat/dm-email-login
repository: yakamachi/battle-map-generator
topic: "S-04 dm-email-login: what the codebase already has for email/password login, guarding generation, a per-account limit, and the tests that must follow"
tags: [research, codebase, auth, identity, rate-limiter, react-router, e2e, ci]
status: complete
last_updated: 2026-09-30
last_updated_by: Claude (Opus 5.5)
---

# Research: S-04 dm-email-login

**Date**: 2026-09-30T16:15:43+02:00
**Researcher**: Claude (Opus 5.5) for Karol Mitek
**Git Commit**: 2fc7bda142eba3031df5a7447f07afb11a9677cb
**Branch**: feat/dm-email-login
**Repository**: yakamachi/battle-map-generator

## Research Question

Roadmap slice S-04 (`context/foundation/roadmap.md:127-138`): the DM registers (open registration, no email verification) and logs in with email and password; a logged-out user sees only login/register and cannot generate; generations per account are limited; the full flow works in Chrome and Firefox.

The brief in `change.md` adds constraints for running in parallel with S-03. This research answers: what exists today at each point the brief names, what each constraint collides with, and which decisions are left for `/10x-plan`.

Method: every file named below was read in full in the main session (the repo is small, so no sub-agents were used). Library behaviour marked **[docs]** comes from Context7 (ASP.NET Core docs, React Router docs, Playwright 1.63 docs). Behaviour marked **[unverified]** is from general knowledge and must be confirmed during planning or by a test.

## Summary

- **The account store is ready and needs no migration.** Identity tables and the key ring exist (`api/Migrations/20260923154324_InitialAccountStore.cs`), `UserManager` works end to end (`api.Tests/AccountStoreTests.cs:54-76`). What is missing is everything after storage: a cookie scheme, `SignInManager`, endpoints, and `UseAuthentication`/`UseAuthorization` in the pipeline (`api/Program.cs:62-64`, `:90-91`).
- **Guarding generation makes the generate path depend on the database**, which reverses what four tests pin today. `MapEndpointTests` runs every test against an unreachable database (`api.Tests/MapEndpointTests.cs:10-20`). An authenticated request must read the Data Protection key ring from the database to decrypt its cookie, so these tests have to move to the SQL Server collection and a logged-in client. `lessons.md` already names this exception ("after S-04, auth cookies through Data Protection").
- **The rate limiter runs before authentication would.** `app.UseRateLimiter()` is at `Program.cs:91` and partitions by remote IP (`:35-42`). A per-account partition needs `HttpContext.User`, so the limiter must move after `UseAuthentication` (and after `UseAuthorization` if anonymous calls should get 401 without spending a permit).
- **e2e has no database anywhere**: not in `playwright.config.ts` (`:24-34`), not in the `e2e` CI job (`ci.yml:156-203`), and nothing applies migrations outside the deploy job and the API test fixture. Login in e2e needs a SQL Server, a connection string for the Production-environment host, and a migration step.
- **Three collisions with the brief's "don't touch" list** that the plan must design around:
  1. Adding a new `GenerateError` kind in `client.ts` breaks `home.tsx`'s exhaustive `switch` (`home.tsx:23-32`), which the brief says not to edit.
  2. A layout route that checks the session makes the visual gate's home tests fail, because `serve-spa.mjs` answers every `/api/*` with 404 (`web/scripts/serve-spa.mjs:87`) and the spec mocks only `**/api/maps/generate`.
  3. Any row the layout adds above the preview (for example a header with a logout button) changes the home baselines and the height budget in `map-preview-fit` (`web/app/app.css:86-88`, note at `home.tsx:114-115`).
- **The S-03 branch has no commits yet** (`git log main..feat/encounter-parameters` is empty at research time), so the overlap is predicted from the roadmap, not observed.

## Detailed Findings

### API: what exists for auth

- `AddIdentityCore<IdentityUser>().AddEntityFrameworkStores<AppDbContext>()` with the comment "login endpoints come with S-04" (`api/Program.cs:62-64`). `AddIdentityCore` registers `UserManager` but no authentication scheme and no `SignInManager` **[unverified, standard Identity behaviour]**.
- No `UseAuthentication`, `UseAuthorization`, `AddAuthentication` or `AddAuthorization` call exists in `api/Program.cs` (whole file read, 158 lines).
- `AppDbContext` is `IdentityDbContext<IdentityUser>` plus `DataProtectionKeys` (`api/Data/AppDbContext.cs:10-14`). The initial migration creates `AspNetUsers`, the other six Identity tables and `DataProtectionKeys`. S-04 needs a new migration only if it adds a column or table (for example a persisted generation counter).
- Data Protection keys persist to the database under a fixed application name (`Program.cs:68-70`), and the preloading hosted service is removed so startup never reads the database (`:72-76`). The auth cookie will be protected by this key ring; the first request that reads or writes a cookie in a process loads it.
- The connection string is resolved lazily so the host builds without one (`Program.cs:45-60`). Two build-time consumers rely on startup needing no configuration: the OpenAPI document generator (`api/BattleMapGenerator.Api.csproj:8-12`) and the EF migrations bundle (`ci.yml:37-42`, `deploy.yml:48-52`). Auth registration must keep that true.
- Routing rules to preserve: every API route under `/api`, `MapFallback("/api/{**rest}")` returning 404 before the SPA fallback (`Program.cs:133-138`, lesson "Unknown /api routes return 404").

### API: the generate endpoint and its limiter

- Route chain today: `MapPost("/api/maps/generate", Generate).WithName("GenerateMap").RequireRateLimiting(GenerateRateLimitPolicy)` (`api/Maps/MapEndpoints.cs:15-17`). The brief allows exactly one change here: add `.RequireAuthorization()` to this chain. The comment above it ("Public until S-04 adds login", `:11-12`) will be stale; whether editing a comment counts as "leave the handler alone" is for the plan to state. The handler itself (`:21-25`) is where S-03 will add parameters.
- Policy `generate`: fixed window, 10 permits per minute, no queue, partition key `RemoteIpAddress ?? "unknown"` (`Program.cs:35-42`). Rejection is 429 with `Retry-After` (`:28-34`).
- The limiter is in memory and per process. On F1 an idle unload or restart resets every count (`infrastructure.md:93` describes the 20-minute idle unload).
- **[docs]** ASP.NET Core's rate-limit docs partition by `httpContext.User.Identity?.Name ?? "anonymous"`; `UseRateLimiter` must come after `UseRouting` for endpoint policies. The app calls no explicit `UseRouting`, so its position relative to auth middleware is set only by the order of the `Use*` calls at `Program.cs:90-91`.
- **[docs]** From ASP.NET Core 10, known API endpoints with cookie auth return 401/403 instead of a 302 to a login page; `DisableCookieRedirect()` exists for endpoints not detected automatically. The plan should pin "anonymous generate returns 401, not 302 and not the SPA shell" with a test rather than rely on detection.
- The stale references a per-account limit must update: `api/AGENTS.md:18`, `api/BattleMapGenerator.Api.http:17`, `e2e/AGENTS.md:10`, `e2e/playwright.config.ts:8-10`, `e2e/tests/map.spec.ts:82` (a comment; the brief says not to rewrite this file), `infrastructure.md:165`, `web/app/routes/home.tsx:26` (message text "wait a minute", not editable under the brief, so a window other than one minute makes it wrong).

### API: endpoint options (decision for the plan)

- **[docs]** `AddIdentityApiEndpoints<TUser>()` + `MapGroup(...).MapIdentityApi<TUser>()` gives `/register` and `/login?useCookies=true` out of the box. It has no logout endpoint; the docs add one by hand with `SignOutAsync`.
- **[unverified]** `MapIdentityApi` also maps `/refresh`, `/confirmEmail`, `/resendConfirmationEmail`, `/forgotPassword`, `/resetPassword`, `/manage/2fa` and `/manage/info`, and registers a bearer-token scheme next to the cookie. Email verification and password reset are PRD non-goals (`prd.md:199`), so these would be unused public surface, and all of them land in `BattleMapGenerator.Api.json` and `schema.d.ts`.
- The alternative is three or four hand-written minimal endpoints (`register`, `login`, `logout`, `me`) on `SignInManager`, with `AddIdentityCore` + `AddSignInManager` + the Identity application cookie. Smaller contract, more code to test.
- Either way the SPA needs a "who am I" call for the layout route; `MapIdentityApi` offers only `/manage/info` for that **[unverified]**.
- Identity's default password rules (digit, upper, lower, symbol, length 6) and duplicate-email errors apply unless configured **[unverified]**. A duplicate-email error on register tells a caller that the address has an account; that touches the NFR "account data invisible to other accounts" and the plan should decide the response.

### API tests

- `ApiFactory` hosts the app in the `Testing` environment with settings supplied last through in-memory configuration (`api.Tests/Infrastructure/ApiFactory.cs:24-33`) and offers `CreateReadyClient()` (`:35-40`). This is where the brief puts the logged-in client helper. `WebApplicationFactory.CreateClient()` keeps cookies by default **[unverified]**, so a helper that registers and logs in can return a client that stays logged in.
- `SqlServerFixture` starts one container per run, migrates a shared `battlemap` database and can create fresh ones (`api.Tests/Infrastructure/SqlServerFixture.cs:20-44`). Tests that count keys take their own database (lesson "API tests control their own configuration").
- `MapEndpointTests` is not in the SQL Server collection and has six test cases across five methods that all use `UnreachableConnectionString` (`api.Tests/MapEndpointTests.cs:14-20`):
  - `Generate_returns_the_default_size_grid…` (`:22-51`), `Generate_with_an_empty_body_or_no_seed…` (`:55-70`): need a logged-in client and a reachable database.
  - `The_eleventh_generate_within_a_minute_returns_429…` (`:72-89`): pins 10 per minute; must follow the new limit and prove that two accounts have separate budgets.
  - `Generate_succeeds_quickly_with_an_unreachable_database` (`:92-103`): cannot hold for an authenticated request. What survives is "startup and an anonymous request answer quickly (401) with the database unreachable".
  - `Unknown_maps_paths_return_404` (`:106-118`): unaffected by auth if the fallback stays unauthenticated.
- Registered users accumulate in the shared database; tests already use unique emails (`AccountStoreTests.cs:57`).

### Web

- One route: `export default [index("routes/home.tsx")]` (`web/app/routes.ts:3`). SPA mode, `ssr: false` (`web/react-router.config.ts:6`). `root.tsx` renders `<Outlet />` and an `ErrorBoundary`; it has no `HydrateFallback` (`web/app/root.tsx:47-82`).
- **[docs]** In SPA mode, `clientLoader` is allowed on any route and `HydrateFallback` renders while it runs; a server `loader` is allowed only on the root route. `throw redirect("/login")` from a loader is the documented guard. So a layout route with a `clientLoader` that asks the API for the session fits the brief without touching `home.tsx`.
- `client.ts` builds an `openapi-fetch` client with relative URLs (`web/app/api/client.ts:17`), so cookies on the same origin are sent without extra options. Dev uses the Vite proxy to `http://localhost:5108` (`web/vite.config.ts:12-14`); the cookie is then set for the Vite origin, which works because the browser only sees one origin **[unverified for `Secure`/`SameSite` defaults over plain http]**.
- `GenerateError` is a three-member union consumed by an exhaustive `switch` with no default in `home.tsx:23-32`. Adding a member in `client.ts` makes `errorMessage` fail type-checking. A session that expires mid-use (401 on generate) therefore has to be handled without a new kind: inside the client (redirect to `/login`), or as the existing `{ kind: "http", status: 401 }`, which shows "The server could not generate a map (error 401)".
- UI rules for the new pages (`web/AGENTS.md:25-30`): tokens only, components from `app/components/ui`, which today holds `alert.tsx` and `button.tsx`. `Input` and `Label` were deliberately left for S-03/S-04 (`context/archive/2026-09-29-home-view-ui-contract/plan.md:99`). S-03 will likely add form primitives too, so both branches may add the same files under `components/ui/`.
- Visual gate: `web/visual/home.visual.spec.ts` mocks `**/api/maps/generate` only, against `serve-spa.mjs`, where every other `/api/*` is a 404. A session check in the layout would send all 22 committed home baselines (8 desktop-light, 8 desktop-dark, 6 mobile in `web/visual/__screenshots__/`) to the login page unless the spec mocks the session call. New login/register views have no baselines yet.

### e2e and CI

- `playwright.config.ts` starts `dotnet run --project ../api --no-launch-profile` in the Production environment with no connection string (`e2e/playwright.config.ts:24-34`) and has two projects, chromium and firefox (`:20-23`).
- `map.spec.ts` starts with `page.goto("/")` and clicks Generate (`e2e/tests/map.spec.ts:83-89`). With a saved session it runs unchanged; it makes 2 generations per browser, 4 per run, as one account if both projects share one `storageState`.
- **[docs]** Playwright 1.63: a `setup` project matching `*.setup.ts` logs in and calls `page.context().storageState({ path })`; browser projects declare `dependencies: ['setup']` and `use.storageState`. The docs put the file in `playwright/.auth/` and add it to `.gitignore`; `e2e/.gitignore` has no such entry.
- The setup needs an account. Registration is open, so it can register through the UI or API; on a reused local server or a persistent local database the second run meets "already registered", so the step must be idempotent (unique email per run, or register-then-login tolerating a duplicate).
- The limit must hold 4 generations per run for that account. With `reuseExistingServer` locally (`playwright.config.ts:30`), quick reruns add up within one window, as the config comment already warns (`:9-10`).
- CI `e2e` job (`ci.yml:158-203`): setup-dotnet, setup-node, install, typecheck, `build:app`, Playwright. No service container, no migration. The `api` job gets SQL Server from Testcontainers inside `dotnet test` (`ci.yml:31-33`), which does not help a `dotnet run` host.
- Building blocks already in the repo for giving e2e a database: the image `mcr.microsoft.com/mssql/server:2022-latest` (`compose.yaml:5`, `SqlServerFixture.cs:13`), a local-only SA password (`compose.yaml:8`, `api/appsettings.Development.json:9`), `dotnet-ef` 10.0.12 as a local tool (`.config/dotnet-tools.json`), and the bundle command (`ci.yml:41`). The app never migrates at startup, so CI must run `dotnet ef database update` or the bundle before Playwright. Configuration reaches a Production host through `ConnectionStrings__AppDb` in `webServer.env`.
- The job name `e2e` is a required check (`context/changes/e2e-root-package/change.md`, "same name, so it's still a required check"); keep the name.
- `contract` job fails on drift in `api/BattleMapGenerator.Api.json` and `web/app/api/schema.d.ts` (`ci.yml:116-126`). Both are regenerated by `dotnet build api` and `npm run api:types` (`web/package.json:10`).

### Production

- The deployed app already has the database, its managed-identity connection and migrations applied on each deploy (`deploy.yml:130-141`). With no new migration, S-04 deploys as code only.
- A first login after the database auto-paused waits for the resume; `EnableRetryOnFailure` (`Program.cs:54-58`) retries, and the deploy notes measured about 3 minutes for a resume (`deploy.yml:126-129`) against EF's roughly 1 minute of retries. A login or register call can therefore fail or take long after a pause; the UI needs a state for that.
- Behind App Service TLS ends at the front end. `api/AGENTS.md:18` says client IP needs `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; the same setting decides whether the app sees the request as https, which a `Secure` cookie policy depends on **[unverified whether the setting is on in production; no file in the repo sets it]**.
- Every authenticated request after S-04 can touch the database (key ring on first use per process; Identity's periodic security-stamp check **[unverified, default 30 minutes]**), which spends free-offer vCore-seconds that public generation did not.

## Code References

- `api/Program.cs:26-43` - rate limiter, per-IP `generate` policy
- `api/Program.cs:62-64` - Identity services only, no scheme or endpoints
- `api/Program.cs:68-76` - Data Protection in the database, lazy key ring
- `api/Program.cs:90-91` - pipeline position where auth middleware and the limiter order are decided
- `api/Program.cs:131-138` - map endpoints, `/api` 404 guard, SPA fallback
- `api/Maps/MapEndpoints.cs:15-17` - the route chain that gets `.RequireAuthorization()`
- `api.Tests/Infrastructure/ApiFactory.cs:35-40` - `CreateReadyClient`, the model for a logged-in client helper
- `api.Tests/MapEndpointTests.cs:14-20,72-103` - unreachable-database setup and the two tests whose premise changes
- `web/app/routes.ts:3` - single index route
- `web/app/api/client.ts:7-10,29-33` - `GenerateError` and status mapping
- `web/app/routes/home.tsx:23-32,114-115` - exhaustive switch; rows above the preview
- `web/app/app.css:86-88` - `map-preview-fit` height budget
- `web/visual/home.visual.spec.ts` (`GENERATE` constant) - the only mocked API route
- `e2e/playwright.config.ts:20-34` - projects and the database-less web server
- `.github/workflows/ci.yml:158-203` - `e2e` job
- `.github/workflows/ci.yml:116-126` - contract drift check
- `compose.yaml:4-12` - local SQL Server

## Architecture Insights

- Startup must stay configuration-free and database-free; three things depend on it (OpenAPI generation at build, the migrations bundle, the cold-start lesson). Auth adds services at startup but must add no startup I/O.
- The repo treats "which requests may touch the database" as a tested rule. S-04 widens the allowed set from "ready probe" to "anything carrying an auth cookie, plus register/login"; liveness, startup and anonymous requests stay outside it.
- The contract files are generated artefacts with a CI drift gate. In a parallel branch pair, the second branch to merge rebases and regenerates both files; a textual merge of either is never the answer (brief, and `api/AGENTS.md:19`).
- Web changes are gated three ways: `ui:scan`, the screenshot gate, and e2e locators by role and text. New pages must satisfy the first, extend the second and give the third stable accessible names.

## Historical Context (from prior changes)

- `context/archive/2026-09-23-account-store-foundation/plan.md:34` - F-01 explicitly left out login, registration, logout, cookie authentication and endpoints for S-04. Supported by the code today.
- `context/archive/2026-09-25-first-map-download/plan.md:52` - generation public until S-04, the rate limit as the only guard. Supported (`MapEndpoints.cs:11-17`).
- `context/archive/2026-09-29-home-view-ui-contract/plan.md:99` - no Input/Label/Select yet; S-03 and S-04 add them through the rule in `web/AGENTS.md`. Supported (`components/ui` holds two files).
- `context/foundation/roadmap.md:136` - the limit's size and window are an open question owned by the user, non-blocking; `/10x-plan` proposes a default.
- `context/foundation/roadmap.md:137` - the plan may split the limit into its own change if the slice is too wide.
- `context/foundation/prd.md:159-167` - access control: open registration, flat roles, logged-out users see only login/register and cannot generate "also bypassing the form", a per-account generation limit protects the free plan.
- `context/foundation/infrastructure.md:93` - the Data Protection key-loss risk that F-01 closed; S-04 is the first feature that a lost key ring would visibly break.

## Related Research

- `context/archive/2026-09-23-account-store-foundation/research.md` - account store and key persistence
- `context/archive/2026-09-25-first-map-download/research.md` - generate endpoint, limiter, e2e

## Open Questions

For `/10x-plan` or the user:

1. **Limit size and window** (owner: user, `roadmap.md:136`). Constraints found: at least 4 per e2e run for one account, local reruns on a reused server share a window, `home.tsx:26` says "wait a minute" and cannot be edited, and an in-memory limiter resets on every F1 restart. Keeping 10 per minute per account satisfies all four with the fewest changes; whether that is enough protection for open registration (many accounts, each with its own budget) is the product question.
2. **In-memory limiter or a persisted quota.** The brief says "turning the per-IP rate limiter into a per-account limit", which reads as the in-memory limiter re-keyed by account. The PRD's "each account has a generation limit" could also mean a stored daily quota. Confirm the brief's reading.
3. **Unauthenticated endpoints need their own guard.** Register and login are public and hash passwords, which costs CPU on F1. Does the per-IP limit stay for them?
4. **`MapIdentityApi` or hand-written endpoints** (see "endpoint options"). Affects the size of the contract and the unused public surface.
5. **401 mid-session in `client.ts`** without a new `GenerateError` kind, since `home.tsx` is off limits.
6. **Logout and the header row.** Where does logout live if `home.tsx` is untouched and any row above the preview changes the baselines and `map-preview-fit`?
7. **Ownership of `client.ts` versus S-03.** The brief gives `client.ts` to this change, but S-03 must change the generate request it sends. The S-03 brief is not in this worktree; confirm who edits `generateMap`.
8. **How e2e gets its database**: a `services:` container in the `e2e` job or `docker compose up` from `compose.yaml`, and `dotnet ef database update` or the bundle for migrations. And locally: should `npm test` in `e2e/` expect the compose database to be running?
9. **Whether the visual gate gets login/register baselines** in this change, and how the home spec mocks the session call.
10. **Production forwarded headers**: is `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` set on the App Service? Needed before choosing the cookie's `Secure` policy.
