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

Merged to `main` via PR #12, squash commit `23fe5a2`, 2026-10-01. Deploy workflow build + deploy succeeded; smoke test (liveness and readiness) healthy.

Follow-ups for the merge after S-04 lands (from the phase reviews):

- ~~Merge `web/app/api/maps.ts` into `client.ts`'s `generateMap`, folding in S-04's changes there.~~ Done: dm-email-login's own `da4764a` merge-with-main commit folded this in while resolving conflicts with S-03; `maps.ts` is gone and `generateMap` lives in `client.ts` (verified in dm-email-login's closeout review, 2026-10-02).
- ~~Register `AddValidation()` in `api/Program.cs` and drop the hand-run validation in `MapEndpoints.cs`; then revisit rejecting comma-list enum values (phase 1 review, F1).~~ Evaluated and declined, 2026-10-02 (now that both S-03 and S-04 have landed): `AddValidation()` only validates a bound endpoint parameter, needs the project-wide `EnableRequestDelegateGenerator` opt-in to do anything, and even then keys its errors by the C# property name instead of this app's camelCase JSON convention, with no supported fix. The hand-run `Validator.TryValidateObject` call in `MapEndpoints.cs` stays. See `context/foundation/lessons.md`, "Triage a .NET native swap against what it actually does today, not by its name alone".
