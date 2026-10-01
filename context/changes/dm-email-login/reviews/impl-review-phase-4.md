<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: DM Email Login (S-04)

- **Plan**: context/changes/dm-email-login/plan.md
- **Scope**: Phase 4 of 4
- **Reviewed phases**: 4
- **Date**: 2026-10-01
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

Commit 04c4819 (plus bookkeeping commit 8cd2767). Plan-drift review: clean MATCH across all 6 change groups (API modules, routes, UI primitives, visual gate, e2e auth flow, docs) and all 3 out-of-scope guards (`home.tsx`, `MapEndpoints.cs`, `map.spec.ts` body all confirmed untouched by this commit). No scope creep. The one documented adaptation — `HydrateFallback` moved from `protected.tsx` to the root route, since React Router 8 SPA mode permits it only there — was verified to still cover all three affected routes' `clientLoader`s before first paint.

Automated gates run before commit: web typecheck+build, `ui:scan`, web rendering tests (36/36), visual gate with regenerated baselines (34/34, 2 skipped on mobile by design), e2e in Chromium and Firefox including `auth.spec.ts` (7/7), `home.tsx`/`map.spec.ts` no diff vs `main`, API tests (67/67) and contract check (no drift). Two deliberate-break checks (register-success navigation, login error copy) both went red on broken code and were cleanly restored. Manual 4.8–4.10 confirmed interactively by the user (register/generate/download/logout/re-login, both themes, phone width, cookie-delete redirect, baseline diff review). 4.11–4.13 (deployed-app checks) remain open, per the plan's own note that they close out after merge.

## Findings

### F1 — Logout failure is silently swallowed and can bounce the user back to the page they meant to leave

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/protected.tsx:19-22
- **Detail**: `onLogout` runs `await logout(); navigate("/login");` unconditionally, ignoring `LogoutResult.ok`. If `logout()` fails (network blip, 5xx), the server session is never invalidated, yet the client navigates to `/login` anyway. `login.tsx`'s own `clientLoader` then calls `getSession()`, still finds a valid account, and `redirect("/")` — bouncing the user straight back to the page they just tried to leave, with zero feedback that anything went wrong.
- **Fix A ⭐ Recommended**: On failure, show an alert and stay put
  - Check `result.ok`; only navigate to `/login` on success. On failure, surface an `Alert` (e.g. "Could not log out — check your connection and try again") in the protected layout and leave the user where they are, so they get visible feedback and can retry.
  - Strength: Matches the pattern login.tsx/register.tsx already use for network/HTTP errors; no silent failure.
  - Tradeoff: One more state variable in `protected.tsx` (an error string) and a small UI addition.
  - Confidence: MEDIUM — the fix is mechanical, but the right wording/placement for the alert in a layout component (vs. a page) wasn't test-covered by this phase.
  - Blind spot: Whether a failed client-side logout should also attempt to clear local UI state (e.g. force a reload) wasn't considered.
- **Fix B**: Navigate regardless, accept the edge case
  - Document that `logout()` is treated as best-effort client-side cleanup; navigating to `/login` regardless is acceptable since a user who lands back on `/` via the redirect can simply click Log out again.
  - Strength: No code change.
  - Tradeoff: The silent bounce-back is confusing — the user clicked Log out, saw the page flash, and ended up back where they started with no explanation.
  - Confidence: LOW — this is a real UX gap with no mitigating control, not just a documented tradeoff.
  - Blind spot: How often `logout()` actually fails in production (likely rare, but untested).
- **Decision**: FIXED via Fix A — `protected.tsx` now checks `LogoutResult.ok`; on failure it shows a destructive `Alert` ("Could not log out. Check your connection and try again.") and does not navigate, so a failed logout no longer silently bounces the user back to the page they tried to leave.

### F2 — A validation error with an empty `errors` object would render a blank alert

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: web/app/routes/register.tsx:24
- **Detail**: `Object.values(error.errors).flat().join(" ")` renders as an empty string if the server ever returns `{"errors": {}}` or `{"errors": {"field": []}}` — an empty `role="alert"` box with no text. Low likelihood given ASP.NET `ModelState` only populates failing keys, and confirmed safe from HTML injection (rendered as JSX text, never `dangerouslySetInnerHTML`), but there's no fallback string today.
- **Fix**: Add a fallback, e.g. `Object.values(error.errors).flat().join(" ") || "Please check your details and try again."`
- **Decision**: FIXED — `register.tsx`'s `errorMessage` now falls back to "Please check your details and try again." when the joined validation text is empty.

### F3 — login.tsx/register.tsx use separate submitting/error state instead of home.tsx's discriminated-union Status pattern

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/routes/login.tsx, web/app/routes/register.tsx
- **Detail**: `home.tsx` models view state as a single discriminated union (`Status = idle | loading | ready | error`); the new auth pages instead use two independent `useState` pairs (`submitting`, `error`). Functionally correct and each still follows a consistent internal `errorMessage(switch)` pattern matching `home.tsx`'s style, so this isn't a bug — just a structural inconsistency worth keeping in mind for future auth-page edits.
- **Fix**: Optional — no action required now; if a future change touches these pages again, consider unifying on the `Status`-union style for consistency.
- **Decision**: FIXED — `login.tsx` and `register.tsx` now use a `Status = idle | submitting | error` discriminated union, matching `home.tsx`'s pattern, instead of separate `submitting`/`error` state variables.

### F4 — input.tsx has no semicolons, unlike every other primitive in the same directory

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/app/components/ui/input.tsx
- **Detail**: `input.tsx` is raw, unedited `shadcn` CLI output (no semicolons throughout), while `label.tsx`, `button.tsx`, and `alert.tsx` all use semicolons consistently. Cosmetic only — no lint tooling is configured in this repo (`web/CLAUDE.md`) — but it's a small drift from `AGENTS.md`'s "copied by hand the same way button.tsx and alert.tsx were" convention.
- **Fix**: Add semicolons to `input.tsx` to match the rest of `app/components/ui/`.
- **Decision**: FIXED — semicolons added throughout `input.tsx`.
