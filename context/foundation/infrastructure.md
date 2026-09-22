---
project: battle-map-generator-dla-d-d-dm-toolkit
researched_at: 2026-09-21
recommended_platform: Azure App Service (Linux, F1 free tier, B1 as escape hatch)
runner_up: Fly.io
context_type: mvp
tech_stack:
  language: C# (.NET 10) backend, TypeScript frontend
  framework: ASP.NET Core webapi + React Router 8 (SPA mode, ssr:false), served as static assets by ASP.NET Core
  runtime: .NET 10 on Linux App Service (DOTNETCORE|10.0, LTS)
---

## Recommendation

**Deploy on Azure App Service (Linux), starting on the F1 free plan with Azure SQL's free offer, and move to B1 (about $13/mo) if a quota wall is hit.**

It is the only option that combines a first-class .NET 10 runtime, a $0 tier, and a co-located free database, and it matches the developer's existing Azure experience and the "minimize cost" priority. The main F1 risks (CPU and bandwidth quotas) are largely designed away by rendering the map PNG in the browser instead of on the server (see Assumptions). Fly.io is the runner-up if F1 proves too constrained.

### Interview answers that drove the decision

| Question | Answer | Effect |
|---|---|---|
| Persistent connections / background workers | No (derived from tech-stack: no realtime, no background jobs) | No platform dropped on this basis |
| Cost vs DX | Minimize cost | Favors $0 tier; penalizes Render/Railway/Fly base costs |
| Familiarity | AWS / GCP / Azure | Tie-break for Azure |
| Geography | Single region | No edge advantage counted |
| Service co-location | External providers fine | Neutral; free Azure SQL still a plus |

### Decisions recorded

- **Map delivery: the backend serves JSON only; the frontend renders the map image (decided by the author, 2026-09-21).** The API returns a **semantic grid** (what each cell is: floor, wall, door, corridor, boss arena; plus seed, parameters and the room list; a few KB of JSON). The frontend owns all art: sprite choice, wall autotiling, variants, the preview, and the PNG download. This removes blob storage cost, removes PNG bytes from server bandwidth, keeps server CPU to the BSP layout only, and keeps sprite work (the author's main direction for improving maps) entirely in `web/`. The root and per-app `AGENTS.md` files reflect this.
- **Download: rendered offscreen at a fixed 140 px per grid square,** independent of screen, zoom and `devicePixelRatio`. That is 2× Roll20's 70 px standard, and it prints sharply at 1 inch per square. The preview shares the render function, not the canvas.
- **Determinism: an own seeded PRNG in C#** (not `System.Random`); frontend visual randomness is derived from the same seed. A seed is stable only while the algorithm is unchanged; saved maps (stage 2) store the grid, not the seed.
- **Repository layout: monorepo** `battle-map-generator` with `api/` and `web/` (decided 2026-09-21, histories preserved). API, client and shared fixture grids change in one commit; CI is a single workflow with no cross-repo checkout.
- **Browsers: Chrome and Firefox,** as in the PRD, covered by Playwright end-to-end tests including the download.
- **Printing across multiple pages** is post-MVP (added to the PRD non-goals); the MVP PNG serves both Roll20 and home printing.
- **Fallback: blob links.** If server-side rendering is needed later (non-browser export, heavy rendering, or browser canvas export proves unreliable), the API renders the PNG, uploads it to a private Azure Blob container, and returns a short-lived user-delegation SAS link; a lifecycle rule deletes blobs after one day. Hot LRS is about $0.0165-0.018/GB-month; a free-egress allowance was not confirmed. Whether blob traffic counts against the F1 bandwidth cap was not confirmed in docs (inferred: it does not).
- **Database: Azure SQL free offer,** same region as the app. Stores Identity users and Data Protection keys only; maps are not stored in the MVP.

## Platform Comparison

Hard filter: .NET 10. Netlify was dropped (no .NET runtime; static SPA hosting only). Cloudflare Workers/Pages were dropped for the API (V8 isolates cannot run ASP.NET Core); Cloudflare Containers remains as a paid, partial path. No platform was dropped for persistent connections (not required).

Scores: P = pass, ~ = partial, F = fail. Status checked 2026-09-21.

| Platform | CLI-first | Managed/serverless | Agent-readable docs | Stable deploy API | MCP / integration | Cost for this app |
|---|---|---|---|---|---|---|
| **Azure App Service** | P | P | ~ | P | P | $0 (F1), about $13/mo (B1) |
| **Fly.io** | P | P | P | P | ~ | about $3-10/mo, no free tier |
| **Render** | ~ | P | P | P | P | about $7-13/mo |
| Railway | ~ | P | P | P | ~ | about $5-8/mo (Hobby) |
| Vercel | P | P | P | ~ | ~ | Hobby $0 but non-commercial |
| Cloudflare (Containers) | P | ~ | P | P | P | about $5-15/mo, no free option |
| Netlify | dropped (no .NET) | | | | | |

Notes on scores:
- **Azure:** `az` covers create, deploy, logs and settings; `azure/login` (OIDC) + `azure/webapps-deploy@v3` for CI. Docs have no llms.txt, but raw markdown is in `MicrosoftDocs/azure-docs` on GitHub, so docs are partial. The Azure MCP Server is GA but has no zip-deploy tool; the App Service built-in MCP is preview. Rollback on F1/B1 is redeploying an older artifact (slots need Standard).
- **Fly.io:** `fly launch` generates a Dockerfile for .NET; `fly deploy --remote-only`; rollback via `fly releases --image` then `fly deploy -i <image>`. Docs are on GitHub (superfly/docs). `fly mcp server` is experimental. The proxy closes connections after 60s of no data (verify).
- **Render:** Docker required for .NET. `render deploys create --wait` gives a usable exit code; rollback is dashboard/API only. Every docs page is available as `.md`, plus llms.txt. Hosted MCP server not labelled beta. Free tier (0.1 CPU, 15-minute spin-down) is too weak.
- **Railway:** Dockerfile is the safe path for .NET 10 (the docs conflict on Railpack). No CLI rollback. Hosted MCP is beta. Public HTTP requests are limited to 15 minutes while data flows and closed after 5 minutes of inactivity.
- **Vercel:** .NET only via `Dockerfile.vercel` container functions (status unclear/beta). 4.5 MB request/response body cap. Hobby is non-commercial.
- **Cloudflare:** Containers are GA (per the docs; third-party source dates GA to 2026-04-13) but need the $5/mo Workers Paid plan and have ephemeral disk.

### Shortlisted Platforms

#### 1. Azure App Service (Recommended)

$0 on F1, first-class .NET 10 LTS runtime, co-located free Azure SQL, and the developer's existing familiarity. Weak points: F1 quotas (60 CPU-min/day, 165 MB/day outbound), no slots, cold start after 20 minutes idle. B1 (about $13/mo) removes the quota walls.

#### 2. Fly.io

Cheapest paid option (about $3-10/mo), no quota walls, best docs and CLI. Gap versus Azure: no free tier, Dockerfile required, database is SQLite on a single-host volume (managed Postgres is $38/mo), and rollback is a multi-step command.

#### 3. Render

Best agent tooling of the three (markdown docs, hosted MCP, `--wait` exit codes), no request-duration problem. Gap: about $7-13/mo with Postgres Basic-256mb ($6/mo; the free Postgres expires after 30 days), Docker required, and no CLI rollback.

### Database combinations

| Platform | Database | Cost | Trade-off |
|---|---|---|---|
| Azure App Service | Azure SQL free offer | $0 | Same region as app; EF Core retry needed for resume error 40613; pauses at monthly cap |
| Fly.io | SQLite on a volume | about $0.15/GB-month | Single machine and host; keep backups; Data Protection keys can live on the volume |
| Render | Render Postgres Basic-256mb | about $6/mo | Free Postgres expires after 30 days |

SQLite on Azure App Service `/home` (network storage) is not recommended (general caution, not researched).

## Anti-Bias Cross-Check: Azure App Service

### Devil's Advocate — Weaknesses

1. **Quota walls return HTTP 403 for everything.** Exceeding F1 CPU (60 min/day, 3 min per 5 min) or bandwidth (165 MB/day) stops the whole app until midnight UTC. SPA assets and every server-rendered PNG count.
2. **Data Protection keys are reportedly not persisted on .NET 10 Linux App Service** (dotnet/aspnetcore#64488, opened 2025-11-21, still open at check time). After every restart or 20-minute idle unload, login cookies and antiforgery tokens become invalid.
3. **Azure SQL free offer pauses.** Auto-resume takes about a minute and the first connection fails with error 40613. At the monthly limit the database is unavailable until the 1st, unless "continue with charges" is chosen, which cannot be reverted.
4. **No slots and no CLI rollback on F1/B1.** Rollback means redeploying an old artifact; EF migrations are not reversed.
5. **F1 has no custom domain or custom TLS,** and the F1 quota can be 0 on Free Trial subscriptions (Pay-As-You-Go is needed).

### Pre-Mortem — How This Could Fail

The team shipped on F1 because it was free. In the first prep session before a game night, the DM regenerated maps a dozen times, each preview a multi-megabyte PNG. Combined with the SPA bundle, that crossed the daily bandwidth cap, and the whole app returned 403 until midnight UTC, on the evening the map was needed. Earlier, nobody had noticed that every idle unload logged users out, because Data Protection keys lived on the ephemeral container disk. The team blamed their Identity setup and burned a weekend on it. The Azure SQL database paused mid-month after a stray query loop used up the free vCore-seconds. They flipped it to "continue with charges", which cannot be undone, and then saw a small bill. With no slots, they rolled back a bad migration by hand at night. The lesson was that free-tier quotas are hard walls, not soft limits. They should have budgeted for B1 from the start and measured CPU and bandwidth per map early.

### Unknown Unknowns

- Free Azure SQL databases are pinned to one region per subscription, so the app and database must be created in the same region.
- Only `/home` persists on App Service Linux, and it is network storage, so file I/O is slow. Do not write generated files to disk.
- The Azure MCP server can read app settings and diagnostics but has no zip-deploy tool; deployment must go through CI (`azure/webapps-deploy`).
- Publish-profile auth requires basic auth to be enabled; use OIDC (`azure/login`) instead.
- .NET runtime patch versions roll out on Microsoft's schedule with no ETA (no explicit GA announcement page for .NET 10 was found); pin the SDK and watch for drift.

## Architecture Cross-Check: JSON-only API + browser-rendered map

Second pass on the combined decision (Azure App Service + JSON-only API + client-side canvas rendering), run 2026-09-21 after the rendering decision. Findings marked "browser behavior" or "general" come from general knowledge, not from this research, and should be verified during implementation.

### Devil's Advocate — Weaknesses

1. **The one PRD guardrail (grid alignment) now depends on the user's browser.** Canvas scaling, `devicePixelRatio`, browser zoom and image smoothing can all produce hairline seams or off-by-one tile edges that server CI never sees.
2. **Preview and download can diverge.** The PRD requires the downloaded PNG to match the preview. If the preview is CSS-scaled and the export is a separate render path, they will differ.
3. **The tile grid is now a two-repo contract.** If the generator's tile indices change (for example when boss rooms are added) and the renderer does not, the map draws as garbage with no server-side error.
4. **"Same seed, same map" is not guaranteed by `System.Random` across .NET versions** (general). If regeneration or a stage-2 library stores only the seed, a runtime upgrade can change old maps.
5. **Visual regressions are harder to catch.** Server-side golden-image tests are gone; alignment must be split into a logical part (grid invariants, testable in C#) and a visual part (browser tests).

### Pre-Mortem — How This Could Fail

Six months later the client-side rendering decision looked like a mistake. The DM exported a map for Roll20 on a laptop with 125% display scaling and browser zoom at 110%. The canvas had been sized in CSS pixels, so the exported PNG had hairline seams between tiles and Roll20's grid overlay drifted by a sliver. The partner's Firefox had strict tracking protection, so canvas export returned a blank image; the team had only ever tested in Chrome on the dev machine, because Firefox was planned for later. Adding boss rooms changed the tile indices in the API while a stale open tab still ran the old client, and it drew nonsense for a day. After a .NET upgrade, "regenerate with the same seed" gave different layouts. Meanwhile the F1 quotas were never hit, and the team had spent its worry budget on infrastructure. The lesson was that moving rendering to the browser moved the risk rather than removing it, and the contract and browser behavior needed the same rigor as the hosting plan.

### Unknown Unknowns

- **Canvas readback can be blocked or randomized in Firefox** with strict tracking protection or `privacy.resistFingerprinting` (browser behavior; verify). `toBlob` can then return a blank or noisy image. Keep the blob-link fallback ready and test export in a strict Firefox profile.
- **Canvas output depends on `devicePixelRatio` and zoom** unless it is rendered to a canvas with explicit pixel dimensions. Render at a fixed integer tile size, independent of the screen. Confirm Roll20's expected pixel size per grid square and upload size limit before fixing the export scale.
- **Browsers cap canvas size** (commonly on the order of 16k px per side in Chromium; browser behavior, verify). Fine for S/M/L encounter maps, but it caps the export scale.
- **The OpenAPI document is only mapped in Development** (`Program.cs` calls `MapOpenApi()` inside `IsDevelopment()`). The frontend needs a build-time or checked-in OpenAPI document to generate its client, or it will have no contract source in CI.
- **Stale open tabs after a deploy** run an old client against the new API. Send a `contractVersion` in every response, have the client prompt a reload on mismatch, and serve `index.html` with `no-cache` (hashed assets can be cached long-term, which also keeps tileset downloads out of the F1 bandwidth budget).

### Effect on the decision

Azure App Service stays the recommendation. The cross-check moves the largest remaining risks from hosting quotas to the browser and the contract, and the register below carries the new mitigations.

### Review round 2 — author's rebuttals (2026-09-21)

The author challenged the findings above; the outcome:

- **"Non-browser consumers need a server renderer"** — rejected. This is a personal tool; better maps and sprites matter, API image export does not.
- **Zoom and display scaling corrupt the output** — resolved by design, not by moving rendering: the download is rendered offscreen at a fixed pixel size, so user settings cannot affect the file. Only the on-screen preview can look soft, which is cosmetic.
- **Contract drift between API and web** — accepted as normal API life. The real cost was two repositories, so the code moved to a monorepo. `contractVersion`, `tileSetVersion` and the reload prompt were dropped (two users can refresh).
- **Tile indices in the contract** — reversed. Sending sprite indices would have forced every art change through C#; the API now sends a semantic grid and the frontend owns the art.
- **`System.Random` seed stability** — resolved with an own PRNG, with the caveat that algorithm improvements change seed-to-map results anyway.
- **Firefox canvas export** — kept in scope because the PRD requires Firefox; covered by Playwright instead of a server-rendering fallback.
- **Printing** — added as post-MVP; the 140 px/square export already prints at 1 inch per square.

The blob-link fallback stays documented but is no longer expected to be needed.

## Operational Story

- **Preview deploys**: none on F1 (deployment slots need Standard or higher). Local is the preview environment; pull-request builds run tests in GitHub Actions only.
- **Secrets**: App Service application settings and connection strings (Key Vault references are possible). GitHub Actions authenticates to Azure via OIDC federated credentials, so there is no long-lived secret in GitHub. Rotating the SQL password is a manual portal step.
- **Rollback**: re-run the last successful GitHub Actions deploy, or `az webapp deploy --src-path <previous-build.zip>`. Expect a few minutes. EF Core migrations do not roll back automatically; keep migrations additive and write down the down-migration.
- **Approval**: human-only: creating the subscription and resources, raising the plan tier, choosing "continue with charges" on the SQL free offer, dropping a database, rotating the SQL password. Agent may act unattended: deploy on merge to main via CI, restart the app, read logs and diagnostics.
- **Logs**: `az webapp log config --docker-container-logging filesystem` once, then `az webapp log tail -g <rg> -n <app>`. Read-only diagnostics via the Azure MCP Server (GA); docs lookups via the Microsoft Learn MCP server (GA since 2025-11-07).

## Risk Register

| Risk | Source | Likelihood | Impact | Mitigation |
|---|---|---|---|---|
| CPU or bandwidth quota exhausted, app returns 403 until midnight UTC | Devil's advocate / Pre-mortem | M | H | Render PNG client-side (or blob links); add ASP.NET rate limiter on the generate endpoint and cap map size; measure CPU-seconds per generation locally; Azure budget alert; move to B1 if hit |
| Data Protection keys lost on restart, users logged out constantly | Devil's advocate / Unknown unknowns | H | H | Persist keys to the database (`Microsoft.AspNetCore.DataProtection.EntityFrameworkCore`) before the first deploy; verify with a forced restart |
| Azure SQL resume error 40613 on first request | Research finding | H | L | `EnableRetryOnFailure` in EF Core; document the one-minute warm-up |
| SQL free offer exhausted or locked; irreversible "continue with charges" | Pre-mortem | L | M | Choose auto-pause at the limit; keep the database idle when not in use; back up Identity data |
| Idle unload (20 min) causes slow first request | Research finding | H | L | Accept for MVP; B1 with Always On if it becomes annoying |
| No slots / CLI rollback; bad migration on prod | Devil's advocate / Pre-mortem | M | M | Additive migrations, tested locally against SQL Server in Docker; redeploy previous artifact |
| Free Trial subscription has F1 quota of 0 | Research finding | M | M | Use a Pay-As-You-Go subscription with a budget alert |
| No custom domain or TLS on F1 | Research finding | H | L | Use `*.azurewebsites.net`; upgrade to B1 for a custom domain |
| .NET 10 runtime patch drift on App Service | Unknown unknowns | L | M | Pin the SDK in `global.json`; watch `az webapp list-runtimes` |
| Client-side rendering breaks grid alignment (PRD guardrail) | Devil's advocate / Pre-mortem | L | H | Download rendered offscreen at a fixed 140 px/square, independent of `devicePixelRatio` and zoom; integer positions, smoothing off; one pure render function; grid invariants tested in C#; rendering tested against fixture grids |
| Downloaded PNG differs from the preview | Devil's advocate | L | M | Preview and download share one render function and the same seed; Playwright checks the download's dimensions and that it is not blank |
| Firefox canvas export blocked or randomized (strict tracking protection) | Unknown unknowns | L | M | Playwright end-to-end tests in Firefox cover the download; blob-link fallback documented if it ever fails |
| API and web drift apart | Devil's advocate / Pre-mortem | L | M | Monorepo: API, client and shared fixture grids change in one commit; semantic grid changes only when the algorithm gains a concept; OpenAPI as the contract source |
| Stale open tab runs an old client against a new API | Pre-mortem / Unknown unknowns | L | L | Accepted for two users (refresh); `index.html` served `no-cache`, hashed assets cached long-term |
| Seeded maps change after a runtime upgrade or algorithm change | Devil's advocate / Pre-mortem | M | L | Own seeded PRNG in C# instead of `System.Random`; fixed-seed fixtures updated deliberately when the algorithm changes; store the grid, not the seed, if a library is added |
| Very large maps exceed browser canvas limits at 140 px/square | Unknown unknowns | L | M | Cap map size in the API; check the largest size preset in Playwright |
| No OpenAPI document available at build time (only mapped in Development) | Unknown unknowns | H | L | Generate the OpenAPI document at build time for `web/` |
| Non-GA features relied on | Research finding | L | L | Status recorded 2026-09-21: App Service built-in MCP preview; Shared tier preview; Fly MCP experimental; Railway MCP beta; Vercel container functions / WebSocket / MCP beta; Fly Tigris beta; Render Workflows beta |

## Getting Started

Commands below are checked against .NET 10 (`net10.0` in the API csproj) and React Router `^8.4.0` with `ssr: false` (SPA mode) from `api/` and `web/`, not copied from generic tutorials.

1. **Confirm the runtime string.** Run `az webapp list-runtimes --os linux` and look for `DOTNETCORE|10.0`. Use whatever separator your CLI version prints when creating the app (`az webapp create --help`).
2. **Create resources in one region.** A resource group, a Linux plan on `F1`, and the web app on the .NET 10 runtime; then create the Azure SQL free-offer database in the same region and choose auto-pause at the limit. Use a Pay-As-You-Go subscription and set a budget alert.
3. **Before the first deploy, fix the two known traps in the API:** persist Data Protection keys to the database, and enable EF Core connection retry (`EnableRetryOnFailure`) for the Azure SQL resume error.
4. **Serve the SPA from the API.** In `web/`, `npm run build` produces `build/client` (`index.html` + `assets/`); `build/server` and the `start` script (`react-router-serve`) are not used in this deployment. In CI, copy `build/client` into the API's `wwwroot`; in `Program.cs`, add `UseDefaultFiles`/`UseStaticFiles` and `MapFallbackToFile("index.html")` after the API routes (keep the API under `/api`).
5. **CI on merge to main.** There is no workflow yet. Add one at the monorepo root: build the web, publish the API (`dotnet publish -c Release`), `azure/login@v2` (OIDC), `azure/webapps-deploy@v3`.
6. **Test locally first.** `dotnet run` in `api/`, `npm run dev` in `web/` with a Vite proxy for `/api`, SQL Server in Docker so dev matches Azure SQL; Playwright in Chromium and Firefox for end-to-end tests.
7. **Set up the contract before the generator.** Add a small seeded PRNG, the response fields (seed, parameters, width, height, row-major semantic grid, room list), shared fixture grids, and an OpenAPI document produced at build time; then rate-limit the generate endpoint and cap the map size. The API never returns image bytes.
8. **Do one thin early deploy** (login plus one generate call, about an hour) to surface key persistence, idle unload and SQL resume before the final stage; leave everything else local until then.

## Out of Scope

The following were not evaluated in this research:
- Docker image configuration
- CI/CD pipeline setup
- Production-scale architecture (multi-region, HA, DR)
