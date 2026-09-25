# CLAUDE.md

@AGENTS.md

## Build gotcha

`dotnet restore`/`dotnet build` fails with `NETSDK1226` unless the .NET 10 ASP.NET Core runtime + targeting pack (`Microsoft.AspNetCore.App` 10.0.12) is installed locally.

Tests live in the sibling project `../api.Tests/` (xUnit + Testcontainers SQL Server, so Docker must be running): `dotnet test api.Tests` from the repo root. CI runs it before publishing. There is no `.editorconfig` or lint command yet.
