# CLAUDE.md

@AGENTS.md

## Commands and gotchas

- `npm run dev`, `npm run typecheck`, `npm run build` are the real workflow commands.
- `npm start` runs a leftover Node SSR server (`react-router-serve`) that is never used — this app runs in SPA mode (`ssr: false` in `react-router.config.ts`) and is served by the ASP.NET Core app in `../api/`. Do not suggest running or fixing `npm start`.
- No lint or test tooling is configured yet (no ESLint/Prettier/Biome, no test framework, no Playwright). Don't assume `npm test` or `npm run lint` exist.
