# Battle Map Generator API

Read the root `../AGENTS.md` first (ownership split, generation rules, testing, deployment). This file adds backend-only rules.

## Scope

This app owns email/password authentication (ASP.NET Core Identity + EF Core), access enforcement, the procedural BSP layout algorithm and the JSON API. It does not render images; `../web/` draws the map.

## Code conventions

- Namespaces are PascalCase and follow the folder: `BattleMapGenerator.Api`, `BattleMapGenerator.Api.Maps`, tests in `BattleMapGenerator.Api.Tests`. The project, assembly and generated OpenAPI file carry the same name (`BattleMapGenerator.Api.csproj`, `.dll`, `.json`).
- The assembly name is also the App Service startup command (`dotnet BattleMapGenerator.Api.dll`), set by `startup-command` in `deploy.yml`. Renaming the assembly means changing that line in the same commit.

## Authentication

- Code lives in `Auth/`: `AuthEndpoints.cs` and `AuthModels.cs`. Routes: `POST /api/auth/register` and `POST /api/auth/login` (body `{ email, password }`, 200 with `{ email }` and the session cookie), `POST /api/auth/logout` (204, also without a session) and `GET /api/auth/me` (200 with the caller's own `{ email }`, 401 without a session).
- Register answers 400 with a validation problem whose `errors` are keyed by `email` or `password`. Login answers the same bodiless 401 for a wrong password, an unknown email and a locked-out account (5 failed attempts lock an account for 5 minutes). The only password rule is a length of 8 to 128 characters; an email is at most 256 characters and uses only letters, digits and `-._@+` (it is also the user name).
- The session is the Identity application cookie: HttpOnly, `SameSite=Lax`, `Secure` when the request is HTTPS, persistent for 14 days with sliding expiration, protected by the key ring in the database so it survives a restart. There are no bearer tokens and no antiforgery tokens, so every state-changing endpoint must stay a POST that takes JSON only (a cross-site form cannot send it; `AuthEndpointTests` pins it).
- There is no server-side login page: an unauthenticated or forbidden API request gets a bare 401 or 403, never a redirect or a `Location` header.
- Register and login share the rate-limit policy `auth`: 10 requests per minute per client IP across both, 429 with `Retry-After`. Logout and `me` are not limited.
- Pipeline order is static files, `UseSessionCookieGate`, `UseAuthentication`, `UseAuthorization`, `UseRateLimiter`. Reading a session cookie loads the key ring from the database, and a forged cookie (a valid-looking payload with an unknown key id) reloads it on every read. So `Auth/SessionCookieGate.cs` removes the session cookie from every request outside `/api` or under `/api/health` (static files, the SPA fallback, liveness and readiness never read it), and limits requests carrying it on the other API routes to 60 per minute per client IP (429 with `Retry-After`) before authentication reads it. Keep `UseAuthentication` at the top level: called only inside a `UseWhen` branch, the framework adds its own at the very start of the pipeline. The limiter must be able to see the user, and a 401 must not spend a permit.
- Only a request carrying a session cookie (valid or forged) to an API route outside `/api/health`, register and login may touch the database. Startup, liveness, static files, the SPA fallback and anonymous requests (including a refused generate) never do; `AuthEndpointTests` and `MapEndpointTests` pin it with an unreachable database or an empty key ring.
- A session cookie stays valid until Identity re-checks the account's security stamp (every 30 minutes by default). A feature that deletes an account or changes a password must lower `SecurityStampValidatorOptions.ValidationInterval` or check the stamp itself.
- Tests get a logged-in client from `ApiFactory.CreateLoggedInClientAsync`; each call spends one permit of that factory's `auth` bucket.

## Map generation contract

- Code lives in `Maps/`: `Prng.cs` (seeded PRNG), `BspGenerator.cs` (layout), `MapModels.cs` (grid contract, request and its data-annotation rules, the size table by room count, cap 60×60), `MapEndpoints.cs` (`POST /api/maps/generate`).
- Encounter parameters: `roomCount` 2–12 (default 6), `encounter` `skirmish` | `boss`, and `bossSize` `large` | `huge` | `gargantuan` for a boss fight. All are optional, so an empty body gives the default 6-room 30×20 map, and the response echoes the parameters used. Any invalid value gets a 400 validation problem naming the field (`request` for a body that is not a JSON object). `MapEndpoints.cs` reads the body itself so that values JSON binding rejects (an unknown enum name, a number as an enum, text as a number) get that body too; `.Accepts<GenerateMapRequest>` keeps the request schema in OpenAPI. The rules are data annotations on `GenerateMapRequest`, run by hand until `AddValidation()` is registered in `Program.cs`. Enums bind by name only, never by number; a comma list of names is read as combined flags, and `[EnumDataType]` rejects the undefined results.
- The map has exactly `roomCount` rooms and its size comes from `MapSize.For` (6 rooms → 30×20; a boss widens the map by the arena's side). A boss fight makes one of the rooms the arena: room `kind` `bossArena`, floor cells `bossArena`, floor at least 8×8 / 10×10 / 12×12 for Large / Huge / Gargantuan, and larger than every other room.
- Fixed-seed fixtures are `../fixtures/grids/*.json`, pinned by `MapFixtureTests` (the default size for three seeds, plus a 3-room map and a Huge boss map). After a deliberate algorithm change, rewrite them with `UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests` and update `../web/app/map/render-baselines.json` in the same commit.
- The generate endpoint requires a session (401 without one, before any permit is spent) and is rate-limited to 10 requests per minute per account (policy `generate`, partitioned by the `NameIdentifier` claim, 429 with `Retry-After`). Forwarded headers still matter: on App Service the client IP for the `auth` limit and the request scheme for the cookie's `Secure` flag come from `X-Forwarded-For`/`X-Forwarded-Proto`, which needs the app setting `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true` (set, verified 2026-09-30); without it all register and login traffic shares one bucket and the cookie loses `Secure`.
- `dotnet build` writes the OpenAPI document to `BattleMapGenerator.Api.json` (committed; `Microsoft.Extensions.ApiDescription.Server` runs `Program.cs` at build time, so startup must need no configuration or database). Regenerate `../web/app/api/schema.d.ts` with `npm run api:types` in `../web/`; CI fails on drift in either file.
- The generate endpoint returns JSON only, never image bytes: seed, parameters, width, height, a row-major **semantic grid** (what each cell is, not which sprite to draw) and the room list.
- Seeded PRNG: implement a small generator (for example SplitMix64 or PCG) in this project and pass it through the algorithm explicitly. Do not use `System.Random` or ambient randomness.
- Test grid correctness as invariants: every room reachable, everything inside the bounds, no half-cells, plus fixed-seed fixtures that pin the grid. Fixture grids are shared with `../web/` tests; update both in the same commit.
- Rate-limit the generate endpoint and cap map size, so a stuck client cannot exhaust the hosting plan's CPU quota.
- The OpenAPI document is generated at build time (see above; `MapOpenApi()` stays Development-only at run time); the frontend reads it as the contract source.

## Hosting constraints

Deployed on Azure App Service (Linux, F1 free tier to start, B1 as the escape hatch). See `../context/foundation/infrastructure.md`.

- Keep API routes under `/api`. Serve the SPA with `UseDefaultFiles`/`UseStaticFiles` and `MapFallbackToFile("index.html")` after the API routes; serve `index.html` with `no-cache` (hashed assets may be cached long-term).
- Persist ASP.NET Core Data Protection keys to the database before the first deploy; on .NET 10 Linux App Service they are otherwise lost on restart and users are logged out.
- Enable EF Core connection retry (`EnableRetryOnFailure`) for Azure SQL's free offer, which fails the first connection after an auto-pause.
- Stay stateless: do not write generated files to disk (only `/home` persists and it is slow network storage).
- Develop against SQL Server in Docker locally, so dev matches Azure SQL.
