# CLAUDE.md

@AGENTS.md

## Build gotcha

`dotnet restore`/`dotnet build` fails with `NETSDK1226` unless the .NET 10 ASP.NET Core runtime + targeting pack (`Microsoft.AspNetCore.App` 10.0.12) is installed locally.

Tests live in the sibling project `../api.Tests/` (xUnit + Testcontainers SQL Server, so Docker must be running): `dotnet test api.Tests` from the repo root. `UPDATE_FIXTURES=1 dotnet test api.Tests --filter MapFixtureTests` rewrites the shared fixture grids in `../fixtures/grids/`. The e2e tests (`npm test` in `../e2e/`, after `npm run db:up`) start this app with `dotnet run --no-launch-profile` in the Production environment against the compose SQL Server. CI (`../.github/workflows/ci.yml`) runs the API tests on every pull request; deploys don't re-run it. There is no `.editorconfig` or lint command yet.
