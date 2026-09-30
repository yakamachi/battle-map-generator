<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Home View UI Contract

- **Plan**: context/changes/home-view-ui-contract/plan.md
- **Scope**: Full plan (Phase 4 reviewed in depth; phases 1–3 checked for cross-phase effects on top of their own phase reviews)
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-09-30
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 5 warnings, 1 observation

## Context

PR #9 was squash-merged as `dcb83c9` before this review finished. That merge did not include the epilogue commit `69a8d56`, so `main` still has `change.md` at `implementing`, and Progress rows 4.4/4.6 unchecked with no Phase 4 SHAs. The closeout PR must carry the epilogue and repoint every Progress SHA to `dcb83c9`.

**Plan drift.** The code matches Phase 4 and all its amendments:
- plan review F1–F3 (scan scopes, container options, held-route teardown)
- impl-review-phase-1 F1 (token-source scope, post-`shadcn add` rule line, row 4.8)

**Baselines:** 8 desktop-light, 8 desktop-dark and 6 mobile.

**Desired End State:** every bullet is satisfied.

**Cross-phase:** Phase 4 touched only `root.tsx`'s `cn` wrap. The single canvas and the download rule are intact.

**Security.** `serve-spa.mjs` rejects path traversal (probed: `..`, `%2e%2e`, `..%2f..`, `%00`) and binds `127.0.0.1` only. `visual-docker.mjs` passes an args array with no shell, so there is no injection.

**Success criteria.** Every automated criterion passed on the branch head:
- locally: `ui:scan` with its break checks, `visual:docker` (22 passed), typecheck, build, `npm test` and e2e
- in PR #9's CI: all 6 checks
- `main` now requires `api`, `web`, `contract`, `web-tests`, `e2e` and `visual`

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | WARNING |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — A required check depends on Google Fonts at runtime

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: web/app/root.tsx:16-27, web/visual/home.visual.spec.ts
- **Detail**:
  - Every screenshot renders Inter fetched from fonts.googleapis.com.
  - A slow or blocked response times out the gate or captures fallback fonts.
  - Google also changes the Inter files it serves over time, which would break all 22 baselines with no code change.
  - `visual` is now a required check on `main`, so either case blocks merges.
- **Fix A ⭐ Recommended**: In the visual spec, `page.route` the Google Fonts CSS and font files to a vendored Inter woff2 under `web/visual/fonts/` (SIL OFL). Production is unchanged.
  - Strength: the gate becomes hermetic and deterministic, with no new runtime dependency and no extra bytes on the F1 bandwidth quota.
  - Tradeoff: the tests pin a font file that production doesn't serve, so a Google-side change would show in production but not in the gate.
  - Confidence: HIGH — a standard Playwright pattern, and the spec already routes `/api`.
  - Blind spot: the exact Google Fonts CSS URL shape to intercept, which needs a quick check.
- **Fix B**: Self-host Inter in the app (`@fontsource-variable/inter` imported in `app.css`) and drop the Google Fonts links.
  - Strength: production and tests use the same font bytes, and one third-party request is removed.
  - Tradeoff: a new dependency, which the user decides on. The Latin woff2 (~50–100 KB) counts against the 165 MB/day F1 quota, though it's cached.
  - Confidence: MED — needs a bundle and bandwidth check.
  - Blind spot: whether a cold-visit budget matters today.
- **Decision**: FIXED — Fix A: vendored Inter (`@fontsource-variable/inter` 5.3.0 woff2 + OFL) served by an auto fixture; stray font requests fail the test; all 22 baselines regenerated

### F2 — `ui:scan` misses several colour utilities

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/scripts/ui-scan.mjs:20
- **Detail**: The scan was verified to let these through: `border-t-black`, `border-x-red-500`, `caret-red-500`, `decoration-red-500`, `ring-offset-white`. Also `accent-*` and `placeholder-*`.
- **Fix**: Allow a side segment on borders (`border(-[xytrbl])?`), add `caret|accent|decoration|ring-offset|placeholder` to the utility list, and re-run the break check.
- **Decision**: FIXED — border sides and caret/accent/decoration/ring-offset/placeholder added; break check flagged `border-t-black caret-red-500`

### F3 — Nested component folders are scanned by neither scope

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/scripts/ui-scan.mjs:38-45
- **Detail**: The views scope only takes top-level `app/components/*.tsx`. A future `app/components/map/foo.tsx` would escape both the views and primitives scopes.
- **Fix**: Walk `app/components` recursively. Everything under `ui/` is a primitive, and everything else is a view.
- **Decision**: FIXED — `app/components` walked recursively; a nested `app/components/map/x.tsx` was flagged as `[views]`

### F4 — `serve-spa.mjs` fails badly when the build is missing or a file is gone

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/scripts/serve-spa.mjs:51-55, :78
- **Detail**:
  - `createReadStream(...).pipe(res)` has no `error` handler. Without `build/client/index.html`, the first request throws ENOENT and kills the server, and Playwright only says "webServer exited".
  - A missing asset such as `/assets/x.js` falls back to `index.html` with a 200. ASP.NET's fallback returns 404 for file paths, so a missing chunk should fail loudly here too.
- **Fix**:
  - At startup, exit with "build/client/index.html missing — run npm run build".
  - On a stream error, send 500.
  - Return 404 for paths with a file extension that don't exist.
- **Decision**: FIXED — startup check for `index.html`, 500 on read error, 404 for missing paths with an extension

### F5 — The plan text no longer matches the shipped code

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: context/changes/home-view-ui-contract/plan.md, plan-brief.md, change.md
- **Detail**: Future reviews check against the plan, so it should say what shipped. Where it doesn't:
  - **Visual tolerance:** the plan says `maxDiffPixelRatio: 0.01`; the code uses `maxDiffPixels: 50, threshold: 0.05`.
  - **Fixtures:** the plan says `seed-1.json` is used for a second map. The spec never draws one, because the regenerate request is held.
  - **Button radius:** the plan says `rounded-md`; the code uses `rounded-lg` (impl-review-phase-2 F2).
  - **Alert role:** the plan says it always renders `role="alert"`; the default variant now uses `status` (impl-review-phase-2 F3).
  - **Undocumented in the plan:** `vite.config.ts`'s `preview.host: "127.0.0.1"` (IPv6 prerender fix), and `root.tsx`'s `cn(buttonVariants())` (the 404 border fix, with no code comment either).
  - **`change.md` Notes:** still say token values go in `:root` / `.dark`.
  - **Brief:** its Guard row omits the `app.css` token scope, and it says "one PR per phase" although everything shipped as PR #9.
- **Fix**: In the closeout PR, add an "Implementation notes" addendum under the plan's Overview listing each deviation and its reason, correct the `change.md` note and the brief's two lines, and add a one-line comment at `root.tsx`'s `cn` wrap.
- **Decision**: FIXED — plan "Implementation notes" addendum, change.md token note, brief Guard/effort lines, comment at root.tsx `cn`

### F6 — Mobile screenshots stop at the viewport

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/visual/home.visual.spec.ts
- **Detail**: The shots aren't `fullPage`. On 390×844, anything below the fold isn't pinned: an error Alert pushes the frame down, and later S-03 form rows will too.
- **Fix**: Use `fullPage: true` for the `mobile` project, and regenerate its 6 baselines.
- **Decision**: FIXED — `fullPage` on the mobile project; its baselines regenerated
