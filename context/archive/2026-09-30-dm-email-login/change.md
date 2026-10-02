---
change_id: dm-email-login
title: DM email and password login guarding map generation, with a per-account limit
status: archived
created: 2026-09-30
updated: 2026-10-02
archived_at: 2026-10-02T12:18:41Z
---

## Notes

Roadmap slice S-04 (`context/foundation/roadmap.md`). Merged to `main` via PR #14, squash commit `d6adc22`, 2026-10-01. Deploy workflow build + deploy succeeded. All three deployed-app manual checks (plan.md 4.11–4.13) verified against production 2026-10-02: register/login work with a `Secure; HttpOnly; SameSite=Lax` cookie, the session survives `az webapp restart`, and generate without a session returns 401. (A first register attempt and the first post-restart `me` each hit a transient 500 from the Azure SQL free-tier database resuming from auto-pause — expected per this plan's Performance Considerations, not a defect; a retry succeeded both times.)

Closeout note (full impl review, 2026-10-02): `home.tsx`'s Phase 4 "no diff against main" success criterion held at the time it was checked, but no longer holds against current `main`. Once S-03 merged, the branch's own `da4764a` merge-with-main commit picked up folding `web/app/api/maps.ts` into `client.ts` (per this change's own Migration Notes), which required a one-line import/rename update in `home.tsx` (`generateMapWith` from `~/api/maps` → `generateMap` from `~/api/client`; no logic change). Mechanical and anticipated, not scope creep — noted here so a future reader doesn't mistake that criterion as still true.

Parallel with S-03 (room count and boss arena). You own: api/Program.cs (auth, and turning the per-IP rate limiter into a per-account limit), web/app/api/client.ts, web/app/routes.ts plus new login and register routes. In MapEndpoints.cs only add .RequireAuthorization() to the route chain and leave the Generate handler alone. Don't edit web/app/routes/home.tsx: protect pages with a layout route. Existing API tests go through a logged-in client helper in api.Tests/Infrastructure/. In e2e, log in once in a setup step that saves the session (Playwright storageState) and don't rewrite map.spec.ts. The plan must give e2e in CI a SQL Server; right now it runs without one. The per-account limit must allow the e2e user's 4 generations per run. Never merge BattleMapGenerator.Api.json or schema.d.ts by hand; generate them again.
