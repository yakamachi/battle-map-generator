<!-- PLAN-REVIEW-REPORT -->
# Plan Review: Home View UI Contract

- **Plan**: context/changes/home-view-ui-contract/plan.md
- **Mode**: Deep
- **Date**: 2026-09-29
- **Verdict**: REVISE → SOUND after triage (all 6 fixed)
- **Findings**: 0 critical, 3 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| End-State Alignment | WARNING |
| Lean Execution | PASS |
| Architectural Fitness | WARNING |
| Blind Spots | WARNING |
| Plan Completeness | WARNING |

## Grounding
9/9 paths ✓, 3/3 symbols ✓ (`POST "/api/maps/generate"` client.ts:22, `canDownload` home.tsx:41, `setRenderedMap(null)` home.tsx:72), brief↔plan ✓, Progress↔Phase 1:1 (4/4 phases).

Claims verified:
- **Confirmed:**
  - The image `mcr.microsoft.com/playwright:v1.63.0-noble` exists (amd64/arm64, Node 24, root by default).
  - Tailwind 4.2 `max-h`/`h` read `--spacing-*`, and `--aspect-*` is a theme namespace (`node_modules/tailwindcss/dist/lib.mjs`, `theme.css:490`).
  - `object-fit` applies to `<canvas>` (HTML spec, replaced elements).
  - A `page.route` handler that never answers stalls the request (`playwright-core/types/types.d.ts:4410`).
- **Contradicted:** a stock shadcn Button would pass the planned scan (see F1).

## Findings

### F1 — `ui:scan` bans what `shadcn add` produces

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Architectural Fitness
- **Location**: Phase 2 criteria 2.2/2.3; Phase 4 §3 (`ui:scan` over `app/components`) and §4 (rule: add components with `npx shadcn@latest add`)
- **Detail**:
  - The plan scans all of `app/components` (including `ui/`) for `dark:` and numeric arbitrary values.
  - Yet the rule tells the next agent to add components with `shadcn add`. The current radix-nova Button source has 9 `dark:` classes and `text-[0.8rem]`, so 10 tokens fail the scan.
  - With OS-driven dark mode, those `dark:` classes do work (Tailwind's default `dark:` is `prefers-color-scheme`). They are token-based refinements, not literals.
  - So every future `shadcn add` would turn CI red, or force hand-editing each primitive and drifting from upstream.
- **Fix A ⭐ Recommended**: Scope the `dark:` and arbitrary-value ban to views (`app/routes`, `app/root.tsx`, `app/components/*.tsx` outside `ui/`). Keep the colour-literal part of the scan (hex/rgb/oklch/palette classes) on `app/components/ui` too, and let `ui/` primitives keep shadcn's token-based `dark:` refinements.
  - Strength: `shadcn add` output passes as-is, and views, where the drift happened (C1), stay strict.
  - Tradeoff: two scan scopes to explain in the rule.
  - Confidence: HIGH — measured against the live registry source.
  - Blind spot: other shadcn components may contain literal colours; the scan would still catch those.
- **Fix B**: Keep one strict scope and require stripping `dark:` classes from every added primitive.
  - Strength: one simple rule.
  - Tradeoff: every future component diverges from upstream, and dark-mode polish in primitives is lost.
  - Confidence: MED — the effort per component is unmeasured.
  - Blind spot: none significant.
- **Decision**: FIXED — Fix A: two scan scopes (views strict; `ui/` colour literals only)

### F2 — Visual CI container runs as root without Playwright's recommended options

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 4 §3 (`visual` job, `visual:docker`/`visual:update`)
- **Detail**:
  - Playwright's GitHub Actions container example (playwright.dev/docs/ci) uses `options: --user 1001`.
  - Its Docker docs recommend `--ipc=host` (Chromium can run out of memory without it) and `--init`.
  - The plan names `--ipc=host` only for the local script and nothing for the CI job.
  - Chromium as root works (the sandbox is simply disabled), but a local/CI mismatch in options can also shift rendering.
- **Fix**: The CI job uses `container: { image: mcr.microsoft.com/playwright:v1.63.0-noble, options: --user 1001 --ipc=host }`. The local scripts use `--ipc=host --init`, the same image, and a user matching the host UID.
- **Decision**: FIXED — CI `--user 1001 --ipc=host`; local `--ipc=host --init` with host UID

### F3 — Pending-route loading shots need an explicit teardown

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Blind Spots
- **Location**: Phase 4 §2 (`loading-first`, `loading-regenerate`)
- **Detail**:
  - Holding `/api/maps/generate` unanswered is supported: the request stalls.
  - `unrouteAll` documents that a handler still running at teardown "may result in unhandled error".
  - `behavior: 'wait'` would hang on a handler that never resolves.
  - The plan doesn't say how the held request ends.
- **Fix**: The held handler just returns without awaiting anything. Each loading test ends with `await page.unrouteAll({ behavior: 'ignoreErrors' })`.
- **Decision**: FIXED — handler returns, `unrouteAll({ behavior: 'ignoreErrors' })` at test end

### F4 — Empty-state copy promises "ready for Roll20"

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: End-State Alignment
- **Location**: Phase 3 §1 (empty state text)
- **Detail**: The S-01 manual check 4.5 found that the PNG (140 px per square) comes into Roll20 at twice the size, because Roll20 uses 70 px per unit. It has to be resized to 2100×1400 (30×20 units). "Ready for Roll20" promises something the user just saw fail.
- **Fix**: Reword the text to "Click Generate to create a 30×20 battle map, then download it as a PNG (140 px per square)" and drop the Roll20 claim until the scale follow-up.
- **Decision**: FIXED — Roll20 claim dropped from the empty-state text

### F5 — "No copy change" vs new copy in Phase 3

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Completeness
- **Location**: What We're NOT Doing; Phase 3 §1, §3
- **Detail**: "No copy or language change" sits next to three new strings: the empty-state message, the "Generating…" overlay and "Back to the generator". The intent (existing strings stay the same) is clear, but a reviewer could flag the new text as scope drift.
- **Fix**: Reword the exclusion as "No changes to existing strings or language; new strings only for the empty state, the loading overlay and the 404 link."
- **Decision**: FIXED — exclusion reworded; new strings listed

### F6 — Viewport-fit value as a spacing token spreads it to the whole scale

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Architectural Fitness
- **Location**: Critical Implementation Details → Canvas sizing; Phase 3 §1
- **Detail**: The plan allows "a `--spacing-*` theme value or an `@utility`". A `--spacing-preview: calc(100dvh - …)` token becomes available to every spacing utility (`p-preview`, `gap-preview`, …), which invites misuse. Tailwind documents `@utility` as the way to add a one-off custom utility.
- **Fix**: Choose `@utility map-preview-fit { … }` in `app.css`, a named utility holding the `calc()`, and drop the spacing-token option.
- **Decision**: FIXED — `@utility map-preview-fit` chosen
