# Battle Map Generator API

Read the root `../AGENTS.md` first (ownership split, generation rules, testing, deployment). This file adds backend-only rules.

## Scope

This app owns email/password authentication (ASP.NET Core Identity + EF Core), access enforcement, the procedural BSP layout algorithm and the JSON API. It does not render images; `../web/` draws the map.

## Map generation contract

- The generate endpoint returns JSON only, never image bytes: seed, parameters, width, height, a row-major **semantic grid** (what each cell is, not which sprite to draw) and the room list.
- Seeded PRNG: implement a small generator (for example SplitMix64 or PCG) in this project and pass it through the algorithm explicitly. Do not use `System.Random` or ambient randomness.
- Test grid correctness as invariants: every room reachable, everything inside the bounds, no half-cells, plus fixed-seed fixtures that pin the grid. Fixture grids are shared with `../web/` tests; update both in the same commit.
- Rate-limit the generate endpoint and cap map size, so a stuck client cannot exhaust the hosting plan's CPU quota.
- The OpenAPI document must be available at build time (`Program.cs` currently maps it only in Development); the frontend reads it as the contract source.

## Hosting constraints

Deployed on Azure App Service (Linux, F1 free tier to start, B1 as the escape hatch). See `../context/foundation/infrastructure.md`.

- Keep API routes under `/api`. Serve the SPA with `UseDefaultFiles`/`UseStaticFiles` and `MapFallbackToFile("index.html")` after the API routes; serve `index.html` with `no-cache` (hashed assets may be cached long-term).
- Persist ASP.NET Core Data Protection keys to the database before the first deploy; on .NET 10 Linux App Service they are otherwise lost on restart and users are logged out.
- Enable EF Core connection retry (`EnableRetryOnFailure`) for Azure SQL's free offer, which fails the first connection after an auto-pause.
- Stay stateless: do not write generated files to disk (only `/home` persists and it is slow network storage).
- Develop against SQL Server in Docker locally, so dev matches Azure SQL.
