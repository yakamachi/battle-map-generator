# Saved Maps Library — Plan Brief

> Full plan: `context/changes/saved-maps-library/plan.md`
> Research: `context/changes/saved-maps-library/research.md`

## What & Why

A logged-in DM can save a generated map with a title, list their own saved maps, open one, rename it and delete it. Today maps are only generated and downloaded, never stored, so the project has no CRUD on a domain entity. That is the remaining 10xBuilder certification gap (target 2026-11-04). The change pulls "Zapis mapy w bibliotece z tytułem" forward from the PRD Non-Goals into a new roadmap slice, S-05.

## Starting Point

- The database holds only Identity and Data Protection keys, and no code filters data by owner yet.
- The generate response (`GeneratedMap`: seed, parameters, size, semantic cells, rooms) already carries everything the web renderer needs.
- The generator is a pure function of seed and parameters.

## Desired End State

- **Save** on the home view opens a title dialog.
- **My maps** (`/maps`) lists only the caller's maps, newest first.
- Each map can be opened (`/maps/:id`, same preview and PNG as when saved), renamed or deleted after confirmation.
- Another account's map is invisible, answering 404 everywhere.
- Each account holds at most 100 maps.
- The PRD, roadmap and ops docs describe the library as in scope.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| HTTP methods | GET/POST/PATCH/DELETE under `/api/maps`; the POST-only rule in `api/AGENTS.md` is rewritten with its CSRF reasoning | Conventional CRUD; with no CORS and a `SameSite=Lax` cookie, a cross-site page can't send JSON, PATCH or DELETE with the session | Plan |
| Not-owned map | 404, the same as a missing map (owner filter in every query) | Leaks nothing about other accounts and needs one code path | Plan |
| What Save stores | Client sends title + seed + parameters; the server regenerates and stores the full grid (as bytes, from Phase 2) | Stored maps are always genuine generator output and there is no untrusted grid to validate; still "store the grid, not only the seed" | Plan (options from Research) |
| Storage shape | One `SavedMaps` table: owner FK (cascade), title, seed, width, height, roomCount, encounter, bossSize, `Cells varbinary` (1 byte per cell), `Rooms varbinary` (5 bytes per room), timestamps; no JSON in SQL | ~0.7–2 KB per map instead of ~10–25 KB of UTF-16 JSON, and the list never reads a grid (changed in the phase 1 review, F1/F3) | Impl review |
| Wire format | Unchanged: the API still returns `cells` as kind names | Moving the wire to bytes is a cross-cutting contract migration, kept as a 10xArchitect candidate | Impl review |
| Limits | A soft cap of 100 maps per account; no new rate-limit policy; Save spends no generate permit, and its generator runs are a documented residual (F2) | Bounds one account to ~200 KB with little code; the cookie gate's 60/min per IP still applies | Plan / Impl review |
| Title | Required, trimmed, 1–80 characters, duplicates allowed | One simple rule shared by save and rename | Plan |
| UI | shadcn Dialog for save, rename and delete-confirm; `/maps` list and `/maps/:id` view; a My maps header link | Keeps the home preview height budget and gives clear CRUD pages | Plan |
| E2E | One lifecycle spec per browser (generate → save → list → open → rename → delete), cleaning up after itself | A user-perspective test of the whole path; generate budget 6 → 8 of 10 per minute | Plan |

## Scope

**In scope:**
- `SavedMaps` table and migration.
- Five owner-scoped endpoints with validation and the cap.
- Rewrite of the CSRF rule.
- OpenAPI and web types.
- `api.Tests` isolation and CRUD suite.
- Client calls with a shared 401 helper.
- Dialog primitive.
- Save, list and view pages.
- Visual baselines.
- E2E lifecycle spec.
- PRD, roadmap S-05, shape-notes, tech-stack, infrastructure and test-plan updates.

**Out of scope:**
- The campaign/session hierarchy, sharing.
- Editing a saved grid, regenerating from a saved map.
- Unique titles, search, pagination.
- A library rate-limit policy, a global storage ceiling.
- CORS or antiforgery.
- Any change to generate.

## Architecture / Approach

`web/` posts `{ title, seed, parameters }` to `POST /api/maps`. The API validates the request with the generate rules plus the title rule, checks the cap, runs `BspGenerator.Generate`, stores the parameters as columns and the cells and rooms as bytes with the owner's `NameIdentifier`, and returns it. `web/` renders from that response with the existing renderer. Reads, renames and deletes filter on `Id` and `OwnerId` in one query. The API owns data and access, and `web/` owns all visuals, matching the existing ownership split.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. API and contract | Table and migration, 5 endpoints, rule rewrite, OpenAPI + `schema.d.ts`, `api.Tests` | An ownership filter missed on one path (pinned by the two-account test and a break-check) |
| 2. Compact storage | Parameter columns, cells and rooms as bytes, a list that never reads grids; review fixes F4–F8 | Regenerating an unmerged migration: local databases must be rolled back first |
| 3. Web UI | Client calls, Dialog, Save, `/maps`, `/maps/:id`, rename/delete, visual baselines | Dialog tokens and theme contrast; visual baseline churn |
| 4. End-to-end lifecycle | One Chrome + Firefox spec over the real stack | Permit budget (8/10) and leftover rows if a run aborts |
| 5. Foundation docs | PRD FR and Non-Goals, roadmap S-05, shape-notes, tech-stack, infrastructure, test-plan | Polish docs drifting from the implemented numbers |

**Prerequisites:** S-01 and S-04 done (they are). Local SQL Server via `npm run db:up` in `e2e/`. Docker for `visual:docker`.
**Estimated effort:** about 4–5 sessions across 5 phases, each followed by `/10x-impl-review`.

## Open Risks & Assumptions

- S-02 (regenerate) is planned in parallel and also touches `home.tsx`, `client.ts` and the e2e specs. Whichever merges second rebases.
- A deploy between Generate and Save would store a different grid than the one on screen. This is accepted as rare, and the UI always shows the server's saved map.
- The 100-map cap is a soft count-then-insert, so a concurrent race can reach 101.
- Many scripted accounts can still grow total storage, since there is no global ceiling. This is recorded as a residual. The free-offer storage size isn't documented in the repo.
- Saved maps re-render with the current atlas. A future tileset change alters how old maps look, as intended.

## Success Criteria (Summary)

- A DM saves, finds, opens (same picture), renames and deletes their own maps in Chrome and Firefox. The e2e lifecycle spec proves it.
- A second account can never see or change the first account's maps. `api.Tests` proves it, and a deliberate break turns the test red.
- The PRD and roadmap list the library as delivered scope (S-05), with no remaining "deferred" wording.
