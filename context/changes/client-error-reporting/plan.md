# Report swallowed fetch errors in the client API wrapper Implementation Plan

## Overview

Add a single shared error-reporting helper and wire it into the 4 catch blocks in `web/app/api/client.ts` and `web/app/api/auth.ts` that currently discard the real fetch-level error before mapping it to a UI-safe error kind. Closes observability audit findings L2 and G1 (`context/audits/observability/2026-10-11_login-generate.md`).

## Current State Analysis

- `web/app/api/client.ts:47` (`generateMap`) and `web/app/api/auth.ts:40,58,74` (`register`, `login`, `logout`) each wrap their `client.POST`/`client.GET` call in `try { ... } catch { return { ok: false, error: { kind: "network" } }; }`. The catch clause binds no variable, so the real thrown value (a `TypeError`, a `DOMException`, or any other failure) is discarded the instant it is caught.
- No error tracker exists yet in either app — confirmed in the audit: no Sentry/Application Insights package in either `.csproj` or `package.json`, zero `console.*` calls anywhere in `web/app/`.
- No existing unit-test or fetch-mocking convention exists for `client.ts`/`auth.ts`. The only Vitest tests in `web/` are the browser-mode map-rendering tests (`web/app/map/*.test.ts`), run via `web/vitest.config.ts` (`test.include: ["app/**/*.test.ts"]`, across Chromium and Firefox instances). A new `*.test.ts` file under `app/api/` is picked up by that same config automatically — no config change needed.
- `client` (the shared `openapi-fetch` instance) is created in `client.ts` with no custom `fetch` option, so it calls `globalThis.fetch` — stubbable with `vi.stubGlobal("fetch", ...)` in tests.

### Key Discoveries:

- `web/app/api/auth.ts` already imports `client` from `./client.ts` — the natural shared module for a new helper both files use.
- The generic "unexpected HTTP status" branches (the `{kind:"http", status}` case in all 4 functions) are explicitly out of scope — only the bare network-failure `catch` blocks are fixed here (confirmed during planning).

## Desired End State

Every one of the 4 call sites reports the real caught error (via `console.error`, with enough context to know which call failed) before mapping it to the existing UI-safe error kind. User-visible behavior is unchanged — the same `{kind:"network"}` result is still returned in every case. A developer with devtools open (or, once log collection or a tracker exists, in production) can now see exactly which call failed and why, instead of nothing at all.

**Verification**: run the 4 flows (register, login, logout, generate) with the network offline; each should produce one `console.error` line naming the failing call, while the on-screen message stays exactly what it is today.

## What We're NOT Doing

- Not installing Sentry or any tracker SDK — this change only stops discarding the error; wiring a real tracker is a separate, later change per the audit's own fix order (its step 1, vs. this change which is its step 2).
- Not touching the generic `{kind:"http"}` / unexpected-status branches in any of the 4 functions — only the bare `catch` blocks (findings L2, G1).
- Not touching `web/app/root.tsx`'s `ErrorBoundary` (finding L3) — a separate, later change.
- Not touching any API-side (`api/`) findings (L1, G4, P1–P3) — out of scope for this web-only change.
- Not adding account/session identity to the reported context — kept to call name + method/route + the caught value, per the confirmed scope.

## Implementation Approach

Add one exported `reportError(label: string, error: unknown): void` function to `web/app/api/client.ts` that logs via `console.error` with a fixed, greppable prefix. Import it into `auth.ts` (which already imports `client` from the same module). Call it as the first statement in each of the 4 catch blocks, passing a label that names the HTTP method and route (e.g. `"POST /api/auth/login"`), before the existing `return { ok: false, error: { kind: "network" } }`. Purely additive — no change to any function's return type or any caller's behavior.

## Phase 1: Report swallowed client API errors (register, login, logout, generate)

### Overview

Add the shared reporter, wire it into all 4 catch blocks, and add unit tests proving the report fires without changing the existing return value.

### Changes Required:

#### 1. Shared error-reporting helper

**File**: `web/app/api/client.ts`

**Intent**: Give every catch block in this module and `auth.ts` a single place to report a swallowed error before mapping it to a UI-safe kind, so a future change that wires a real tracker only needs to change this one function's body.

**Contract**: Export `function reportError(label: string, error: unknown): void` that calls `console.error(`[api] ${label} failed`, error)`. `label` names the HTTP method and route (e.g. `"POST /api/maps/generate"`). Never throws; no return value.

#### 2. Wire the reporter into `generateMap`

**File**: `web/app/api/client.ts`

**Intent**: Stop discarding the real error in the `generateMap` catch block (finding G1).

**Contract**: In the existing `catch` (line 47), bind the caught value, call `reportError("POST /api/maps/generate", error)`, then keep the existing `return { ok: false, error: { kind: "network" } }` unchanged.

#### 3. Wire the reporter into `register`, `login`, `logout`

**File**: `web/app/api/auth.ts`

**Intent**: Stop discarding the real error in the three auth catch blocks (finding L2).

**Contract**: Import `reportError` from `./client`. In each of the three existing `catch` blocks (lines 40, 58, 74), bind the caught value and call `reportError(...)` with the matching label (`"POST /api/auth/register"`, `"POST /api/auth/login"`, `"POST /api/auth/logout"`) before the existing return.

#### 4. Unit tests

**File**: `web/app/api/client.test.ts` (new), `web/app/api/auth.test.ts` (new)

**Intent**: Pin the fix with an automated regression test, using the project's existing Vitest browser-mode setup (no config change needed — `web/vitest.config.ts` already matches `app/**/*.test.ts`).

**Contract**: For each of the 4 functions, stub `globalThis.fetch` (`vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")))`) and spy on `console.error` (`vi.spyOn(console, "error")`). Assert the spy was called once with a message containing the expected label, and assert the function still resolves to `{ ok: false, error: { kind: "network" } }` (behavior unchanged). Restore both after each test (`vi.unstubAllGlobals()`, `vi.restoreAllMocks()`).

### Success Criteria:

#### Automated Verification:

- New unit tests pass: `npm test` (from `web/`; runs in both Chromium and Firefox per existing config)
- Type checking passes: `npm run typecheck` (from `web/`)
- Existing test suite still green (no regression in map-rendering tests): `npm test`

#### Manual Verification:

- With devtools open, trigger a network failure (Network tab → Offline) on each of login, register, logout and generate, and confirm a `console.error` line appears naming the failing call, while the on-screen message is unchanged ("Could not reach the server. Check your connection and try again.")

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding.

---

## Testing Strategy

### Unit Tests:

- One test per call site (4 total) asserting `console.error` fires with the right label and the return value is unchanged.

### Manual Testing Steps:

1. Run `npm run dev` in `web/` and `dotnet run` in `api/`.
2. Open devtools, go offline (Network tab → Offline), and attempt login — confirm the UI shows "Could not reach the server..." and the console shows a `console.error` line naming `POST /api/auth/login`.
3. Repeat for register, logout and generate.
4. Go back online and confirm normal login/generate still work (no behavior regression).

## References

- Observability audit: `context/audits/observability/2026-10-11_login-generate.md` (findings L2, G1)
- `web/app/api/client.ts:47`, `web/app/api/auth.ts:40,58,74`

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Report swallowed client API errors (register, login, logout, generate)

#### Automated

- [x] 1.1 New unit tests pass: `npm test`
- [x] 1.2 Type checking passes: `npm run typecheck`
- [x] 1.3 Existing test suite still green

#### Manual

- [x] 1.4 Devtools-offline check confirms `console.error` fires for all 4 flows with unchanged UI messages
