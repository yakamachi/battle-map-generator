# CLAUDE.md

@AGENTS.md

## Commands and gotchas

- `npm run dev`, `npm run typecheck`, `npm run build` are the real workflow commands.
- `npm start` runs a leftover Node SSR server (`react-router-serve`) that is never used — this app runs in SPA mode (`ssr: false` in `react-router.config.ts`) and is served by the ASP.NET Core app in `../api/`. Do not suggest running or fixing `npm start`.
- `npm test` runs the mapping and rendering tests in Vitest browser mode, headless, in both Chromium and Firefox (`npx playwright install chromium firefox` once). `UPDATE_BASELINES=1 npm test` rewrites `app/map/render-baselines.json` for the browsers it runs; update it in the same commit as any fixture or atlas change.
- End-to-end tests are in the root `../e2e/` package (`npm --prefix ../e2e run build:app && npm --prefix ../e2e test`); see `../e2e/CLAUDE.md`.
- `npm run api:types` regenerates `app/api/schema.d.ts` from `../api/BattleMapGenerator.Api.json` (build the API first); CI fails if either file drifts.
- `npm run atlas` rebuilds `app/map/tileset/` from `tileset-src/scribble/`; CI fails if the committed atlas differs from a fresh build.
- No lint tooling is configured (no ESLint/Prettier/Biome). Don't assume `npm run lint` exists.
