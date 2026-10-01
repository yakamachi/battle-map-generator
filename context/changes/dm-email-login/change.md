---
change_id: dm-email-login
title: DM email and password login guarding map generation, with a per-account limit
status: impl_reviewed
created: 2026-09-30
updated: 2026-10-01
archived_at: null
---

## Notes

Roadmap slice S-04 (`context/foundation/roadmap.md`).

Parallel with S-03 (room count and boss arena). You own: api/Program.cs (auth, and turning the per-IP rate limiter into a per-account limit), web/app/api/client.ts, web/app/routes.ts plus new login and register routes. In MapEndpoints.cs only add .RequireAuthorization() to the route chain and leave the Generate handler alone. Don't edit web/app/routes/home.tsx: protect pages with a layout route. Existing API tests go through a logged-in client helper in api.Tests/Infrastructure/. In e2e, log in once in a setup step that saves the session (Playwright storageState) and don't rewrite map.spec.ts. The plan must give e2e in CI a SQL Server; right now it runs without one. The per-account limit must allow the e2e user's 4 generations per run. Never merge BattleMapGenerator.Api.json or schema.d.ts by hand; generate them again.
