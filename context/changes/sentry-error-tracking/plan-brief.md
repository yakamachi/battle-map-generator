# Install Sentry error tracking in api and web — Plan Brief

> Full plan: `context/changes/sentry-error-tracking/plan.md`

## What & Why

Install the Sentry SDK in both `api/` and `web/` so production errors actually reach a tracker, then wire the specific gaps the 2026-10-11 observability audit named (L1, L3, G4) to report to it. This is the precondition `client-error-reporting` deferred — `reportError()` was explicitly designed so "wiring a real tracker later only means changing this function's body."

## Starting Point

No tracker SDK exists anywhere in the codebase today (confirmed by the audit). `api/Program.cs` uses the minimal hosting model; `web/` has zero existing build-time env-var wiring. Neither app's CI/deploy pipeline pushes app settings into Azure — those are set by hand in the portal today (`Health:ReadyKey`, the SQL connection string).

## Desired End State

Both apps report real errors to Sentry: the API via `Sentry.AspNetCore`'s automatic unhandled-exception + `LogError` capture, the web app via `@sentry/react`'s global handlers plus explicit calls from `reportError()` and the root `ErrorBoundary`. Two specific previously-silent failures (a Data Protection round-trip failure, a map-generation crash with its seed/params) now reach Sentry with useful context.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
|---|---|---|---|
| Web DSN classification | GitHub Actions repo variable (`vars.VITE_SENTRY_DSN`), not a secret | A Sentry DSN ships inside public client bundles by design — it isn't actually sensitive, so it follows the existing `vars.*` convention rather than misclassifying it as a secret | Plan |
| Wiring scope | Install SDKs **and** wire the audit's L1/L3/G4 findings in this same change | Closes most of the audit's remaining gaps in one pass rather than three separate changes | Plan |
| Performance tracing | Off (`TracesSampleRate = 0`, no tracing integration) | Matches the audit's actual ask (error visibility) and avoids burning Sentry's free-tier transaction quota on unknown traffic | Plan |
| PII capture | Off (`SendDefaultPii = false`, no `Sentry.setUser()`) | Consistent with `reportError()`'s existing no-PII design — no account email or IP flows into Sentry | Plan |

## Scope

**In scope:**
- `Sentry.AspNetCore` install + init in `api/Program.cs` (config-driven DSN)
- `@sentry/react` install + init in `web/app/root.tsx` (build-time env var DSN)
- `deploy.yml` wiring for the web DSN; manual documentation for the API's App Service setting and the GitHub repo variable
- Logging at the two audit-named API sites (`DataProtectionHealthCheck` = L1, `MapEndpoints.Generate` = G4)
- Wiring `reportError()` and the root `ErrorBoundary` to `Sentry.captureException` (L2/G1's tracker leg, and L3)

**Out of scope:**
- A global `app.UseExceptionHandler` middleware (Sentry's automatic capture already covers it)
- Broader `ILogger` rollout across every API endpoint (rest of P2)
- `home.tsx`'s render/download catches (G2, G3)
- Performance tracing, PII capture

## Architecture / Approach

Four phases: API install → API wiring → web install → web wiring. Each install phase is a safe no-op without a DSN configured (confirmed via Context7 docs), so there's no window where tests or builds depend on a live Sentry project. A notable discovery: Sentry.AspNetCore's "capture LogError" and "capture unhandled exceptions" are both automatic SDK features — most of this plan's API-side work is adding missing *context* to captures that happen automatically, not building capture from scratch.

## Phases at a Glance

| Phase | What it delivers | Key risk |
|---|---|---|
| 1. Install api/ | Sentry.AspNetCore wired, config-driven DSN | Low — no-op without a DSN |
| 2. Wire API findings | L1 and G4 now logged with context | Low — additive logging only |
| 3. Install web/ | @sentry/react wired, new CI/deploy env var | Medium — first env-var wiring in this app |
| 4. Wire web findings | reportError() and ErrorBoundary report to Sentry | Low — verify existing tests stay green with an uninitialized SDK in test context |

**Prerequisites:** none — can start immediately.
**Estimated effort:** a few sessions across 4 phases; most of the work is small, additive edits plus manual verification against a real Sentry project.

## Open Risks & Assumptions

- `Sentry.captureException` called against an uninitialized SDK (the case in `client.test.ts`/`auth.test.ts`'s isolated test context) is assumed to no-op safely rather than throw or spam warnings — confirm on first run; mock `@sentry/react` in those tests if it doesn't.
- The GitHub repo variable and the Azure App Service setting are both manual, human-authorized steps — this plan documents them but doesn't automate them.
- Full production confirmation of the web DSN wiring (Phase 3) can only happen after a real merge-to-main deploy.

## Success Criteria (Summary)

- A Sentry test exception, triggered locally on each app, reaches the Sentry dashboard.
- The two previously-silent API failures (L1, G4) and the client-side findings (reportError's 4 call sites, the root ErrorBoundary) all report to Sentry with useful context, verified manually against the real project.
- All existing automated test suites (`dotnet test api.Tests`, `npm test`, `npm run typecheck`) stay green throughout.
