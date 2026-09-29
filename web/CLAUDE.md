# CLAUDE.md

@AGENTS.md

## Commands and gotchas

- `npm run dev`, `npm run typecheck`, `npm run build` are the real workflow commands.
- `npm start` runs a leftover Node SSR server (`react-router-serve`) that is never used — this app runs in SPA mode (`ssr: false` in `react-router.config.ts`) and is served by the ASP.NET Core app in `../api/`. Do not suggest running or fixing `npm start`.
- `npm test` runs the mapping and rendering tests in Vitest browser mode, headless, in both Chromium and Firefox (`npx playwright install chromium firefox` once). `UPDATE_BASELINES=1 npm test` rewrites `app/map/render-baselines.json` for the browsers it runs; update it in the same commit as any fixture or atlas change.
- `npm run e2e:prepare` builds the SPA and copies `build/client` into `../api/wwwroot` (gitignored); `npm run e2e` then runs Playwright in Chromium and Firefox, starting the API itself (`dotnet run`, Production environment, port 5108, no database). Locally it reuses a server already on 5108; rerun `e2e:prepare` after web changes. The generate endpoint allows 10 calls per minute per IP and a run makes one per browser, so rapid reruns against a reused server can hit 429.
- `npm run api:types` regenerates `app/api/schema.d.ts` from `../api/BattleMapGenerator.Api.json` (build the API first); CI fails if either file drifts.
- `npm run atlas` rebuilds `app/map/tileset/` from `tileset-src/scribble/`; CI fails if the committed atlas differs from a fresh build.
- No lint tooling is configured (no ESLint/Prettier/Biome). Don't assume `npm run lint` exists.
