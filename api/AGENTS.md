# Battle Map Generator API

Read the root `../AGENTS.md` first (ownership split, generation rules, testing, deployment). This file adds backend-only rules.

## Scope

This app owns email/password authentication (ASP.NET Core Identity + EF Core), access enforcement, the procedural BSP layout algorithm and the JSON API. It does not render images; `../web/` draws the map.

## Code conventions

- Namespaces are PascalCase and follow the folder: `BattleMapGenerator.Api`, `BattleMapGenerator.Api.Maps`, tests in `BattleMapGenerator.Api.Tests`. The project, assembly and generated OpenAPI file carry the same name (`BattleMapGenerator.Api.csproj`, `.dll`, `.json`).
- The assembly name is also the App Service startup command (`dotnet BattleMapGenerator.Api.dll`), set by `startup-command` in `deploy.yml`. Renaming the assembly means changing that line in the same commit.

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
