# Saved Maps Library Implementation Plan

## Overview

A logged-in DM can save the generated map with a title, list their own saved maps, open one (it renders exactly as when saved), rename it and delete it. This is the first domain entity the app stores: it closes the 10xBuilder "CRUD on a domain entity" gap (`change.md`) and pulls "Zapis mapy w bibliotece z tytułem" forward from the PRD Non-Goals into a new roadmap slice, S-05.

## Current State Analysis

From `context/changes/saved-maps-library/research.md`:

- **No domain persistence.** `AppDbContext` holds only Identity and Data Protection keys (`api/Data/AppDbContext.cs:10-14`). There is one migration (`InitialAccountStore`), user ids are `AspNetUsers.Id nvarchar(450)`, and deploy runs an EF bundle before the app starts (`.github/workflows/deploy.yml:50-54, 132-143`), so migrations must stay additive.
- **No ownership pattern.** No handler reads the caller's id or filters data by owner yet. The `NameIdentifier` claim only partitions the generate limiter (`api/Program.cs:64`).
- **The map contract already carries everything needed to re-render.** `GeneratedMap(Seed, Parameters, Width, Height, Cells, Rooms)` (`api/Maps/MapModels.cs:46`). The web renderer uses `seed/width/height/cells` (`web/app/map/tileset.ts:22`), and sprite variants come from the seed (`tileset.ts:70`). A map is ~4.8 KB at 30×20 and ~12 KB at the generator's real maximum of 54×30.
- **The generator is a pure function of seed and parameters.** `GenerateMapRequest` already takes an optional `seed`, and `BspGenerator.Generate(seed, parameters)` (`api/Maps/MapEndpoints.cs:70`) can regenerate on save.
- **A rule clashes with REST verbs.** `api/AGENTS.md:18`: "every state-changing endpoint must stay a POST that takes JSON only", pinned by `AuthEndpointTests.cs:453-474`.
- **Defaults that already fit.** New `/api/maps/...` routes with `RequireAuthorization()` inherit the database-touch rule (`lessons.md`), the SessionCookieGate 60/min-per-IP limit (`api/Auth/SessionCookieGate.cs:62-63`) and the `/api` 404 fallback (`Program.cs:237`).
- **Web.** Routes are `protected.tsx` (layout) → `home.tsx`, plus login/register (`web/app/routes.ts:3-7`). The 401 → `/login?expired=1` redirect exists only in `generateMap` (`web/app/api/client.ts:66-71`). There is no Dialog primitive (`web/app/components/ui/`). The preview height budget lives in `web/app/app.css:89-95`.
- **Docs still exclude the feature:** PRD `:61-62`, `:175-176`, `:182`; shape-notes `:27-28`, `:277-278`; roadmap Parked `:162`; tech-stack docs `:29`; infrastructure `:38`, `:170`; test-plan `:60-62`, `:199`.

## Desired End State

- A DM who has generated a map clicks **Save**, enters a title in a dialog, and the map is stored.
- **My maps** in the header opens `/maps`, which lists only this account's maps, newest first, with title, size and date.
- From the list the DM can **Open** a map (`/maps/:id` shows the same preview and Download PNG as when it was saved), **Rename** it (dialog) and **Delete** it (confirm dialog).
- Another account's map is never visible: every read, rename and delete answers 404 for an id the caller does not own.
- An account holds at most 100 saved maps.
- The PRD, roadmap (S-05), shape-notes, tech-stack, infrastructure and test-plan docs describe the library as in scope.

Verify with: `dotnet test api.Tests`, `npm run typecheck && npm test && npm run ui:scan` and the visual gate in `web/`, the e2e lifecycle spec in Chrome and Firefox, the CI `contract` job (no OpenAPI/schema drift), and a manual run against the local stack.

### Key Discoveries:

- The generate request already accepts a seed. Save sends `title + seed + parameters` and reuses `GenerateMapRequest`'s validation rules (`api/Maps/MapModels.cs:52-85`).
- The body is read and validated by hand, with camelCase error keys (`MapEndpoints.cs:81-137`, lessons "Triage a .NET native swap"). New endpoints use the same approach rather than duplicating it.
- `{id:guid}` route constraints keep `/api/maps/{id}` from colliding with `POST /api/maps/generate`. A non-guid id falls through to the `/api` 404 fallback.
- A two-account isolation test pattern exists (`api.Tests/AuthEndpointTests.cs:234-245`), and so does the unreachable-database 401 test (`MapEndpointTests.cs:123-136`).
- The e2e budget is documented in `e2e/AGENTS.md`: 6 of the 10/min generate permits are used per run today.

## What We're NOT Doing

- No campaign → session → map hierarchy, sharing, or public maps (still PRD Non-Goals).
- No editing of a saved map's grid or parameters; only the title changes. "Regenerate" from a saved map is not part of this change (S-02 owns regenerate).
- No unique titles, no search, filter, sort options or pagination (the 100-map cap bounds the list).
- No new rate-limit policy for library routes. Only the existing SessionCookieGate 60/min-per-IP applies, and Save does **not** spend a `generate` permit. Save still runs the generator, so it sits outside both the per-account generate limit and the 50/min global generate ceiling (impl-review phase 1 F2, accepted). This residual is recorded in `infrastructure.md` in Phase 5.
- No change to the wire format. `cells` stays a JSON array of kind names on the API, and the fixtures keep their current shape. Moving the wire format to bytes is recorded as a 10xArchitect candidate, to be found and ranked through the Module 4 refactor-opportunities workflow (impl-review phase 1, triage 2026-10-17).
- No storing of images, atlas or renderer versions. A saved map re-renders with the current atlas.
- No global storage ceiling across accounts. The residual risk is recorded in `infrastructure.md`.
- No antiforgery tokens or CORS configuration. The rewritten rule relies on the absence of CORS plus `SameSite=Lax`.
- No change to the generate endpoint's behaviour or contract.

## Implementation Approach

API first, so the contract (OpenAPI + `schema.d.ts`) lands in one commit with its tests. Then the web UI against the generated types, then the e2e lifecycle spec against the real stack, then the foundation docs.

**Save regenerates the map on the server.** The request carries `title, seed, roomCount, encounter, bossSize`, and the server runs `BspGenerator.Generate` and stores the full `GeneratedMap` JSON. Stored maps are therefore always genuine generator output, and the "store the grid, not only the seed" rule holds.

**Every query is scoped by the caller's `NameIdentifier`.** A map that doesn't exist and a map someone else owns both answer 404.

## Critical Implementation Details

- **Ownership by query shape.** Every load, rename and delete must filter on `Id == id && OwnerId == callerId` in the same query. Loading by id and then comparing owners would need a second code path and would make it easy to leak a 403 or a timing difference.
- **Save vs. a deploy in between.** If the algorithm changes between Generate and Save, the regenerated grid differs from the one on screen. This is accepted as rare. The client therefore must show the saved map from the **Save response**, not keep the old in-memory grid.
- **Cap race.** The 100-map check is a count-then-insert. Two concurrent saves at 99 can produce 101. This is accepted (a soft cap); note it in the code comment.

### Addendum (impl-review phase 1, 2026-10-17)

- **Storage changes from one JSON column to compact columns.** Phase 1 stored the whole `GeneratedMap` as `MapJson nvarchar(max)` (~10–25 KB per map, since nvarchar is UTF-16). The list deserialized every row just to read the parameters (F1, F3). The user ruled out JSON in SQL Server.
- **The new Phase 2 ("Compact storage")** stores:
  - the parameters as columns;
  - the cells as one byte per cell;
  - the rooms as 5 bytes each.

  The list reads summary columns only. It also takes in the triaged fixes F4 (rename vs delete race), F5 (no-Content-Type test), F6 (16 KB request size limit), F7 (test-clock comment) and F8 (control characters in titles).
- **The later phases are renumbered:** Web 2→3, E2E 3→4, Docs 4→5.
- **Phase 1 stays as committed (3e9afdf).** Its `MapJson` description is superseded by Phase 2.

## Phase 1: API and contract

### Overview

Add the `SavedMap` table and migration, and five owner-scoped endpoints with validation and the cap. Rewrite the POST-only rule with its reasoning, regenerate the OpenAPI document and web types, and cover it all in `api.Tests`.

### Changes Required:

#### 1. Entity, context and migration

**Files**: `api/Data/SavedMap.cs` (new), `api/Data/AppDbContext.cs`, `api/Migrations/<timestamp>_SavedMaps.cs` (new, generated)

**Intent**: Store one row per saved map, owned by an Identity user. Deleting the account deletes its maps.

**Contract**: Table `SavedMaps`:

| Column | Type | Notes |
| --- | --- | --- |
| `Id` | `uniqueidentifier` | PK |
| `OwnerId` | `nvarchar(450)` | FK → `AspNetUsers.Id`, cascade delete |
| `Title` | `nvarchar(80)` | required |
| `Seed` | `bigint` | holds the `uint` |
| `Width` | `int` | |
| `Height` | `int` | |
| `MapJson` | `nvarchar(max)` | the full `GeneratedMap` serialized with `MapJson` options |
| `CreatedAt` | `datetimeoffset` | |
| `UpdatedAt` | `datetimeoffset` | |

- Index on `(OwnerId, CreatedAt)`.
- `AppDbContext` gets `DbSet<SavedMap> SavedMaps` and an `OnModelCreating` that calls `base` first.
- The migration only adds things, and its `Down` drops the table.
- Timestamps come from an injected `TimeProvider` (register `TimeProvider.System`) so tests can control them.

#### 2. Library endpoints

**Files**: `api/Maps/SavedMapEndpoints.cs` (new), `api/Maps/SavedMapModels.cs` (new), `api/Program.cs` (register after `MapMapEndpoints`, before the fallbacks)

**Intent**: CRUD for the caller's own maps. Each route uses `RequireAuthorization()`, scopes every query by the caller's `NameIdentifier`, returns typed results that appear in OpenAPI, and reads and validates bodies by hand like generate does.

**Contract**:

Routes:

| Route | Body | Success | Errors |
| --- | --- | --- | --- |
| `GET /api/maps` | — | `200 SavedMapSummary[]`, newest `CreatedAt` first, ties by `Id` | 401 |
| `GET /api/maps/{id:guid}` | — | `200 SavedMap` | 404, 401 |
| `POST /api/maps` | `SaveMapRequest { title, seed (required), roomCount?, encounter?, bossSize? }` | `201 SavedMap` with `Location` | 400 validation problem (`title`, the generate field keys, or `request` for the cap and broken JSON), 401 |
| `PATCH /api/maps/{id:guid}` | `RenameMapRequest { title }` | `200 SavedMap` (bumps `UpdatedAt`) | 400, 404, 401 |
| `DELETE /api/maps/{id:guid}` | — | `204` | 404, 401 |

Response shapes:
- `SavedMapSummary { id, title, seed, width, height, parameters, createdAt, updatedAt }`
- `SavedMap { id, title, createdAt, updatedAt, map: GeneratedMap }`

Rules:
- **Title:** trimmed, then 1–80 characters, duplicates allowed. The message is shared by Save and Rename and keyed `title`.
- **Cap:** `SavedMapEndpoints.MaxSavedMapsPerAccount = 100`. At or above the cap, Save answers 400 keyed `request` ("You can keep at most 100 saved maps. Delete one to save another.").
- **JSON only:** POST and PATCH declare `.Accepts<…>("application/json")`, so other content types fall to the `/api` 404 like generate.
- **Shared validation:** the generate validation helpers (body read, `BindingError`, `Validate`) are shared with these endpoints, not duplicated. Moving them into a small internal helper is fine, as long as generate's error keys and messages stay byte-identical.
- **Database touch:** no route resolves `AppDbContext` before authorization has passed.

#### 3. API rules

**File**: `api/AGENTS.md`

**Intent**:
- Replace the POST-only sentence with the actual CSRF stance.
- Add a "Saved maps" section: routes, ownership-by-query, 404 for not-owned, title rule, cap, Save regenerates and spends no generate permit, and the database-touch status.

**Contract**: The new rule text says:
- State-changing endpoints take JSON only (POST/PATCH) or no body (DELETE), and never accept form or `text/plain` content types.
- With no CORS policy and a `SameSite=Lax` cookie, a cross-site page can't send them with the session. JSON, PATCH and DELETE need a preflight that no origin passes.
- Adding CORS, or relaxing the content-type rule, requires antiforgery tokens first.
- GET never changes state.
- `AuthEndpointTests` and `SavedMapEndpointTests` pin this.

#### 4. Contract artifacts

**Files**: `api/BattleMapGenerator.Api.json`, `web/app/api/schema.d.ts`

**Intent**: Regenerate both (`dotnet build api`, then `npm run api:types` in `web/`) and commit them together with the API.

**Contract**: New paths and the schemas `SaveMapRequest`, `RenameMapRequest`, `SavedMap` and `SavedMapSummary`. Existing schemas stay unchanged.

#### 5. Tests

**File**: `api.Tests/SavedMapEndpointTests.cs` (new)

**Intent**: Pin behaviour and isolation through the HTTP API, following the `MapEndpointTests`/`AuthEndpointTests` patterns: a fresh factory per test, unique emails, and fresh databases for tests that count rows.

**Contract**: Named tests (wording may adapt):
- `Saved_map_round_trips_with_the_generated_grid`: Save, then Get, equals `BspGenerator.Generate(seed, params)`.
- `List_shows_only_own_maps_newest_first`
- `Another_accounts_map_is_404_for_get_rename_and_delete`: two accounts, and B's map is untouched afterwards.
- `Rename_changes_title_and_updated_at_only`
- `Delete_removes_the_map_and_a_second_delete_is_404`
- `Title_rules_are_enforced_on_save_and_rename`: empty, whitespace and 81 characters give 400 keyed `title`; 80 characters and duplicates are fine; the title is trimmed.
- `Invalid_parameters_on_save_use_the_generate_messages`
- `Save_is_refused_at_the_100_map_cap`: seed 100 rows directly through `SqlServerFixture.CreateContext`, then expect 400 keyed `request`.
- `Library_routes_answer_401_without_a_session_and_never_touch_the_database`: unreachable-database pattern.
- `Form_and_text_bodies_are_refused_on_save_and_rename`: no 2xx, nothing stored.
- `Save_does_not_spend_a_generate_permit`: after 10 saves, generate still answers 200.
- `Non_guid_id_falls_to_the_api_404`

`AccountStoreTests`' "no pending migrations" check covers the migration.

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test api.Tests`
- Contract has no drift: `dotnet build api && (cd web && npm run api:types) && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts` (run after the files are committed or staged)
- Web still typechecks against the new schema: `cd web && npm run typecheck`
- Break-check: dropping the owner filter from the get query turns `Another_accounts_map_is_404_for_get_rename_and_delete` red

#### Manual Verification:

- Against the local stack (`dotnet ef database update` applied), `api/BattleMapGenerator.Api.http` or curl with a session cookie shows the five routes behaving as in the table above

**Implementation Note**: After this phase passes, pause for manual confirmation and run `/10x-impl-review saved-maps-library phase 1` before Phase 2 (compact storage).

---

## Phase 2: Compact storage

### Overview

Replace the `MapJson` column with compact columns: parameters as columns, cells and rooms as bytes. The list then never touches a grid. Fold in the triaged phase 1 review fixes F4–F8. The API contract (OpenAPI, `schema.d.ts`) and the wire format do not change.

### Changes Required:

#### 1. Persisted enum numbers

**File**: `api/Maps/MapModels.cs`

**Intent**: The storage now records cell and room kinds by number, so give `CellKind` and `RoomKind` explicit values and mark them as persisted. JSON keeps using names, so the wire is unaffected.

**Contract**:
- `CellKind`: Void=0, Floor=1, Corridor=2, Wall=3, Door=4, BossArena=5.
- `RoomKind`: explicit values in their current order.
- Add a comment on both: "persisted in SavedMaps: append new values, never renumber".

#### 2. Grid codec

**File**: `api/Maps/GridCodec.cs` (new; or `api/Data/` if it reads better next to the entity)

**Intent**: One place that turns cells and rooms into bytes and back. Decoding validates what it reads instead of trusting the row.

**Contract**:
- `byte[] EncodeCells(CellKind[])` stores one byte per cell, row-major.
- `CellKind[] DecodeCells(byte[], int width, int height)` checks that the length is `width*height` and that every value is defined.
- `byte[] EncodeRooms(Room[])` stores 5 bytes per room: X, Y, Width, Height (each ≤ 60) and Kind.
- `Room[] DecodeRooms(byte[])` checks that the length is a multiple of 5 and that every kind is defined.
- Invalid input throws `InvalidDataException`.

#### 3. Entity and migration

**Files**:
- `api/Data/SavedMap.cs`, `api/Data/AppDbContext.cs`
- `api/Migrations/*_SavedMaps.*` (regenerated)
- `api/Migrations/AppDbContextModelSnapshot.cs`

**Intent**:
- Drop `MapJson`.
- Add `RoomCount int`, `Encounter nvarchar(16)` (enum name via value conversion, matching the JSON spelling), `BossSize nvarchar(16) NULL`, `Cells varbinary(3600)` (60×60 cap) and `Rooms varbinary(60)` (12 rooms × 5 bytes).
- Keep every other Phase 1 column, the FK and the index.

**Contract**:
- The Phase 1 migration has not been merged or deployed. Delete it and regenerate one `SavedMaps` migration instead of stacking a second one.
- Local databases that already applied it must first be rolled back with `dotnet ef database update InitialAccountStore --project api`. This covers the dev database and the e2e compose database if `npm run db:up` ran.
- The regenerated migration still only adds things.

#### 4. Endpoints use the columns

**File**: `api/Maps/SavedMapEndpoints.cs`

**Intent**:
- **Save** writes the parameter columns and the encoded cells and rooms from the generated map.
- **List** projects summary columns only with `.Select(...)`, so the query never reads `Cells` or `Rooms`.
- **Get** and **Rename** rebuild `GeneratedMap` from the columns plus the decoded bytes.
- **F4:** Rename catches `DbUpdateConcurrencyException` (a concurrent delete) and returns 404.
- **F6:** `generate`, Save and Rename get a 16 KB request size limit. Verify end to end that an oversized body is refused (413) under TestServer as well. If the metadata isn't honoured there, cap the read in `RequestBody.ReadAsync` instead (lessons: verify a framework feature against what it actually does).

**Contract**: Response shapes are unchanged (`SavedMapSummary`, `SavedMap`). `parameters` in the list comes from the columns.

#### 5. Title rule

**File**: `api/Maps/SavedMapModels.cs`

**Intent**: **F8:** reject titles containing control characters (`char.IsControl`, which covers newlines and tabs) under the existing `title` message, after trimming.

**Contract**: Same key and message as the length rule.

#### 6. Tests and test infrastructure

**Files**: `api.Tests/SavedMapEndpointTests.cs`, `api.Tests/GridCodecTests.cs` (new), `api.Tests/Infrastructure/ApiFactory.cs`

**Intent**:
- **Codec tests:**
  - round-trips for every fixture grid;
  - a wrong cell length, an undefined kind and a room-bytes length that isn't a multiple of 5 are each rejected;
  - the persisted enum numbers are pinned, so renumbering an enum turns a test red.
- **Endpoint tests:**
  - the existing round-trip test still compares with `BspGenerator.Generate`;
  - the list works when a row's `Cells` is corrupted (written directly through `SqlServerFixture.CreateContext`), which proves the list never decodes grids;
  - **F5:** a POST/PATCH with no Content-Type is refused and stores nothing;
  - **F6:** a body over 16 KB on Save, Rename and generate is refused;
  - **F8:** titles with a newline or a control character get 400 keyed `title`.
- **F7:** the `ApiFactory.Time` comment says the override also drives the clocks for the login cookie, account lockout and the security stamp.

**Contract**: The existing Phase 1 test names stay.

#### 7. API rules

**File**: `api/AGENTS.md`

**Intent**:
- Update the "Saved maps" section with the storage layout and the persisted enum-number rule.
- Note the request size limit.
- Record that Save sits outside the generate limits (F2, accepted).

**Contract**: Prose only.

### Success Criteria:

#### Automated Verification:

- API tests pass: `dotnet test api.Tests`
- Contract unchanged: `dotnet build api && (cd web && npm run api:types) && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts` (relative to Phase 1's commit)
- Break-check: swapping two values in the cell encoding (for example Floor and Wall) turns the codec and round-trip tests red

#### Manual Verification:

- After rolling the local database back and re-applying the regenerated migration, save, list, open, rename and delete work against the local stack. The `SavedMaps` table shows `Cells`/`Rooms` as bytes and has no JSON column.

**Implementation Note**: After this phase passes, pause for manual confirmation and run `/10x-impl-review saved-maps-library phase 2` before Phase 3.

---

## Phase 3: Web UI

### Overview

Add library calls to the API client, a Dialog primitive, Save on the home view, the `/maps` list and `/maps/:id` view, rename and delete dialogs, the header link, and visual-gate coverage.

### Changes Required:

#### 1. API client

**File**: `web/app/api/client.ts` (and a test if a pure helper is extracted)

**Intent**: Add the library calls with the same `{ok}|{ok:false,error}` results and the `reportError` choke point. Move the 401 → `/login?expired=1` redirect into one shared helper that `generateMap` and the new calls all use.

**Contract**:
- Types: `SavedMap`, `SavedMapSummary`.
- Functions: `listSavedMaps()`, `getSavedMap(id)`, `saveMap({ title, seed, roomCount, encounter, bossSize })`, `renameSavedMap(id, title)`, `deleteSavedMap(id)`.
- Error kinds add `not-found` (404) and `invalid` (400, carrying the `errors` record, so the dialog can show the title message or the cap message).
- `generateMap`'s observable behaviour is unchanged.

#### 2. Dialog primitive

**Files**: `web/app/components/ui/dialog.tsx` (new), `web/app/app.css`, `web/package.json` (if `shadcn add` brings a dependency)

**Intent**: An accessible modal for the save title, rename and delete confirmation. It is added per `web/AGENTS.md:33-36`.

**Contract**:
- Remove any `.dark` block or `@custom-variant` the CLI writes.
- Add any new token (for example `--popover`, overlay) by hand in `:root` and the dark media block, with the contrast checked and recorded.
- `npm run ui:scan` passes.

#### 3. Save on home

**File**: `web/app/routes/home.tsx`

**Intent**: Add a **Save** button in the action row, enabled when a map is rendered. It opens the dialog with a title input (required, max 80, trimmed), shows field errors from the API, and on success shows a confirmation with a link to the saved map.

**Contract**:
- The dialog adds no height above the preview (`app.css:89-95` budget unchanged).
- Save sends the current map's `seed` and `parameters`.

#### 4. Library routes

**Files**: `web/app/routes.ts`, `web/app/routes/maps.tsx` (new), `web/app/routes/saved-map.tsx` (new), `web/app/routes/protected.tsx`

**Intent**:
- `/maps` lists this account's maps, with title, `width×height`, encounter and date.
- Each row has Open, Rename (dialog prefilled with the title) and Delete (confirm dialog that names the title). The list has an empty state that links back to the generator.
- `/maps/:id` loads one map and shows its title, the preview and Download PNG, using the same render and download code as home. A 404 shows a "Map not found" state with a link to `/maps`.
- The header gains a **My maps** link (and a link back to the generator).

**Contract**:
- Both routes sit under the `protected.tsx` layout.
- Rendering and download reuse `renderMap` and `download.ts`. Extract the atlas, render and download logic shared with `home.tsx` into one module rather than copying it.
- The download filename stays `battle-map-<seed>.png`.

#### 5. Visual gate and web rules

**Files**: `web/visual/library.visual.spec.ts` (new), `web/visual/fixtures.ts` (mock library routes if needed), `web/visual/__screenshots__/…`, `web/AGENTS.md`

**Intent**:
- Baseline the new states: the save dialog, the list with maps, the empty list, the delete-confirm dialog, the saved-map view, and the not-found view. Use the mocked API, in all three visual projects.
- Update the home baseline if the action row changes.
- Document the library routes and the shared 401 helper in `web/AGENTS.md`.

**Contract**: Baselines are updated only through `npm run visual:update`.

### Success Criteria:

#### Automated Verification:

- Typecheck passes: `cd web && npm run typecheck`
- Unit and render tests pass: `cd web && npm test`
- UI token scan passes: `cd web && npm run ui:scan`
- Visual gate passes with the new baselines: `cd web && npm run visual:docker`
- Production build succeeds: `cd web && npm run build`

#### Manual Verification:

- In the dev app: generate, save with a title, find the map in My maps, open it (same picture and seed as before), rename it, and delete it after confirming. Done in Chrome and Firefox.
- The keyboard alone can open, submit and cancel each dialog. Focus returns to the triggering button.
- The preview height on home is unchanged at desktop and phone widths.

**Implementation Note**: After this phase passes, pause for manual confirmation and run `/10x-impl-review saved-maps-library phase 3` before Phase 4.

---

## Phase 4: End-to-end lifecycle

### Overview

Prove the whole user path against the real stack in Chrome and Firefox.

### Changes Required:

#### 1. Lifecycle spec

**File**: `e2e/tests/library.spec.ts` (new)

**Intent**: Each browser runs the full path through the UI, finding elements by role and name:
1. Generate a map, then Save it with a unique title (browser name + timestamp).
2. See it in My maps.
3. Open it and see the same `Seed: N` as on home.
4. Rename it and see the new title in the list.
5. Delete it after confirming, and see it gone from the list.

**Contract**:
- One generate per browser.
- The spec deletes what it creates. A run interrupted mid-test leaves at most one uniquely titled row, which is harmless.

#### 2. E2E budget

**File**: `e2e/AGENTS.md`

**Intent**: Record the new spec and the updated budgets.

**Contract**:
- Generate: 6 → 8 of 10 per minute.
- Auth calls: unchanged.
- Library calls go through the cookie gate's 60/min per IP and stay well under it.

### Success Criteria:

#### Automated Verification:

- E2E specs typecheck: `cd e2e && npm run typecheck`
- Full e2e run passes in Chromium and Firefox: `cd e2e && npm run db:up && npm run build:app && npm test`
- Break-check: making the delete handler return 204 without deleting turns `library.spec.ts` red

#### Manual Verification:

- After a full e2e run, the local database holds no leftover library rows for `e2e@example.com`

**Implementation Note**: After this phase passes, pause for manual confirmation and run `/10x-impl-review saved-maps-library phase 4` before Phase 5.

---

## Phase 5: Foundation docs

### Overview

Move the library from "deferred" to "in scope" everywhere the product and operations docs say otherwise. Keep each document's language: the PRD, roadmap and shape-notes are Polish.

### Changes Required:

#### 1. Product scope

**Files**: `context/foundation/prd.md`, `context/foundation/shape-notes.md`

**Intent**:
- **PRD:** add a functional requirement for the saved-map library (save with title; list own; open; rename; delete; owner-only; 100 per account) and user-story acceptance criteria. Take "Zapis mapy w bibliotece z tytułem" out of Non-Goals (`:175-176`, `:182`; the Kampania → Sesja → Mapa hierarchy stays a Non-Goal). Adjust the secondary success criterion (`:61-62`), and record a version bump with a dated note on why (certification CRUD requirement).
- **Shape-notes:** add a dated note that reverses the "odłożona do etapu 2" decision (`:27-28`, `:277-278`).

**Contract**: The PRD frontmatter version is bumped, and existing FR numbers are not renumbered.

#### 2. Roadmap

**File**: `context/foundation/roadmap.md`

**Intent**:
- Add slice **S-05** "DM zapisuje mapę w bibliotece i nią zarządza" with Change ID `saved-maps-library`, prerequisites S-01 and S-04, the PRD refs and `Status: in-progress`, in both the At-a-glance table and the Slices body.
- Remove the Parked line `:162`.
- Bump `updated`.

**Contract**: The roadmap template shape holds, so `/10x-archive` can close S-05 by Change ID.

#### 3. Stack, infrastructure and test plan

**Files**:
- `context/foundation/tech-stack.md`, `tech-stack-api.md`, `tech-stack-web.md`
- `context/foundation/infrastructure.md`
- `context/foundation/test-plan.md`

**Intent**:
- **Tech-stack docs:** the `:29` lines describe stored maps (EF Core table with parameter columns and the grid as bytes) instead of "no persistence".
- **infrastructure.md:**
  - `:38` says maps are stored (~0.7–2 KB each including the bytes, ≤100 per account, so ≤ ~200 KB per account).
  - The `:170` backup row now includes saved maps.
  - Add a risk-register row: the storage growth residual (many scripted accounts × 100 maps; there is no global ceiling; registration's 50/min global limit bounds how fast accounts are created, not total storage; revisit with test-plan Phase 4).
  - Add a risk-register row (F2): Save runs the generator outside the generate limits (60/min per IP, no global cap; one generation is well under 1 ms; revisit with test-plan Phase 4).
  - Note that the free-offer storage limit is not recorded in the repo.
- **test-plan.md:**
  - Replace the ownership-attack (IDOR) negative space (`:60-62`) with a pointer to the isolation tests from Phase 1.
  - Remove "Map library, CRUD" from §7 (`:199`; offline mode stays).
  - Add a freshness-ledger entry.

**Contract**: No change to the test-plan rollout phases (4 and 5 stay as they are).

### Success Criteria:

#### Automated Verification:

- No stale "deferred" wording remains: `grep -n "biblioteka z tytułem\|bibliotece z tytułem\|not stored in the MVP\|Map library, CRUD" context/foundation/*.md` shows only the intended historical notes
- Roadmap carries the slice: `grep -n "saved-maps-library" context/foundation/roadmap.md` matches the At-a-glance row and the S-05 body

#### Manual Verification:

- The PRD, roadmap and infrastructure edits read correctly in Polish/English and agree with the implemented behaviour (cap 100, owner-only 404, title 1–80)

**Implementation Note**: After this phase, run `/10x-impl-review saved-maps-library phase 5`, then a full-plan review before opening the PR.

---

## Testing Strategy

### Unit Tests:

- Title normalisation and validation (trim, 1–80), if extracted as a pure function, in both `api.Tests` and the web client.

### Integration Tests:

- `api.Tests/SavedMapEndpointTests.cs`: CRUD round trip, ordering, two-account isolation (404 on get, rename and delete, with the victim's row unchanged), title and parameter validation, the cap, 401 without a database, refused content types, no generate permit spent, non-guid 404.
- Visual gate: the new library states with a mocked API.
- E2E: the lifecycle spec in Chromium and Firefox against the real host and SQL Server.

### Manual Testing Steps:

1. Generate a boss map, save it as "Boss lair", open My maps, open it, and compare its seed and picture with the original.
2. Log in as a second account in another browser, paste the first account's `/maps/<id>` URL, and see "Map not found".
3. Rename to an empty title (error shown), then to a valid one. Delete with Cancel (map stays), then Confirm (map gone).

## Performance Considerations

- Save runs one generation (well under 1 ms) plus one insert. From Phase 2 the list reads summary columns only, never `Cells`/`Rooms`. At most 100 rows come back per account, so no pagination is needed.
- Each library request with a session cookie touches the database, waking it from auto-pause. This is the same as any authenticated request today.

## Migration Notes

- The `SavedMaps` migration only adds things. The deploy bundle applies it before the new code runs, and the old code ignores the table.
- Rollback: redeploy the previous build. If the table itself must go, apply `Down` (drop the table) by hand; that is a human-only action under the root `AGENTS.md` rules.

## References

- Research: `context/changes/saved-maps-library/research.md`
- Generate endpoint pattern: `api/Maps/MapEndpoints.cs:25-137`
- Two-account isolation pattern: `api.Tests/AuthEndpointTests.cs:234-245`
- Unreachable-database 401 pattern: `api.Tests/MapEndpointTests.cs:123-136`
- Client choke point and 401 redirect: `web/app/api/client.ts:41-74`
- Prior decision to defer the library: `context/foundation/shape-notes.md:27-28`, `context/archive/2026-09-23-account-store-foundation/plan.md:36`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: API and contract

#### Automated

- [x] 1.1 API tests pass: `dotnet test api.Tests` — 3e9afdf
- [x] 1.2 Contract has no drift: `dotnet build api && (cd web && npm run api:types) && git diff --exit-code api/BattleMapGenerator.Api.json web/app/api/schema.d.ts` — 3e9afdf
- [x] 1.3 Web still typechecks against the new schema: `cd web && npm run typecheck` — 3e9afdf
- [x] 1.4 Break-check: dropping the owner filter turns the isolation test red — 3e9afdf

#### Manual

- [x] 1.5 Five routes behave as specified against the local stack — 3e9afdf

### Phase 2: Compact storage

#### Automated

- [x] 2.1 API tests pass: `dotnet test api.Tests` — 161484e
- [x] 2.2 Contract unchanged: OpenAPI json and `schema.d.ts` show no drift — 161484e
- [x] 2.3 Break-check: swapping two cell encodings turns the codec pin tests red (round-trip stays green by design) — 161484e

#### Manual

- [x] 2.4 Local stack works after rollback and re-applied migration; table holds bytes, no JSON — 161484e

### Phase 3: Web UI

#### Automated

- [x] 3.1 Typecheck passes: `cd web && npm run typecheck`
- [x] 3.2 Unit and render tests pass: `cd web && npm test`
- [x] 3.3 UI token scan passes: `cd web && npm run ui:scan`
- [x] 3.4 Visual gate passes with the new baselines: `cd web && npm run visual:docker`
- [x] 3.5 Production build succeeds: `cd web && npm run build`

#### Manual

- [x] 3.6 Full save/list/open/rename/delete path works in Chrome and Firefox
- [x] 3.7 Dialogs are keyboard-operable and return focus
- [x] 3.8 Home preview height unchanged at desktop and phone widths

### Phase 4: End-to-end lifecycle

#### Automated

- [ ] 4.1 E2E specs typecheck: `cd e2e && npm run typecheck`
- [ ] 4.2 Full e2e run passes in Chromium and Firefox
- [ ] 4.3 Break-check: a delete that doesn't delete turns `library.spec.ts` red

#### Manual

- [ ] 4.4 No leftover library rows for the e2e account after a full run

### Phase 5: Foundation docs

#### Automated

- [ ] 5.1 No stale "deferred" wording remains in `context/foundation/`
- [ ] 5.2 Roadmap carries S-05 with Change ID `saved-maps-library`

#### Manual

- [ ] 5.3 Doc edits read correctly and agree with the implemented behaviour
