# Battle Map Generator

A DM toolkit that generates grid-aligned D&D battle maps. Monorepo: `api/` (ASP.NET Core, .NET 10) and `web/` (React Router SPA, TypeScript), deployed as one app on Azure App Service.

## Context

- Product scope: `context/foundation/prd.md`. Decision history and unresolved requirements: `context/foundation/shape-notes.md`.
- Stack: `context/foundation/tech-stack.md` (combined), with per-app hand-offs in `tech-stack-api.md` and `tech-stack-web.md`.
- Deployment, hosting, database or CI work: read `context/foundation/infrastructure.md` first (platform, quotas, risk register, operational story).
- Each app has its own `AGENTS.md` with rules for that side. Read it before changing code there.

## Ownership split

- `api/` owns authentication and access enforcement, the procedural BSP **layout algorithm**, and the JSON API. It never renders or returns images.
- `web/` owns the UI and **all visual output**: choosing sprites for each cell, wall autotiling, visual variants, the on-screen preview and the PNG download.
- The contract between them is a **semantic grid**: each cell says what it *is* (for example floor, wall, door, corridor, boss arena), never which sprite to draw. It changes only when the algorithm learns a new concept; art and sprite work stays entirely in `web/`.
- Backend-generated OpenAPI is the contract source. Change the API, the client and the shared fixture grids in the same commit.

## Map generation rules

- Deterministic: the same seed and parameters give the same grid. Use the project's own small seeded PRNG, never `System.Random` or `Math.random()`.
- Any randomness in `web/` (sprite variants, decoration) is derived from the response's seed, so re-rendering the same map always looks the same.
- A seed is only stable while the algorithm is unchanged. Improving the generator is expected to change which map a seed produces; tests pin fixed-seed grids and are updated deliberately when the algorithm changes. If maps are ever saved, store the grid, not only the seed.
- The PRD guardrail (the map is always aligned to the grid, with no shifted or cut-off tiles) is tested on both sides: grid invariants in `api/`, rendered output in `web/`.

## Testing

- `api/`: unit tests for the algorithm (grid invariants, connectivity, fixed-seed fixtures).
- `web/`: rendering tests against the shared fixture grids.
- End to end: Playwright in **Chrome and Firefox** (both required by the PRD), covering login, generate, regenerate and download.

## Deployment

- One GitHub Actions workflow on merge to `main`: build `web/`, copy `web/build/client` into the API's published `wwwroot`, publish `api/`, deploy with `azure/login` (OIDC) and `azure/webapps-deploy`.
- Destructive or billing actions are human-only: raising the plan tier, "continue with charges" on the Azure SQL free offer, dropping a database, rotating secrets.

## Guards

- Skills must not write to `context/archive/`. Archived changes are immutable; if a resolved target path starts with `context/archive/`, abort with: "This change is archived. Open a new change with `/10x-new` instead."
