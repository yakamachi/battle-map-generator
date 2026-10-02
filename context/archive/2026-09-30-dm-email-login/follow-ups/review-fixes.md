# Follow-ups from implementation reviews

Work decided during review triage that lands outside the phase it came from.

## After S-03 (encounter-parameters) merges

- ~~**Declare 401 and 429 on the generate endpoint** (phase 3 review F4).~~ Done, 2026-10-02 (full impl review / closeout): added `.Produces(StatusCodes.Status401Unauthorized)` and `.Produces(StatusCodes.Status429TooManyRequests)` to the `/api/maps/generate` route chain in `api/Maps/MapEndpoints.cs`, regenerated `api/BattleMapGenerator.Api.json` and `web/app/api/schema.d.ts`. `dotnet test api.Tests` (157/157) and `npm run typecheck` both pass.

## S-04 closeout pull request

- ~~**Roadmap** (phase 3 review F6)~~ Done, 2026-10-02: `context/foundation/roadmap.md`'s baseline ("Backend / API", "Auth"), the S-04 row/slice status (→ done) and Open Roadmap Question 2 are all updated to reflect S-04 landed.
