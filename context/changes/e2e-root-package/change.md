---
change_id: e2e-root-package
title: Move end-to-end tests to a root e2e package
status: implemented
created: 2026-09-30
updated: 2026-09-30
archived_at: null
---

## Notes

**Why.** The Playwright e2e lived in `web/e2e/` only because Playwright is an npm package. The tests exercise the whole app as it is deployed: the .NET host in `api/` serving the built SPA from `web/`. Each side keeps its own tests next to it: `api.Tests/` (a separate .NET project by necessity) and `web/app/**/*.test.ts` (Vitest). The whole-app tests now sit at the root, next to both. Raised in the user's review on 2026-09-30. A light change: no research or plan, one commit, one PR.

**What moved.**
- `web/e2e/map.spec.ts` → `e2e/tests/map.spec.ts` (unchanged)
- `web/playwright.config.ts` → `e2e/playwright.config.ts`. The only changes are `testDir: "tests"` and the comments (the build script name, and "two calls per browser", which was stale).
- New `e2e/package.json` scripts:
  - `build:app` runs `scripts/build-app.mjs`, which builds `../web` and copies it into `../api/wwwroot`. It replaces web's `e2e:prepare`.
  - `test` runs `playwright test`.
  - `typecheck` runs `tsc`, which is new: the specs were previously typechecked by web's `tsconfig`.
- The new package has its own lockfile, `tsconfig.json`, `.gitignore`, `AGENTS.md` and `CLAUDE.md`.
- CI job `e2e` (same name, so it's still a required check) installs `web` and `e2e`, typechecks the specs, runs `build:app`, then Playwright from `e2e/`. The report is uploaded from `e2e/`.

**What stays in `web/`.**
- `playwright` (Vitest browser mode runs on it).
- `@playwright/test`. It's unused on `main` until `home-view-ui-contract` Phase 4 adds the SPA-only screenshot tests with a mocked API, which belong in `web/`. Removing it now would only mean adding it back there.
- web's `.gitignore` entries for `test-results` and `playwright-report` stay for the same reason.

**Docs.** Root `AGENTS.md` (monorepo line, per-package `AGENTS.md`, Testing), `web/AGENTS.md` (Tests) and `web/CLAUDE.md` (commands).

**Follow-up for `home-view-ui-contract`.** Rebase it onto this change. Its plan names `npm --prefix web run e2e:prepare && npm --prefix web run e2e` as a criterion command in Phases 1–3. Those phases are done, but the Phase 4 text and any rerun should use `npm --prefix e2e run build:app && npm --prefix e2e test`.

**Verified locally (2026-09-30).**
- `npm --prefix e2e run typecheck` and `npm --prefix web run typecheck` pass.
- `npm --prefix e2e run build:app && npm --prefix e2e test` runs 2/2 (Chromium, Firefox).
- `ci.yml` parses.
- `git grep` finds no `web/e2e`, `e2e:prepare` or `playwright.config` references outside `context/archive` and `context/changes`.
