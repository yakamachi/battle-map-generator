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

Post-S-04 re-verification (2026-10-02, dm-email-login closeout): the full impl review (`reviews/impl-review.md`) was written 2026-10-01 before S-04 merged. Re-ran every success criterion fresh against the current `main` (both S-03 and S-04 landed): `dotnet test api.Tests` 157/157, `dotnet build api` + `npm run api:types` no contract drift, `npm run typecheck`/`build`/`ui:scan`/`test` in `web/` all clean, `npm run visual:docker` 40 passed / 2 skipped, `npm run atlas` unchanged, `npm run build:app && npm test` in `e2e/` 9/9 in Chromium and Firefox (including `parameters.spec.ts`'s boss-map generation case). Confirmed the Generate handler in `api/Maps/MapEndpoints.cs` has no diff against this change's own tip beyond the `.RequireAuthorization()` line S-04 added; `home.tsx`'s only post-merge diff is the mechanical `maps.ts` → `client.ts` import rename already noted in dm-email-login's change.md. No regressions found.

Follow-ups for the merge after S-04 lands (from the phase reviews):

- ~~Merge `web/app/api/maps.ts` into `client.ts`'s `generateMap`, folding in S-04's changes there.~~ Done: dm-email-login's own `da4764a` merge-with-main commit folded this in while resolving conflicts with S-03; `maps.ts` is gone and `generateMap` lives in `client.ts` (verified in dm-email-login's closeout review, 2026-10-02).
- ~~Register `AddValidation()` in `api/Program.cs` and drop the hand-run validation in `MapEndpoints.cs`; then revisit rejecting comma-list enum values (phase 1 review, F1).~~ Evaluated and declined, 2026-10-02 (now that both S-03 and S-04 have landed): `AddValidation()` only validates a bound endpoint parameter, needs the project-wide `EnableRequestDelegateGenerator` opt-in to do anything, and even then keys its errors by the C# property name instead of this app's camelCase JSON convention, with no supported fix. The hand-run `Validator.TryValidateObject` call in `MapEndpoints.cs` stays. See `context/foundation/lessons.md`, "Triage a .NET native swap against what it actually does today, not by its name alone".
