<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Saved Maps Library

- **Plan**: context/changes/saved-maps-library/plan.md
- **Scope**: Phase 3 of 5
- **Reviewed phases**: 3
- **Date**: 2026-10-17
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Notes:
- **Plan drift:** every planned item in commit 396515c is a MATCH (§1–§5). `git diff 29e03e8 396515c -- api api.Tests` is empty.
  - The disclosed adaptations are benign: the adapted Dialog, `radix-ui ~1.6.7`, the tokens and the destructive variant, the `useMapCanvas` hook, the helper modules, fetching in effects instead of `clientLoader`, the confirmation inside the dialog, and visual workers capped at 4.
  - The accepted extras are: the "N of 100 saved maps" line; Delete treating a 404 as success; the generic load-error state on `/maps/:id`; and the atlas cached once per page load.
  - The minor drift is that "Seed: N" wraps onto its own line at 390 px. The preview is width-bound there, the `map-preview-fit` values are unchanged, and manual check 3.8 passed.
- **`generateMap`:** the old and new paths match in order and side effects: network, then 429, then 401 redirect, then http.
- **Verified clean:**
  - React: stale responses are cancelled on an id change or unmount; double-submit is prevented; `useMapCanvas` matches the old home logic (atlas retry, dim-while-loading, download only for the drawn map).
  - Security: titles render only as React text; the path id is encoded; the 401 redirect goes to a fixed URL.
  - Error mapping and accessibility: dialog titles and descriptions, focus return, unique row-button names, `aria-invalid` and `aria-describedby`.
  - The lockfile only adds Radix and its own dependencies; no existing versions changed.
  - The token contrast was recomputed and matches `tokens.md` (destructive 5.42 / 4.67).
- **Noted, no action:**
  - Library 4xx and 5xx responses are not sent to `reportError`. That matches `generateMap` and `auth.ts`, and the API's Sentry captures server 500s.
  - Going from one `/maps/:id` straight to another dims the previous map, as home does. No link in the app goes directly between two ids.
  - The Dialog chunk is 40.8 kB (13.8 kB gzip) on the home route.
- **Success criteria:**
  - 3.1 typecheck, 3.2 `npm test` (90/90 in Chromium and Firefox), 3.3 `ui:scan`, 3.4 `visual:docker` (58/58 twice at 4 workers) and 3.5 build all pass.
  - Break-check: mapping 404 to something else turned "a 404 is not-found" red in both browsers.
  - E2E: 9/9 pass, so the existing locators are unaffected by the header links.
  - 3.6–3.8 were confirmed manually by the user in Chrome and Firefox. Enter not opening a native select is standard browser behaviour (Space or Alt+↓ open it).

## Findings

### F1 — Dialogs can be closed while their request is in flight

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/maps.tsx:195-228; web/app/components/map-title-dialog.tsx:63-85
- **Detail**: Only the confirm or submit button is disabled while `pending`, so Cancel, Escape and an outside click can still close the dialog.
  - **Delete:** the row is removed in `onCloseAutoFocus`, and only if `deleted.current` is already true when the dialog closes. If the user closes it mid-request, a delete that succeeds afterwards leaves a dead row (Open shows "Map not found", Rename shows a 404). A failure sets an error nobody sees.
  - **Save and Rename:** `pending` survives the close. Reopening the dialog shows "Saving…" with the button disabled, and the old response then lands in the new session.
- **Fix**: While `pending`, refuse to close: ignore `onOpenChange(false)`, disable Cancel, and keep Escape and outside clicks from closing (Radix `onEscapeKeyDown` / `onInteractOutside` → `preventDefault`). Apply this in both dialogs.
- **Decision**: FIXED: both dialogs ignore a close (Cancel, Escape, outside click) while their request is pending, and Cancel is disabled then. Checked in Chromium against the local app: with the DELETE held for 2 s, Escape left the dialog open, and the row left once the delete succeeded.

### F2 — Rename of an already-deleted map keeps its row

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/maps.tsx:160-174
- **Detail**: When Rename gets a 404 (the map was deleted in another tab), it shows "This map no longer exists" but leaves the row in the list. Delete, by contrast, removes the row on `not-found`.
- **Fix**: When Rename reports `not-found`, remove the row once the dialog closes, the same way Delete does (focus goes to the list heading).
- **Decision**: FIXED: MapTitleDialog takes onGone; a not-found rename calls it when the dialog closes, and maps.tsx drops the row and focuses the heading, as Delete does. Checked in Chromium with the PATCH faked to 404: the row left after Cancel and focus moved to the h1.

### F3 — Client tests skip the 429 and 401 paths of library calls

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: web/app/api/client.test.ts:30-87
- **Detail**: Nothing tests that a library call maps 429 to `rate-limited`, or that a 401 goes through `redirectIfSessionExpired` (`window.location.assign("/login?expired=1")`). A comment acknowledges the missing 401 test. These are the two paths the shared helper exists for.
- **Fix**: Add a 429 → `rate-limited` test, and a 401 test that stubs `window.location.assign` and checks the redirect URL and the `{ kind: "http", status: 401 }` result.
- **Decision**: FIXED: a 401 test (observed through the new exported sessionRedirect, since browser mode cannot stub window.location) and a 429 test. Break-check: the 401 test went red in both browsers when the helper ignored 401.
