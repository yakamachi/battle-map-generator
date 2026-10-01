---
change_id: encounter-parameters
title: Encounter parameters: room count and boss arena
status: implemented
created: 2026-09-30
updated: 2026-10-01
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

Follow-ups for the merge after S-04 lands (from the phase reviews):

- Merge `web/app/api/maps.ts` into `client.ts`'s `generateMap`, folding in S-04's changes there. The room-count limits (2/12/6) are repeated by hand in `maps.ts`; keep them next to the merged function (phase 3 review, F2).
- Register `AddValidation()` in `api/Program.cs` and drop the hand-run validation in `MapEndpoints.cs`; then revisit rejecting comma-list enum values (phase 1 review, F1).
