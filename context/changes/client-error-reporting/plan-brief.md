# Report swallowed fetch errors in the client API wrapper — Plan Brief

> Full plan: `context/changes/client-error-reporting/plan.md`

## What & Why

The client-side `register`, `login`, `logout` and `generateMap` calls (`web/app/api/auth.ts`, `web/app/api/client.ts`) all catch fetch failures and discard the real error, returning a generic `{kind:"network"}` with nothing logged anywhere. The 2026-10-11 observability audit (`context/audits/observability/2026-10-11_login-generate.md`, findings L2 and G1) flagged this as critical: a real production failure in login or generate is currently invisible to anyone, not even in the browser console.

## Starting Point

4 call sites share one shape: `try { ... } catch { return { ok: false, error: { kind: "network" } }; } }`, with the caught value never even bound to a variable. No tracker SDK exists in either app yet, and there's no existing unit-test or fetch-mocking convention for these two files.

## Desired End State

Every one of the 4 call sites logs the real error (with a label naming the failing call) via `console.error` before mapping it to the existing UI-safe error kind. User-visible behavior is unchanged. A developer with devtools open can now see exactly which call failed and why.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Reporting destination (no tracker yet) | `console.error` via one shared helper | Ships today with zero new dependencies; swapping the function body for Sentry later is a one-file change | Plan |
| Fix scope | Bare `catch` blocks only (literal L2/G1), not the generic `{kind:"http"}` unexpected-status branches | Keeps the change exactly as scoped in `change.md`; the status branches are a separate, similar gap for a future change | Plan |
| Context captured | Call name (method + route) + the raw caught value, no account identity | Minimum needed to triage "which call, what broke" with nothing sensitive to handle | Plan |
| Testing approach | New Vitest unit tests (stub `fetch`, spy on `console.error`) + one manual devtools-offline check | Pins the fix with an automated regression test using the project's existing Vitest setup; no config change needed | Plan |

## Scope

**In scope:**
- `web/app/api/client.ts`: new `reportError` helper + wiring it into `generateMap`'s catch block
- `web/app/api/auth.ts`: wiring `reportError` into `register`, `login`, `logout`'s catch blocks
- New unit tests for all 4 call sites

**Out of scope:**
- Installing Sentry or any tracker SDK (a later change)
- The generic `{kind:"http"}` unexpected-status branches
- `web/app/root.tsx`'s `ErrorBoundary` (finding L3)
- Any API-side (`api/`) findings (L1, G4, P1–P3)

## Architecture / Approach

One new exported function, `reportError(label, error)`, added to `client.ts` (the module `auth.ts` already imports from) and called at the top of each of the 4 existing catch blocks. Purely additive — no return-type or caller changes.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Report swallowed client API errors | Shared `reportError` helper wired into all 4 catch blocks, plus unit tests | Low — small, additive, no behavior change |

**Prerequisites:** none — can start immediately.
**Estimated effort:** well under one session; one phase, 4 call sites, 2 new test files.

## Open Risks & Assumptions

- `console.error` output is only as useful as whatever picks it up later (devtools today; a tracker or log collection once one exists) — this change doesn't itself add production visibility, only stops throwing the evidence away.
- New tests run under the existing Vitest browser-mode config (Chromium + Firefox); `vi.stubGlobal("fetch", ...)` is assumed to work in that environment the same way it does in Node — confirm on first run.

## Success Criteria (Summary)

- All 4 flows (register, login, logout, generate) log a `console.error` line naming the failing call when the network is offline, with the on-screen message unchanged.
- New unit tests and the existing suite pass; typecheck is clean.
