<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: First Map Download

- **Plan**: context/changes/first-map-download/plan.md
- **Scope**: Phase 4 of 4
- **Reviewed phases**: 4
- **Date**: 2026-09-29
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 2 observations

Reviewed the staged, uncommitted Phase 4 diff on `first-map-download-p4` (13 files). Plan drift found no DRIFT, MISSING or EXTRA items: every planned change matches, the implementer's adaptations were confirmed benign, and every factual claim added to the docs was checked against the code. Production startup without a database, `--no-launch-profile` with HTTPS redirection, the `e2e:prepare` delete path and the `@playwright/test` pin were all verified safe. Automated criteria were re-run: e2e 2/2 (Chromium and Firefox), `npm test` 36/36, typecheck, `dotnet test api.Tests` 39/39. A break check (download saves a blank canvas) turned e2e red in both browsers. Manual checks 4.4–4.6 are pending.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — The download promise never settles if the toBlob callback throws

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/map/download.ts:9-24
- **Detail**: Only a `null` blob rejects. A throw from `URL.createObjectURL`, `append` or `click` inside the async callback escapes the Promise executor. It goes to `window.onerror`, the promise never settles, and `onDownload` shows no error.
- **Fix**: Wrap the callback body in `try { … resolve(); } catch (error) { reject(error); }`.
- **Decision**: FIXED — the `toBlob` callback body is wrapped in try/catch and rejects, so `onDownload` shows the error

### F2 — Revoking the object URL on the next tick can abort a Firefox download

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/map/download.ts:23
- **Detail**: `setTimeout(() => URL.revokeObjectURL(url), 0)`. Some Firefox versions read the blob URL asynchronously after `click()`, so revoking on the next task can cancel a ~1 MB download. Headless Playwright captures downloads differently, so the green e2e run doesn't cover this. It sits on the infrastructure.md "Firefox export" risk row. FileSaver.js waits 40 s for this reason.
- **Fix**: Revoke after a generous delay (e.g. 60 s). The leak is at most one blob per click.
- **Decision**: FIXED — `revokeObjectURL` now runs after 60 s (`REVOKE_DELAY_MS`), at most one leaked blob per click

### F3 — The e2e waits for the whole generate and render within the default 5 s expect timeout

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/e2e/map.spec.ts:17
- **Detail**: `await expect(download).toBeEnabled()` covers the generate request, the atlas fetch and decode, and drawing a 4200×2800 canvas. On a cold CI runner, the first request to the freshly started .NET host and Firefox's canvas work could exceed 5 s, which makes the test flaky. Locally the whole test takes under 1 s.
- **Fix**: Pass `{ timeout: 20_000 }` to that assertion.
- **Decision**: FIXED — `toBeEnabled({ timeout: 20_000 })` (`RENDER_TIMEOUT_MS`)

### F4 — The e2e never generates twice

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/e2e/map.spec.ts
- **Detail**: The Download button's state logic (`renderedMap === map`, reset on Generate, the canvas unmounting while loading) is exercised only for the first map. A second Generate is the path where the button could stay enabled for a stale canvas. Regeneration as a feature belongs to S-02, but pressing Generate twice already exists. It would add 2 generate calls per run (4 of the 10 per minute).
- **Fix**: After the first download, click Generate again, wait for the button to be enabled, and download a second time. Assert that the new filename's seed differs and that the header is still 4200×2800.
- **Decision**: FIXED — the spec generates and downloads twice per browser (4 of 10 calls per minute); the filename must match the seed shown on screen, and the second map's pixels must differ from the first

### F5 — The e2e doesn't compare the downloaded pixels with the preview

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/e2e/map.spec.ts:19-46
- **Detail**: The canvas and the file are checked separately (size, not blank). Preview and download are identical by construction, because both come from the same canvas, so the "differs from preview" risk is covered by design rather than by the test.
- **Fix**: None needed. If it's ever wanted, decode the file in the page with `createImageBitmap` and compare its hash with the canvas's.
- **Decision**: FIXED — the downloaded PNG is decoded in the page and its SHA-256 pixel hash must equal the canvas's. A break check (one pixel changed in the file) turned it red in both browsers
