# CLAUDE.md

@AGENTS.md

## Build gotcha

`dotnet restore`/`dotnet build` fails with `NETSDK1226` unless the .NET 10 ASP.NET Core runtime + targeting pack (`Microsoft.AspNetCore.App` 10.0.12) is installed locally.

No test project, `.editorconfig`, or CI workflow exists yet — don't assume `dotnet test` or a lint command works until one is added.
