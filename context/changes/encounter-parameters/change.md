---
change_id: encounter-parameters
title: Encounter parameters: room count and boss arena
status: impl_reviewed
created: 2026-09-30
updated: 2026-09-30
archived_at: null
---

## Notes

Roadmap S-03. Runs in parallel with S-04 (dm-email-login), in its own worktree on `feat/encounter-parameters`.

Parallel-work boundaries (from the owner, 2026-09-30):

- This change owns: the Generate handler and the request record in `api/Maps/`, `web/app/routes/home.tsx` (the form), and `fixtures/grids/*.json`.
- Don't touch: `api/Program.cs` (rate limiter), `web/app/api/client.ts`, `web/app/routes.ts`.
- In `api.Tests/MapEndpointTests.cs` add new test methods and don't rewrite existing ones.
- Default parameters must keep `e2e/tests/map.spec.ts` passing unchanged; new e2e cases go in a new spec file.
- Never merge `BattleMapGenerator.Api.json` or `schema.d.ts` by hand; generate them again.
