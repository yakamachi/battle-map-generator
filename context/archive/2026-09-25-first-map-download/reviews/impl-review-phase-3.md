<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First Map Download

- **Plan**: context/changes/first-map-download/plan.md
- **Scope**: Phase 3 of 4
- **Reviewed phases**: 3
- **Date**: 2026-09-29
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 3 observations

Reviewed the staged, uncommitted Phase 3 diff on `first-map-download-p3` (33 files). Plan drift: every planned item matches, and nothing from Phase 4 or the "not doing" list crept in. The implementer's reported adaptations were all confirmed benign. Automated criteria were re-run: typecheck and build pass, `npm test` passes 36/36 in Chromium and Firefox, the contract diff is clean, and the atlas rebuild is byte-identical. Manual checks 3.5–3.7 are pending.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Findings

### F1 — Render baselines are unproven on CI

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: web/app/map/render.test.ts:57-66, web/app/map/render-baselines.json
- **Detail**: The baselines are exact SHA-256 hashes of `getImageData`, generated headless on the local CachyOS machine. CI runs `ubuntu-latest` with the same pinned Playwright 1.63.0 browsers, but Skia's per-CPU SIMD paths and the headless raster backend can differ by ±1 in alpha-blended pixels. Chromium and Firefox already differ from each other by at most 2 levels in the blended cells, so exact cross-machine equality isn't guaranteed. The plan anticipated per-browser baselines but not per-machine drift.
- **Fix A ⭐ Recommended**: Let the first `web-tests` run be the gate. If it fails, regenerate the baselines in CI (a `workflow_dispatch` path that runs `UPDATE_BASELINES=1 npm test` and uploads the JSON as an artifact), commit that file, and note in `web/AGENTS.md` that CI's environment owns the baselines.
  - Strength: Keeps the plan's exact-hash contract; costs nothing if CI matches.
  - Tradeoff: Baselines can then only be refreshed through CI, not locally.
  - Confidence: MED — depends on how Skia behaves on the runner's CPU.
  - Blind spot: GitHub runner CPUs vary between runs, so hashes could flake across runners even after they're regenerated.
- **Fix B**: Replace the hash comparison with a per-pixel comparison against committed reference PNGs, with a tolerance of ±2 per channel.
  - Strength: Robust across machines and browsers, and a failure shows where the image changed.
  - Tradeoff: It commits 6 large reference PNGs (4200×2800 each) or needs downscaling, and it departs from the plan's hash contract.
  - Confidence: HIGH — standard visual-regression practice.
  - Blind spot: The size of the repo and of the test runs.
- **Decision**: FIXED via Fix A — no code change now; the first `web-tests` run on the Phase 3 PR decides. If it's red, regenerate the baselines through CI, commit them, and note in `web/AGENTS.md` that CI owns them

### F2 — `server.fs.allow: [".."]` exposes the whole repo to the Vitest dev server

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/vitest.config.ts:59
- **Detail**: The tests need only `../fixtures/grids`, but the allow list opens the repo root, including `api/appsettings.Development.json` (tracked, with local Password/Key values), `.git/` and `.claude/`. The server is localhost-only and short-lived, so the risk is low. The plan text (plan.md:274) specified `[".."]`.
- **Fix**: Set `fs: { allow: [".", "../fixtures"] }`. An explicit list replaces Vite's default, so `web/` itself must stay allowed.
- **Decision**: FIXED — `fs.allow` narrowed to `[".", "../fixtures"]`; tests still load the fixtures (36/36)

### F3 — `web/CLAUDE.md` says there is no test tooling

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/CLAUDE.md:9
- **Detail**: "No lint or test tooling is configured yet … Don't assume `npm test` … exist." That is false as of this phase (`npm test`, `api:types`, `atlas`). The plan defers the CLAUDE.md updates to Phase 4, but agents that read the file before then are misled.
- **Fix**: Rewrite line 9 now to list `npm test` (Vitest browser mode, Chromium and Firefox), `UPDATE_BASELINES=1 npm test`, `npm run api:types` and `npm run atlas`, and keep "no lint tooling". Phase 4 can extend it with e2e.
- **Decision**: FIXED — `web/CLAUDE.md` lists `npm test`, `UPDATE_BASELINES`, `api:types` and `atlas`; "no lint tooling" kept

### F4 — A render error throws away the loaded atlas

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/home.tsx:47-48
- **Detail**: The `.catch` resets `atlasRef.current = null` for any error, including a `renderMap` throw (the size cap), even though the atlas loaded fine. The bitmap is then fetched and decoded again on the next click. This can't happen with the fixed 30×20 map today.
- **Fix**: Reset the ref only when `loadAtlas` rejects, and handle render errors separately.
- **Decision**: FIXED — a failed `loadAtlas` clears the cached promise; a render error keeps the loaded atlas

### F5 — Canvas cap comment is inaccurate, and there is no area check

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/map/render.ts:8-9
- **Detail**: "Chromium and Firefox both reject canvases wider or taller than this" (16384). Both browsers actually allow sides up to about 32767 and enforce an area limit (Chromium about 16384²). As a conservative per-side cap, 16384 is fine. The API's 60×60 maximum is 8400² px, about 282 MB of RGBA. That isn't reachable until S-03 adds sizes.
- **Fix**: Reword the comment as a deliberate conservative cap that stays under Chromium's area limit, and leave the area check to S-03.
- **Decision**: FIXED — the comment now describes a deliberate cap under Chromium's area limit, with the area check deferred to S-03

### F6 — CI never checks that the atlas is reproducible

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/scripts/build-scribble-atlas.mjs, .github/workflows/ci.yml
- **Detail**: Criterion 3.4 is a local gate only. CI tests against the committed PNG, so it stays consistent, but "byte-identical on every run" (with `sharp.simd(false)`) is proven only on this machine.
- **Fix**: None needed now. Revisit if someone else rebuilds the atlas on another machine.
- **Decision**: FIXED — the `contract` job rebuilds the atlas and fails on any diff in `app/map/tileset/`
