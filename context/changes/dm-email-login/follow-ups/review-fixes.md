# Follow-ups from implementation reviews

Work decided during review triage that lands outside the phase it came from.

## After S-03 (encounter-parameters) merges

- **Declare 401 and 429 on the generate endpoint** (phase 3 review F4). Add `.Produces(StatusCodes.Status401Unauthorized)` and `.Produces(StatusCodes.Status429TooManyRequests)` to the `/api/maps/generate` route chain in `api/Maps/MapEndpoints.cs`, then regenerate `api/BattleMapGenerator.Api.json` (`dotnet build api`) and `web/app/api/schema.d.ts` (`npm run api:types` in `web/`). Deferred because the S-04 brief limits this file to `.RequireAuthorization()` while S-03 edits it. The client handles both statuses by code, so nothing depends on it meanwhile.

## S-04 closeout pull request

- **Roadmap** (phase 3 review F6): `context/foundation/roadmap.md` baseline "Backend / API" still says generation is public and rate-limited until S-04, and Open Roadmap Question 2 (public generation before login) is resolved by S-04. Update both when S-04 is marked done.
