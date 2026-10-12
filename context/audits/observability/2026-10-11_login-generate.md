---
type: observability-audit
date: 2026-10-11 06:43
mode: audit
commit: 948345e
branch: test/seed-determinism
dirty_tree: true   # CLAUDE.md modified by the M3L5 skill sync, unrelated to this audit
areas: [login, generate]
area_source: arguments
runtime_proof: not-run
error_tracker: none
previous_report: null
findings: { critical: 5, high: 3, medium: 2, low: 0 }
---

# Observability audit — login, generate (2026-10-11)

## 1. TL;DR

- **There is no error-reporting sink anywhere in the code.** No tracker SDK in either app, zero `ILogger` calls in `api/`, zero `console.*` calls in `web/app/`. Every catch below has nowhere to send an error even where it tries.
- **Every client API call swallows its real exception.** `web/app/api/client.ts:47` and `auth.ts:40,58,74` catch, discard the caught value, and return a generic `{kind:"network"}` — the actual `TypeError`/network failure is gone the instant it's caught.
- **The SPA's only error boundary (`web/app/root.tsx:54`) strips error detail in production on purpose** (`import.meta.env.DEV` gate): any uncaught exception renders a static "Oops!" with the `Error` object thrown away, never reported.
- **The API has no global exception handler**, so an unhandled exception in login or generate falls to ASP.NET Core's bare default 500, whose only trace is a framework log line gated by a platform setting (`az webapp log config`) this repo cannot confirm was ever run.
- **The one place in the whole codebase that deliberately keeps an exception** (`api/Health/DataProtectionHealthCheck.cs:26`) still can't get it to a human: no logger call, and the default health-check writer never serializes it into the response.
- Net effect for both flows: a real production failure in login or generate is, today, **completely invisible** — not even a browser console line — unless a user happens to describe the on-screen message.

## 2. Capture model

**Runtime:** ASP.NET Core 10 API (`api/`) serving a React Router SPA in client-only mode (`web/`), deployed as one Azure App Service process. No background workers, no queues, no serverless functions.

**Error tracker:** none. `api/*.csproj` has no Sentry/Application Insights/OpenTelemetry/Rollbar package reference (checked both `.csproj` files). `web/package-lock.json` has `@opentelemetry/api` only as an unused transitive dependency of something else — no import of it anywhere in `web/app`. (This matches the conversation: Sentry was just chosen but not yet installed.)

**Capture boundaries — API:** `api/Program.cs` wires no `app.UseExceptionHandler(...)`, no exception filter, no middleware around `app.MapMapEndpoints()` / `app.MapAuthEndpoints()`. The only two `catch` blocks in the entire `api/` tree are:
- `api/Maps/MapEndpoints.cs:43` — catches `JsonException` from a malformed request body and turns it into a 400 validation problem. Legitimate: this is a client input error, not a production failure, and nothing here should go to a tracker.
- `api/Health/DataProtectionHealthCheck.cs:26` — catches `Exception`, wraps it in `HealthCheckResult.Unhealthy(message, ex)`. The exception object is *kept* on the `HealthCheckResult`, but nothing downstream logs or reports it (see Finding L1).

Everything else — `AuthEndpoints.Register/Login/Logout/Me`, `MapEndpoints.Generate`'s call into `BspGenerator.Generate` — has zero exception handling. A thrown exception there is caught only by ASP.NET Core's built-in `ExceptionHandlerMiddleware`, which logs via its own internal `ILogger` category and returns an empty 500 with no body. `grep -rn "ILogger\|logger\." api --include="*.cs"` returns **zero** matches — app code never logs anything, ever.

**Capture boundaries — Web:** no `entry.client.tsx` exists (React Router's default entry is used unmodified), so there is no `window.onerror` / `window.onunhandledrejection` registration anywhere. `grep -rn "console\." web/app` returns **zero** matches. The single `ErrorBoundary` (`web/app/root.tsx:54`) is reached by any uncaught `clientLoader` throw (e.g. `getSession()` in `protected.tsx:11`, `login.tsx:17`, `register.tsx`) or render error; in production (`import.meta.env.DEV` false) it discards `error.message`/`error.stack` entirely and shows a fixed "Oops! An unexpected error occurred."

**Logging:** `api/appsettings.json` configures the default console logger (`Default: Information`), which would carry the framework's own exception log line *if* app code ever threw past the framework boundary *and if* App Service's log stream has actually been turned on. Per `context/foundation/infrastructure.md`'s Operational Story, `az webapp log config --docker-container-logging filesystem` is a one-time manual step — there's no evidence in `deploy.yml` or elsewhere in the repo that it ran. **Assumption, not a finding** (see below).

**Scrubbing / sampling / deploy identity:** not applicable — there is no tracker to configure.

**Assumptions (outside the repo, unconfirmed):**
- Whether `az webapp log config` was ever run, so framework-level 500 logs actually reach somewhere readable. *Unconfirmed.*
- Whether Azure App Service has any platform-level alerting on HTTP 5xx rate. *Unconfirmed, likely not set up given no monitoring tool was chosen until this conversation.*

## 3. What reaches the tracker

No tracker exists, so this table is necessarily static-only (no runtime proof was run this pass — see §8).

| Failure shape | Response | Platform logs | Tracker | Verdict |
|---|---|---|---|---|
| Login: DB/network exception inside `AuthEndpoints.Login` | Bare 500, no body | Framework log line *if* log streaming is enabled (unconfirmed) | None — no tracker exists | Invisible to undiagnosable |
| Login: client-side `fetch` failure (CORS, DNS, offline) | UI shows "Could not reach the server" | Nothing — no `console.*` call | None | Invisible |
| Login: `getSession()` throws on an unexpected status | UI shows generic "Oops!" (prod) | Nothing | None | Invisible |
| Generate: `BspGenerator.Generate` throws | Bare 500, no body | Same as above (unconfirmed) | None | Invisible to undiagnosable |
| Generate: client-side `fetch` failure | UI shows "Could not reach the server" | Nothing | None | Invisible |
| Generate: atlas/render failure in browser | UI shows "Could not draw the map: `<message>`" | Nothing | None | Message visible to the user only, not to anyone who could fix it |
| Data Protection round-trip failure (health check) | Readiness endpoint returns generic "Unhealthy" behind a key header | Nothing | None | Invisible — exception object created, then discarded |

## 4. Systemic root causes

1. **No error-reporting sink exists in code at all.** `api/*.csproj` — no tracker package. `grep ILogger` in `api/` — 0 hits. `grep console.` in `web/app/` — 0 hits. Every finding below ends in "...and then nothing," because there is nowhere for "something" to go. This is a reasonable state for a project that hadn't yet picked a monitoring vendor — but it means picking Sentry only fixes things once code is changed to call it.
2. **Every client API wrapper swallows its catch parameter.** `web/app/api/auth.ts:40,58,74` and `web/app/api/client.ts:47` all read `} catch { return { ok: false, error: { kind: "network" } }; }` — the caught value is never bound to a name, so it's structurally impossible to log even if a call existed. Explains L2, G1.
3. **The SPA's only error boundary deliberately drops detail outside dev mode.** `web/app/root.tsx:60-62`: `else if (import.meta.env.DEV && error instanceof Error) { details = error.message; stack = error.stack; }` — in production this branch never runs, so `details` stays `"An unexpected error occurred."` and the real error is never captured before being discarded. Explains L3.
4. **The API has no global exception-handling middleware.** Nothing in `api/Program.cs` sits between `app.MapMapEndpoints()`/`app.MapAuthEndpoints()` and the framework defaults. Any exception not explicitly caught (which, per root cause 1, means almost all of them) gets ASP.NET Core's bare default behavior: no app context, no correlation id, visibility gated entirely on an unconfirmed platform setting. Explains G4 and the login/generate 500 paths generally.
5. **The one deliberate "keep the exception" line in the codebase still doesn't complete the trip.** `api/Health/DataProtectionHealthCheck.cs:26` passes `ex` into `HealthCheckResult.Unhealthy(message, ex)` — someone clearly intended this to be diagnosable — but nothing logs it, and ASP.NET Core's default health-check response writer never serializes `Exception` into the HTTP body. Explains L1, and matters because it shows the gap isn't inattention, it's an incomplete pattern that's easy to fix once noticed.

## 5. Findings by area

### Login

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| L1 | `api/Health/DataProtectionHealthCheck.cs:26` | swallowed | critical | Data Protection key-ring failure (the exact risk the infra risk register calls out: users logged out constantly) produces an `Exception` that is wrapped and then dropped; the readiness endpoint shows a bare "Unhealthy" behind a key header nobody polls automatically. No one learns why logins started failing. | Log `ex` (with the health check name) before/when returning `Unhealthy`; once a tracker exists, report it there too. |
| L2 | `web/app/api/auth.ts:40,58,74` | swallowed | critical | Any `fetch`-level failure during register/login/logout (CORS misconfig, DNS failure, a real bug in `openapi-fetch`) collapses to `{kind:"network"}`; the actual thrown value is discarded in the `catch` clause itself — it's never even bound to a variable. User sees "Could not reach the server," which is the last anyone will know. | Bind the caught error, log/report it with the call's name and method, *then* map to the UI-safe error kind. |
| L3 | `web/app/api/auth.ts:33` (`getSession` throw) → `web/app/root.tsx:54` (`ErrorBoundary`) | identity-lost / coverage-gap | critical | If `/api/auth/me` returns an unexpected status during the login-flow session check (e.g. a 500 while the DB resumes, per the known Azure SQL auto-pause risk), `getSession()` throws `Error("GET /api/auth/me returned 500")`. In production that reaches the root `ErrorBoundary`, which discards the message/stack and renders "Oops! An unexpected error occurred." with nothing reported anywhere. | Report the error inside the `ErrorBoundary` (not only in the `DEV` branch) before rendering the fallback. |

### Generate

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| G1 | `web/app/api/client.ts:47` | swallowed | critical | Same pattern as L2, for `generateMap`'s `client.POST("/api/maps/generate", ...)` call. A real client-side failure during generation is reduced to "Could not reach the server" with the original error discarded. | Same fix as L2: log/report before mapping to `GenerateError`. |
| G2 | `web/app/routes/home.tsx:111-117` (atlas/render `.catch`) | identity-lost | high | A tileset-loading or `renderMap` failure is caught and shown to the user as `"Could not draw the map: <message>"` — better than most findings here (it keeps the message) but it is UI-only. A rendering bug the developer can't personally reproduce stays invisible unless a user transcribes the exact string on screen. | Report the caught error (with the map's seed/params for repro) in addition to showing it. |
| G3 | `web/app/routes/home.tsx:144-148` (`onDownload` catch) | identity-lost | medium | Same shape as G2 for PNG export failures (e.g. the known Firefox canvas-export risk). Shown to the user only. Lower severity: this is a documented, known browser-capability edge case rather than a backend bug. | Report alongside the existing UI message. |
| G4 | `api/Maps/MapEndpoints.cs:52` (`BspGenerator.Generate` call, uncaught) | coverage-gap | medium | A bug in the BSP generator — the most actively developed, least battle-tested code per the roadmap — throws straight into the framework default 500, with no seed/params attached anywhere since nothing logs. Medium rather than critical because fixed-seed fixtures already pin correctness for known inputs. | Catch around the generator call, log with seed+params, re-throw or map to a 500 with a tracker event. |

### Platform / plumbing

| # | Location | Category | Severity | What happens in production | Fix direction |
|---|---|---|---|---|---|
| P1 | Repo-wide: no tracker package in `api/*.csproj` or `web/package.json` | coverage-gap | critical | There is nothing to report *to*. Every other finding's fix depends on this closing first. | Add the Sentry SDK (just decided) to both apps: one init site each. |
| P2 | Repo-wide: `grep -rn "ILogger\|logger\." api --include="*.cs"` → 0 hits | coverage-gap | high | No API code path ever writes an application-level log line with route/account/request context. The only signal for any API failure is the framework's own generic exception log (if enabled at the platform level at all). | Inject `ILogger` at the endpoints that currently have no error handling (Auth, Maps) as part of fixing L1/L3/G4. |
| P3 | Repo-wide: `grep -rn "console\." web/app` → 0 hits; no `window.onerror`/`unhandledrejection` listener | coverage-gap | high | Any browser-side error outside React's render path (an event handler not wrapped in `try/catch`, a timer callback) is silent even in devtools, let alone in production. | Add a global `window.onerror`/`onunhandledrejection` handler once the tracker SDK is in, as a safety net behind the local fixes above. |

## 6. Recommended fix order

Ordered by blindness removed per unit of effort:

1. **Install the Sentry SDK in both apps** (one init site each: `api/Program.cs`, a new `web/app` entry point). Closes nothing by itself but is the prerequisite for every fix below — right now there is nowhere to send anything. (Closes the precondition behind P1.)
2. **Fix the client API wrapper pattern once.** `web/app/api/client.ts` and `auth.ts` share one shape (`try { ... } catch { return {kind:"network"} }`) across 4 call sites. One small shared helper that captures the error before mapping it closes L2 and G1 together.
3. **Report from the root `ErrorBoundary`** (`web/app/root.tsx:54`) unconditionally, not only in the `DEV` branch. One function, closes L3, and becomes the safety net for P3 until a global handler exists.
4. **Add exception logging/reporting in `api/Program.cs`** (a minimal `app.UseExceptionHandler` that logs before responding) and **log the exception already captured** in `DataProtectionHealthCheck.cs:26`. Closes L1, most of P2, and the API side of the login/generate 500 paths.
5. **Lowest priority, same shape:** wrap `BspGenerator.Generate` with seed/params context (G4), and report (not just display) the render/download catches in `home.tsx` (G2, G3).

Constraint to carry into implementation: none of these need PII scrubbing decisions — the data involved (seeds, room counts, HTTP status codes, route names) has no user-identifying content beyond the account email already visible in the session cookie claims, so this is a pure "add the call" exercise, not a privacy trade-off.

## 7. Changes since last audit

First run — not applicable.

## 8. Method and limits

This was a direct single-pass review, not a multi-subagent fan-out: the codebase is small (one `Program.cs`, two endpoint files, four route files), and every file relevant to login and generate was read directly rather than sampled. Repo-wide sweeps run and their exact results:

- `grep -rn "catch" api --include="*.cs"` → 3 hits total, 2 relevant (the third is a comment).
- `grep -rn "ILogger\|logger\." api --include="*.cs"` → 0 hits.
- `grep -rn "console\." web/app --include="*.ts" --include="*.tsx"` → 0 hits.
- `grep -rn "catch" web/app --include="*.ts" --include="*.tsx"` → 8 hits, all reviewed (§5).
- `grep -rIn "Sentry|sentry|Rollbar|Bugsnag|Honeybadger|opentelemetry|OpenTelemetry|ApplicationInsights"` repo-wide → only an unused transitive `@opentelemetry/api` dependency in `web/package-lock.json`.

No runtime proof was run this pass (`--runtime` not requested); every verdict in §3 is static analysis of the code paths, not an observed probe result. If you want the gaps proven rather than argued, say so and I'll set up an isolated worktree with the fake-ingest harness (`references/runtime-probes.md`) and inject the failure catalog against local login/generate calls.

No foundation docs (`shape-notes.md`/`prd.md`/`roadmap.md`) were needed for area selection since the areas were given explicitly (login, generate).
