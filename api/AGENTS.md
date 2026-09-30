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
- Pipeline order is static files, then `UseAuthentication`, `UseAuthorization`, `UseRateLimiter`. Static files come first so a session cookie on the shell or an asset never loads the key ring; API routes and the SPA fallback still authenticate. The limiter must be able to see the user, and a 401 must not spend a permit.
- Only a request carrying a session cookie, register and login may touch the database. Startup, liveness and anonymous requests never do; `AuthEndpointTests` pins it with an unreachable database.
- Tests get a logged-in client from `ApiFactory.CreateLoggedInClientAsync`; each call spends one permit of that factory's `auth` bucket.

## Map generation contract

- Code lives in `Maps/`: `Prng.cs` (seeded PRNG), `BspGenerator.cs` (layout), `MapModels.cs` (grid contract, default 30×20, cap 60×60), `MapEndpoints.cs` (`POST /api/maps/generate`).
- Fixed-seed fixtures are `../fixtures/grids/*.json`, pinned by `MapFixtureTests`. After a deliberate algorithm change, rewrite them with `UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests` and update `../web/app/map/render-baselines.json` in the same commit.
- The generate endpoint is rate-limited to 10 requests per minute per client IP (policy `generate`, 429 with `Retry-After`). On App Service the client IP comes from `X-Forwarded-For`, which needs the app setting `ASPNETCORE_FORWARDEDHEADERS_ENABLED=true`; without it all traffic shares one bucket.
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
