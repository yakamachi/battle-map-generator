---
date: 2026-10-17T10:35:45+02:00
researcher: Claude Code (claude-opus-5-5)
git_commit: f48a702cbdde5d6d19f14f89a267dea0b1247c45
branch: saved-maps-library
repository: yakamachi/battle-map-generator
topic: "Saved maps library: what exists today and what a save/list/open/rename/delete feature must fit"
tags: [research, api, data, ef-core, web, e2e, auth, saved-maps]
status: complete
last_updated: 2026-10-17
last_updated_by: Claude Code (claude-opus-5-5)
---

# Research: Saved maps library

**Date**: 2026-10-17T10:35:45+02:00
**Researcher**: Claude Code (claude-opus-5-5)
**Git Commit**: f48a702cbdde5d6d19f14f89a267dea0b1247c45 (`main` at branch creation; no code changes on the branch yet)
**Branch**: saved-maps-library
**Repository**: yakamachi/battle-map-generator

## Research Question

What does the codebase have today, and what constraints apply, for a feature where a logged-in DM:
- saves the current generated map with a title;
- lists their own saved maps;
- opens one, rendered as today;
- renames it;
- deletes it.

The map is stored as its full grid (not only the seed), and only the owner can access it (`context/changes/saved-maps-library/change.md:12-14`).

## Summary

- **No domain persistence exists yet.**
  - `AppDbContext` holds only Identity and Data Protection keys (`api/Data/AppDbContext.cs:10-14`).
  - There is one migration (`api/Migrations/20260923154324_InitialAccountStore.cs`).
  - The `NameIdentifier` claim is read only to partition the generate limiter (`api/Program.cs:64`). No handler reads a user id or filters data by owner yet.
- **Everything needed to re-render is in today's response.**
  - `GeneratedMap(Seed, Parameters, Width, Height, Cells, Rooms)` (`api/Maps/MapModels.cs:46`).
  - The web renderer needs only `seed`, `width`, `height` and `cells` (`web/app/map/tileset.ts:22`). Sprite variants come from `mulberry32(map.seed)` (`tileset.ts:70`).
  - Storing the whole `GeneratedMap` reproduces the preview and PNG while the atlas and tileset mapping are unchanged.
  - The seed must be stored alongside the cells, because cells alone don't fix the floor variants.
  - Measured on the fixture grids, compact JSON runs at about 7.3 bytes per cell. A default 30×20 map is about 4.8 KB. The generator's largest real map, 54×30 (`MapModels.cs:125`), is about 12 KB.
- **Product docs still exclude the feature.** Before planning, these need changing:
  - PRD Non-Goals (`context/foundation/prd.md:175-176`, `:182`) and the secondary criterion (`:61-62`).
  - Shape-notes (`context/foundation/shape-notes.md:27-28`, `:277-278`).
  - Roadmap "Parked" (`context/foundation/roadmap.md:161-162`).
  - Tech-stack docs (`tech-stack.md:29`, `tech-stack-api.md:29`, `tech-stack-web.md:29`).
  - infrastructure.md ("maps are not stored in the MVP", `:38`).
  - test-plan.md: the IDOR negative space at `:60-62` and the §7 "Map library, CRUD … no test budget" at `:199`.
- **One existing rule clashes with REST verbs.**
  - `api/AGENTS.md:18`: "every state-changing endpoint must stay a POST that takes JSON only". The rule exists because there is no antiforgery token, and it is pinned by `AuthEndpointTests.cs:453-474`.
  - Rename and delete as PUT/PATCH/DELETE would contradict that wording, so the plan must choose deliberately (see Open Questions).
- **Most other constraints are satisfied by default** if the new endpoints sit under `/api/maps/...` with `RequireAuthorization()`:
  - The database-touch rule (`lessons.md:33-39`).
  - SessionCookieGate's 60/min per-IP limit on cookie requests (`SessionCookieGate.cs:62-63`).
  - The `/api` 404 fallback (`Program.cs:236-237`).

## Detailed Findings

### Data layer and migrations

- **Context**: `IdentityDbContext<IdentityUser>` plus `IDataProtectionKeyContext`. Its only own set is `DataProtectionKeys`, and there is no `OnModelCreating` override (`api/Data/AppDbContext.cs:10-14`).
  - User ids are strings, stored as `AspNetUsers.Id nvarchar(450)` (`api/Migrations/20260923154324_InitialAccountStore.cs:32`).
  - A saved-map row would reference that key.
- **Registration**: the connection string is read lazily, with `EnableRetryOnFailure` (`api/Program.cs:93-108`). This lets the build-time OpenAPI step and the migrations bundle start without a database.
- **Migrations**:
  - Created with `dotnet ef migrations add <Name> --project api` (`context/archive/2026-09-23-account-store-foundation/plan.md:110`).
  - Applied in dev via `dotnet ef database update`.
  - Applied in e2e via `e2e/scripts/db-up.mjs` (`.github/workflows/ci.yml:182-184`).
  - In deploy, an EF bundle runs before the app is deployed, with up to 5 attempts 30 s apart (`.github/workflows/deploy.yml:50-54`, `:132-143`). A failure stops the job.
  - Not rolled back automatically. Policy is "keep migrations additive and write down the down-migration" (`context/foundation/infrastructure.md:154`, `:174`).
- **Tests pick up new migrations automatically.**
  - The fixture migrates the shared database once (`api.Tests/Infrastructure/SqlServerFixture.cs:21-25`).
  - `AccountStoreTests.cs:12-23` asserts there are no pending migrations.

### Map contract

- **Records**:
  - `GeneratedMap(uint Seed, MapParameters Parameters, int Width, int Height, CellKind[] Cells, Room[] Rooms)`, with row-major cells, `index = y * Width + x` (`api/Maps/MapModels.cs:45-46`).
  - `MapParameters(RoomCount, Encounter, BossSize?)` (`:43`).
  - `Room(X, Y, Width, Height, Kind)` (`:40`).
- **Enums**: `CellKind` {Void, Floor, Corridor, Wall, Door, BossArena} (`:9-18`), `EncounterType`, `BossSize` and `RoomKind` (`:20-37`).
- **Serialization**: camelCase strings, integers rejected, strict number handling (`MapJson.Configure`, `MapModels.cs:152-159`, applied at `Program.cs:34`).
- **OpenAPI**: schemas `GeneratedMap`, `MapParameters`, `Room` and `CellKind`, with every field required (`api/BattleMapGenerator.Api.json:205-400`). The web types mirror them (`web/app/api/schema.d.ts:101-114`).
- **Size**: measured on `fixtures/grids/*.json` as compact JSON.
  - 30×20 is about 4.8 KB, of which cells are about 4.4 KB.
  - 40×20 boss is about 7.0 KB.
  - The 60×60 size cap would be about 26–29 KB, with about 40 KB as the all-`"corridor"` worst case. The real generator maximum is 54×30, about 12 KB.

### Endpoints, auth and ownership

- **Route style**: no route groups. Each route is `app.MapPost`/`MapGet` in an extension method (`MapEndpoints.cs:25-37`, `AuthEndpoints.cs:26-44`), registered at `Program.cs:233-234`.
- **Generate** (`MapEndpoints.cs:29-35`) carries:
  - `.Accepts<GenerateMapRequest>(isOptional: true, "application/json")` and `.WithName`;
  - `.RequireAuthorization()` and `.RequireRateLimiting("generate")`;
  - `.Produces(401/429)`;
  - typed results `Results<Ok<GeneratedMap>, ValidationProblem>` (`:42`).
- **Body and validation**:
  - The body is read by hand (`MapEndpoints.cs:81-112`).
  - Data-annotation validation also runs by hand, with errors keyed camelCase (`:120-137`). The reason is recorded in `lessons.md` "Triage a .NET native swap".
  - The `Accepts` content type is what refuses non-JSON. A POST without JSON content type falls to the `/api` 404 (`MapEndpointTests.cs:51-52`, `:356-367`).
- **Status codes**:
  - The cookie events turn 401/403 into bare responses with no redirect (`Program.cs:143-152`).
  - 404 appears only in the `/api/{**rest}` fallback (`:237`) and the ready-probe key gate (`:224-227`).
  - No authorization policies exist.
- **Ownership**: no per-user data pattern exists. The closest are `Me` (`AuthEndpoints.cs:113-118`) and `Two_accounts_each_see_only_their_own_email_from_me` (`AuthEndpointTests.cs:234-245`).
- **CSRF stance**: the cookie is `SameSite=Lax` (`Program.cs:136`), with no antiforgery and no CORS configured (no `AddCors` in `Program.cs`).
  - The rule is "state-changing endpoint must stay a POST that takes JSON only" (`api/AGENTS.md:18`).
  - It is pinned for form-urlencoded and text/plain (`AuthEndpointTests.cs:453-474`).
  - It records no reasoning about PUT/PATCH/DELETE.
- **Error handling**: no `AddProblemDetails`/`UseExceptionHandler`. Under TestServer an unhandled exception is rethrown to the caller (`AuthEndpointTests.cs:294-298`).

### Constraints that apply to new persisted endpoints

- **Database-touch rule** (`context/foundation/lessons.md:33-39`, `api/AGENTS.md:22`):
  - A session-cookie request to an API route outside `/api/health` may touch the database. Anonymous requests must not.
  - `RequireAuthorization()` refuses anonymous calls with 401 before any key-ring or database read. That is the existing generate behaviour, pinned with an unreachable database in `MapEndpointTests.cs:123-136`.
- **SessionCookieGate**: `NeedsSession` covers `/api` except `/api/health` (`api/Auth/SessionCookieGate.cs:62-63`). So any `/api/maps/...` route inherits the 60/min per-IP limit on cookie-carrying requests (`:18`, `:49-56`).
  - No per-account rate limit applies to non-generate routes on the inspected path. Only `generate` and `auth` policies exist (`Program.cs:57-101`).
- **OpenAPI and client**:
  - `dotnet build` writes `api/BattleMapGenerator.Api.json`, and `npm run api:types` regenerates `web/app/api/schema.d.ts`.
  - The CI `contract` job fails on drift in either file (`ci.yml:100-126`).
  - API, client and fixtures change in one commit (`AGENTS.md:17`).
- **Other constraints**:
  - `/api` 404 fallback stays before the SPA fallback (`lessons.md:26-31`, `Program.cs:236-237`).
  - Stateless: no disk writes (`api/AGENTS.md:47`).
  - Testing env with in-memory config (`lessons.md:41-46`).

### Web side

- **Routes** (`web/app/routes.ts:3-7`): a `protected.tsx` layout around `index("routes/home.tsx")`, plus `login` and `register`.
  - `protected.tsx` redirects to `/login` without a session (`:12-16`) and renders the header with email and "Log out" (`:37-46`).
  - A "My maps" route (for example `maps`, `maps/:id`) would be a sibling of `index` under that layout. This is an inference from the structure; no such route exists.
- **Home** (`web/app/routes/home.tsx`):
  - A `Status` union of idle, loading, ready with `map` and error (`:28-32`). `renderedMap` gates download (`:70`, `:81`).
  - The action row holds Generate, Download PNG and the `Seed: N` text (`:208-218`).
  - Atlas loading, `renderMap` and download are inline in the component, not extracted (`:94-149`).
- **Layout budget**: the preview height is `100dvh - 18rem` (`15rem` at sm) in `web/app/app.css:89-95`. Rows added above the preview count against it (`web/AGENTS.md` Access section; comments at `home.tsx:152`, `protected.tsx:36`).
- **API client** (`web/app/api/client.ts:35`): `openapi-fetch` with typed `{ok}|{ok:false,error}` results (`:11-18`).
  - Every catch goes through `reportError` to Sentry (`:41-44`).
  - 401 handling, `window.location.assign("/login?expired=1")`, exists only inside `generateMap` (`:64-74`), so each new call would repeat it unless it is shared.
  - `getSession()` hits `/api/auth/me` (`web/app/api/auth.ts:29-34`).
- **Rendering**: `renderMap(ctx, map: MapGrid, atlas)` (`web/app/map/render.ts:27-65`) and `drawOps` (`tileset.ts:62+`).
  - Rendering depends only on `seed/width/height/cells` plus the static atlas.
  - The download filename is `battle-map-<seed>.png` (`web/app/map/download.ts:3-4`, `:9-35`), seed-based rather than title-based.
- **UI conventions** (`web/AGENTS.md`, UI section):
  - Tailwind v4 and shadcn (`web/components.json`). Primitives present: alert, button, input, label, native-select. No dialog or list.
  - Tokens only in `app/app.css`. `npm run ui:scan` enforces no literal colours.
  - UI strings are English (`root.tsx:39`).
  - The visual screenshot gate is in `web/visual/` (3 projects, mocked API), with baselines updated via `npm run visual:update` (Docker).

### Tests

- **API**:
  - `SqlServerFixture` (`api.Tests/Infrastructure/SqlServerFixture.cs`) provides `CreateMigratedDatabaseAsync()` for a fresh database (`:39-45`) and `CreateContext(cs)` for direct `DbSet` queries (`:47-48`).
  - `ApiFactory.CreateLoggedInClientAsync` registers an account and spends one `auth` permit (`ApiFactory.cs:61-68`).
- **Reusable API patterns**:
  - A fresh factory per test (`MapEndpointTests.cs:13-14`) and unique emails (`AuthEndpointTests.cs:13`).
  - Two-account isolation (`AuthEndpointTests.cs:234-245`).
  - Bare-401 assertions (`MapEndpointTests.cs:97-121`) and the validation-problem reader (`AuthEndpointTests.cs:486-494`).
  - Form content types refused (`:453-474`).
  - The unreachable-DB timing test (`MapEndpointTests.cs:123-136`).
- **Web**: Vitest browser mode in Chromium and Firefox (`web/vitest.config.ts`), with no React component tests. Views are covered by the visual gate only.
- **E2E**:
  - Real API, never mocked (`e2e/AGENTS.md`). Locators by role or name.
  - `map.spec.ts` generates and downloads twice per browser.
  - A run spends 6 of the 10/min generate permits for the shared e2e account (`e2e/AGENTS.md`). A save/open e2e flow that generates first spends from the remaining 4.
  - Saved maps from earlier runs persist in the local database for that account.

## Code References

- `api/Data/AppDbContext.cs:10-14`: the context, with no domain sets.
- `api/Migrations/20260923154324_InitialAccountStore.cs:14-170`: the only migration (Identity and keys).
- `api/Program.cs:57-101`: rate-limit policies (`generate`, `auth`).
- `api/Program.cs:93-108`: DbContext registration.
- `api/Program.cs:136`: `SameSite=Lax`.
- `api/Program.cs:143-152`: bare 401/403.
- `api/Program.cs:207-212`: pipeline order.
- `api/Program.cs:233-237`: endpoint registration and the `/api` 404 fallback.
- `api/Maps/MapModels.cs:9-46`: the map contract records and enums.
- `api/Maps/MapModels.cs:152-159`: JSON options.
- `api/Maps/MapEndpoints.cs:29-35`: generate endpoint metadata.
- `api/Maps/MapEndpoints.cs:81-137`: manual body read and validation.
- `api/Auth/AuthEndpoints.cs:113-118`: `Me`, the closest "own data only" pattern.
- `api/Auth/SessionCookieGate.cs:18, 49-63`: per-IP cookie limit and the `/api` scope.
- `api/AGENTS.md:18`: POST + JSON-only rule for state-changing endpoints.
- `api/AGENTS.md:22`: database-touch rule.
- `api.Tests/Infrastructure/SqlServerFixture.cs:21-48`: migrated shared DB, fresh DB, direct context.
- `api.Tests/Infrastructure/ApiFactory.cs:49-70`: client helpers.
- `api.Tests/AuthEndpointTests.cs:234-245`: two-account isolation.
- `api.Tests/AuthEndpointTests.cs:453-474`: form content types refused.
- `web/app/routes.ts:3-7`: route config.
- `web/app/routes/protected.tsx:12-46`: session guard and header.
- `web/app/routes/home.tsx:28-32, 70-81, 94-149, 208-218`: state, gating, render/download, action row.
- `web/app/api/client.ts:11-18, 35, 41-44, 64-74`: client, result type, Sentry choke point, 401 redirect.
- `web/app/map/tileset.ts:22, 62-70`: `MapGrid` and seed-derived variants.
- `web/app/map/render.ts:27-65`: renderer.
- `web/app/map/download.ts:3-4`: seed-based filename.
- `web/app/app.css:89-95`: preview height budget.

## Architecture Insights

- **The semantic grid stays the contract.** The API owns data and access, and `web/` owns all visual output (root `AGENTS.md`, Ownership split).
  - A saved map is persisted contract data: the API stores and returns a `GeneratedMap`-shaped payload, and `web/` re-renders it with the existing renderer.
  - No image ever needs to be stored.
- **The seed's role changes for saved maps.** It stops being a reproduction key and stays only as the render-variant input and the filename stem.
  - Root `AGENTS.md:23`: a seed is stable only while the algorithm is unchanged, and the `encounter-parameters` change already re-mapped every seed (`context/archive/2026-09-30-encounter-parameters/plan.md:366`).
- **Saved maps are tied to the atlas and renderer.** A future atlas or tileset-mapping change would change how old saved maps look. Neither the response nor any stored data carries a renderer or atlas version. Today that change is treated as intended and baselines are updated (`web/AGENTS.md`).
- **Ownership is enforced only in the API** (`AGENTS.md`, Ownership split: "api/ owns authentication and access enforcement"). Every read, rename and delete must filter by the caller's `NameIdentifier`. No existing helper does this.

## Historical Context (from prior changes)

The library was deliberately excluded until now:
- `context/foundation/shape-notes.md:27-28` records "FR-006 (biblioteka z tytułem)" removed from MVP in the Socratic round, "odłożona całkowicie do etapu 2".
- `context/archive/2026-09-23-account-store-foundation/plan.md:36`: "No map persistence (maps are not stored in the MVP)". The same appears in `plan-brief.md:33`.
- `context/archive/2026-09-25-first-map-download/plan.md:55`: "no saving maps". `plan.md:30` fixed the row-major string-cell encoding.
- `context/archive/2026-09-30-encounter-parameters/research.md:125`: the parameters of the map on screen "are not stored anywhere yet". `plan.md:366` shows that a generator change re-maps every seed.
- `context/archive/2026-09-30-dm-email-login/research.md:107`: authenticated requests spend free-offer vCore-seconds that public generation did not.
- `context/foundation/prd.md:136`: the isolation NFR already reads "Dane mapy/kampanii danego konta nie są widoczne dla innych kont", so it anticipates stored maps.

## Related Research

- `context/archive/2026-09-23-account-store-foundation/research.md`: database, Identity and migrations setup.
- `context/archive/2026-09-30-dm-email-login/research.md`: sessions, cookie gate, database-touch rule.
- `context/archive/2026-09-29-home-view-ui-contract/research.md`: UI tokens and the home view contract.

## Open Questions

These are the choices `/10x-plan` must settle; they are not resolved by the code.

1. **HTTP verbs vs the POST-only rule** (`api/AGENTS.md:18`).
   - Option one: keep rename and delete as POST with a JSON body (for example `POST /api/maps/{id}/rename`, `POST /api/maps/{id}/delete`).
   - Option two: adopt PUT/PATCH/DELETE and amend the rule, with its reasoning recorded, and tests updated to match.
   - On the inspected config, a cross-site HTML form can send neither non-POST methods nor JSON (no CORS, `SameSite=Lax`). This is reasoning, not something the repo documents.
2. **Not-owned resource response.** 404 for another account's map (does not reveal existence) or 403. No convention exists (`Program.cs:224-237` shows 404 used only for the fallback and the ready-key gate).
3. **What is saved.**
   - Option one: the client posts the `GeneratedMap` it shows. The server must then validate the grid, since a client could store an arbitrary payload.
   - Option two: the server regenerates from seed and parameters on save. This only matches while the algorithm is unchanged, which is the same caveat as `AGENTS.md:23`.
   - Option three: the server keeps the last generated map per account, but the API is stateless today (`api/AGENTS.md:47` covers disk; there is no server-side session state).
4. **Storage shape.** One JSON column holding the `GeneratedMap`, or separate columns for seed, width, height and parameters plus a cells blob. Size is small either way: ≤ ~12 KB per map at the real generator maximum.
5. **Limits.** A cap on saved maps per account, and a rate limit on save. Today only SessionCookieGate's 60/min per IP applies to new routes. The free offer's storage size and vCore-seconds are not recorded anywhere in `context/` (gap), so a cap cannot be sized from repo evidence.
6. **Title rules.** Length, uniqueness per account, empty or default title. Neither the PRD nor the shape-notes define a map entity beyond "pojedyncza encja Mapa" (`prd.md:175-176`).
7. **Doc updates the plan must include.**
   - The PRD gets a new FR/US and the library comes out of Non-Goals.
   - The roadmap gets a new slice, which needs an ID and Change ID `saved-maps-library`.
   - Infrastructure `:38`, plus the risk-register row `:170`, whose "back up Identity data" would now include maps.
   - Tech-stack lines.
   - The test-plan IDOR negative space `:60-62` and §7 `:199`.
8. **UI placement.**
   - A Save control in the action row (`home.tsx:208-218`).
   - A "My maps" route under the `protected.tsx` layout.
   - New primitives for a title input and delete confirmation (no dialog component exists).
   - The preview height budget (`app.css:89-95`).
   - New visual-gate states.
9. **E2E budget.** A save/open flow spends generate permits for the shared e2e account (6 of 10 used per run today), and saved rows accumulate across runs in the local database.
