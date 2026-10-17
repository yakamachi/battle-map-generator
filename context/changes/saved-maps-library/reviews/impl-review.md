<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4, 5
- **Date**: 2026-10-18
- **Verdict**: APPROVED
- **Findings**: 0 critical, 0 warnings, 4 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Notes:
- **Scope:** `git diff main...HEAD`, 8 commits (3e9afdf, 161484e, 29e03e8, 396515c, 0a1c0b6, aeeb753, 58a957c, de54b5e). This review sits on top of the phase reports `impl-review-phase-1.md` … `impl-review-phase-5.md` and covers what only shows across phases.
- **Desired End State and the Summary success criteria all hold in the final tree:**
  - Save dialog, with the map shown from the Save response.
  - `/maps` lists only the owner's maps, newest first.
  - Open, Rename and Delete; the owner filter is in one query on every route.
  - The 100-map cap.
  - The isolation test, with its break-check.
  - The e2e lifecycle in Chromium and Firefox.
  - PRD v3 and roadmap S-05.
- **"What We're NOT Doing":** no violations.
  - `fixtures/` is untouched.
  - The OpenAPI document only gains lines.
  - generate changes only by the 16 KB cap; its error messages are byte-identical.
  - No CORS, antiforgery, new rate-limit policy, pagination, search or regenerate-from-saved.
- **Triage follow-through:** all 20 FIXED decisions in the phase 1–4 reports were spot-checked in the code, and none is PENDING. No unplanned files: every extra traces to a plan item or an accepted review extra.
- **Verified clean:**
  - **Migration:** `has-pending-model-changes` reports no changes. The script is additive only (CREATE TABLE with a cascade FK to AspNetUsers, CREATE INDEX, history insert) in one transaction, and the lock on AspNetUsers is brief. The deleted Phase 1 migration id never reached `main`.
  - **API ↔ web error mapping:** `title` and `request` keys, `not-found`, `rate-limited`.
  - **Routing:** `/api` 404 before the SPA fallback; deep links serve `index.html`.
  - **Security:** owner filters; the CSRF stance matches the endpoint metadata; no CORS; the account-delete cascade; no account data in module-level state.
  - **Query cost:** the list is an index seek on (OwnerId, CreatedAt) with a projection; Save is Count + generate + insert.
  - **CI:** api, web-tests, visual (workers 4), e2e (`library.spec.ts`, migration applied by db-up) and contract cover everything.
  - **Lockfile:** 73 additions, 0 changed, 0 removed.
  - **Ownership split:** respected.
- **Gates at review time:**
  - `dotnet test api.Tests`: 214/214.
  - web typecheck clean; `npm test`: 94/94 in Chromium and Firefox.
  - The e2e full run (11/11) and the visual gate (58/58) passed in their phases.
- **Accepted, no action:**
  - A 413 shows the generic "(error 413)" message; the client can't realistically send more than 16 KB.
  - The `System.Security.Cryptography.Pkcs` 9.0.11 NU1902/NU1903 warning predates this branch and belongs in a separate change.
  - The user's uncommitted CLAUDE.md lesson block (M4L5 → M5L5) stays out of this PR.
- **Process items raised by the drift pass:**
  - The Phase 5 SHA suffixes and this report are committed with the review fixes.
  - The Phase 5 review ran in parallel with this one (`impl-review-phase-5.md`).
  - Per the user's memory note, the closeout PR rewrites the Progress SHAs to main's squash commit.

## Findings

### F1 — CI does not check that migrations match the model

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/ci.yml (api job)
- **Detail**: This branch adds the first domain table. A later model change without a migration would pass CI, and deploy.yml would apply no schema change, so production would fail at run time. It is clean today (verified locally).
- **Fix**: Run `dotnet ef migrations has-pending-model-changes --project api` in the api job after `dotnet tool restore`.
- **Decision**: FIXED: ci.yml api job runs `dotnet ef migrations has-pending-model-changes --project api` after the restore; locally it reports no pending changes.

### F2 — Running the migration's Down deletes every saved map

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Migrations/20261017115141_SavedMaps.cs (Down); context/foundation/infrastructure.md
- **Detail**: `Down` drops `SavedMaps`. The deploy never runs it, but a manual rollback with `dotnet ef database update InitialAccountStore` against production would delete all users' maps.
- **Fix**: One line in infrastructure.md's operational story: roll back by redeploying the previous build (the old app ignores the extra table), never by running a migration's Down against production.
- **Decision**: FIXED: infrastructure.md Rollback line: never run a migration Down against production (SavedMaps Down drops every saved map); redeploy the previous build, which ignores the extra table.

### F3 — Stale wording in docs and comments

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: AGENTS.md:23; .github/workflows/ci.yml (visual job comment); web/AGENTS.md (visual bullet); context/changes/saved-maps-library/plan.md:91-104, :307
- **Detail**:
  - Root AGENTS.md: "If maps are ever saved, store the grid" — maps are now saved.
  - ci.yml's visual comment names only the home view.
  - web/AGENTS.md says "both specs share `visual/fixtures.ts`"; there are three.
  - The plan's Phase 1 §1 still lists `MapJson` with no inline superseded note.
  - The plan's 2.3 criterion text (:307) still says the round-trip tests go red; Progress row 2.3 and the phase 2 review say they stay green by design.
- **Fix**:
  - AGENTS.md: "Saved maps store the grid (as bytes), never only the seed".
  - ci.yml comment: home, auth and library views.
  - web/AGENTS.md: "all three specs".
  - plan.md Phase 1 §1: add "Superseded by Phase 2 §3"; align :307 with row 2.3.
- **Decision**: FIXED: root AGENTS.md (saved maps store the grid as bytes), ci.yml visual comment (home, auth and library views), web/AGENTS.md (all three specs), plan.md Phase 1 Â§1 superseded note and the 2.3 criterion text aligned with Progress row 2.3.

### F4 — The radix-ui meta-package for one primitive

- **Severity**: 💡 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Architecture
- **Location**: web/package.json:26; web/app/components/ui/dialog.tsx:3
- **Detail**: `radix-ui` 1.6.7 adds 73 lockfile entries even though only Dialog is imported. Tree-shaking keeps the bundle small (the Dialog chunk is 40.8 kB, 13.8 kB gzip), but the install and audit surface grow.
- **Fix A ⭐ Recommended**: Keep `radix-ui`.
  - Strength: It is what `shadcn add` writes and what web/AGENTS.md documents; future primitives (Popover, Tooltip) need no new dependency.
  - Tradeoff: A larger lockfile and more for `npm audit` to report.
  - Confidence: HIGH — no runtime cost was measured beyond the Dialog chunk.
  - Blind spot: Audit noise from unused sub-packages over time.
- **Fix B**: Switch to `@radix-ui/react-dialog` and adjust the import.
  - Strength: The smallest dependency set.
  - Tradeoff: It diverges from shadcn's generated import, and the visual gate and web tests must be rerun. A small change late in the branch.
  - Confidence: MED — the API is the same, but it is unverified here.
  - Blind spot: Whether shadcn's later `add` calls rewrite it back.
- **Decision**: ACCEPTED via Fix A: keep radix-ui (shadcn default, documented in web/AGENTS.md).
