# Install Sentry error tracking in api and web Implementation Plan

## Overview

Install the Sentry SDK in both `api/` (Sentry.AspNetCore) and `web/` (@sentry/react), then wire the specific gaps the 2026-10-11 observability audit named (L1, L3, G4) to actually report to it. Closes audit finding P1 (no tracker SDK anywhere) and makes the `reportError()` helper added in `client-error-reporting` — which was designed so "wiring a real tracker later only means changing this function's body" — actually report somewhere real.

## Current State Analysis

- `api/Program.cs` uses the **minimal hosting model** (`WebApplication.CreateBuilder`), not `Host.CreateDefaultBuilder().ConfigureWebHostDefaults()`. Sentry's own current docs (verified via Context7, `/getsentry/sentry-docs`) confirm the matching call for this pattern is `builder.WebHost.UseSentry(...)` — a one-line adaptation from the `webBuilder.UseSentry(...)` style shown in generic onboarding snippets.
- No app setting is ever pushed into Azure App Service by CI today — `deploy.yml` has no `az webapp config appsettings set` step. `Health:ReadyKey` and `ConnectionStrings:AppDb` are both set by hand in the Azure portal (confirmed in `context/foundation/infrastructure.md:156` and `deploy.yml`). A Sentry DSN app setting (`Sentry__Dsn`) will need the same manual one-time step — this plan documents it, it isn't a code change.
- `api.Tests/Infrastructure/ApiFactory.cs` runs the test host under `ASPNETCORE_ENVIRONMENT=Testing`, which never loads `appsettings.Development.json` at all (not just overridden — excluded by environment). No DSN key will exist in any test run, so Sentry stays inert there automatically, with no extra guard code needed.
- `api/BattleMapGenerator.Api.csproj`'s `OpenApiGenerateDocumentsOnBuild` runs `Program.cs`'s startup against a mock server on every `dotnet build`, which "must not need configuration or the database." `UseSentry` with an empty/missing DSN is a safe no-op (confirmed via Context7 docs and Sentry.AspNetCore's own behavior), so this is satisfied without extra code.
- `web/` has **no existing build-time env-var wiring at all** — no `.env*` files, no `VITE_*` usage, no `import.meta.env` beyond the framework's own `import.meta.env.DEV`. Both `ci.yml` and `deploy.yml` run plain `npm run build` with no `env:` block on that step today. Adding `VITE_SENTRY_DSN` is new wiring, not a tweak.
- A Sentry DSN is not actually a secret — it ships inside every public page/app that uses it. The repo's existing convention splits non-secret config into GitHub Actions `vars.*` (`AZURE_WEBAPP_NAME` etc.) vs. true secrets into `secrets.*` (the one precedent, `HEALTH_READY_KEY`, used only inside the workflow run, never written into Azure config). The web DSN follows the `vars.*` convention (confirmed decision, 2026-10-11).

### Key Discoveries:

- **Sentry.AspNetCore already auto-captures any truly unhandled exception** once `UseSentry` is wired in (confirmed via Context7: "Capture unhandled exceptions and exceptions with custom exception handlers" is a built-in feature of the ASP.NET Core integration). This means finding **G4** (the uncaught `BspGenerator.Generate` exception) is already captured, bare, the moment Phase 1 lands — Phase 2's job for G4 is adding the *missing business context* (seed, room count, encounter) on top of that automatic capture, not creating capture where none existed.
- Finding **L1** is different: `DataProtectionHealthCheck.cs`'s exception is already caught and converted into a `HealthCheckResult`, so it never becomes "unhandled" and Sentry's automatic capture never sees it. L1 genuinely needs an explicit log call.
- Sentry.AspNetCore's `UseSentry(Action<options>)` overload still reads the `"Sentry"` config section from `IConfiguration` first, then applies the callback on top (confirmed via Context7: "Settings from `appsettings.json` will be applied before the callback is invoked") — so `Sentry:Dsn` can come from App Service's `Sentry__Dsn` application setting exactly like `Health:ReadyKey` does today, with no custom config-reading code.
- **"Capture `LogError` or `LogCritical`"** is also a built-in Sentry.AspNetCore feature — any `ILogger.LogError(ex, ...)` call automatically becomes a Sentry event (not just a log line). L1 and G4 both just need a normal `ILogger.LogError` call; no `SentrySdk.CaptureException` calls are needed on the API side.
- `@sentry/react`'s `Sentry.init()` automatically installs global `window.onerror`/`onunhandledrejection` handlers — this covers audit finding **P3** (no global client-side handler) as a side effect of Phase 3, with no extra code.
- `web/app/root.tsx` has no custom `entry.client.tsx` to hook into (confirmed in the earlier audit) — the earliest available client-side hook is the top of `root.tsx` itself.

## What We're NOT Doing

- Not adding a global `app.UseExceptionHandler` middleware — Sentry.AspNetCore's own automatic unhandled-exception capture already covers what that would add; this change only adds explicit logging at the two specific sites the audit named (L1, G4) where an exception is already caught and would otherwise never reach even that automatic capture.
- Not rolling out `ILogger` across every API endpoint (the rest of audit finding P2) — only the two specific sites named by the audit (L1, G4).
- Not wiring `web/app/map/home.tsx`'s render/download catches (G2, G3) — those already show their error to the user; reporting them to Sentry is a smaller, separate follow-up.
- Not adding performance tracing (spans/transactions) — `TracesSampleRate` stays `0` on the API side and no tracing integration is added on the web side (confirmed decision, 2026-10-11): this is error tracking only, to match the audit's actual ask and avoid burning Sentry's free-tier transaction quota on an unknown-traffic hobby project.
- Not enabling `SendDefaultPii` (API) or calling `Sentry.setUser()` (web) — no account email, IP, or session identity flows into Sentry events (confirmed decision, 2026-10-11), consistent with `reportError()`'s existing no-PII design.
- Not adding the Sentry DSN to `ci.yml`'s build steps — CI never needs real capture, and `Sentry.init`/`UseSentry` with no DSN is a safe no-op, so leaving it unset there is simplest.
- Not adding a default DSN to `api/appsettings.Development.json` — local dev opts in only if a developer wants it (see Manual Testing Steps), so the shared repo config stays DSN-free.

## Implementation Approach

Four phases, grouped by app and by "install" vs. "wire the audit's specific findings": API install → API wiring (L1, G4) → web install → web wiring (reportError, ErrorBoundary). Each phase is independently testable; the install phases are safe no-ops without a DSN configured, so there's no window where tests or builds depend on a live Sentry project.

## Phase 1: Install and initialize Sentry in api/

### Overview

Add the Sentry.AspNetCore SDK, initialize it in `Program.cs` reading its DSN from configuration, and document the manual Azure App Service setting production needs.

### Changes Required:

#### 1. Add the Sentry.AspNetCore package

**File**: `api/BattleMapGenerator.Api.csproj`

**Intent**: Bring in the SDK that will capture unhandled exceptions and `LogError`/`LogCritical` calls.

**Contract**: Add `<PackageReference Include="Sentry.AspNetCore" Version="6.12.0" />` to the existing `<ItemGroup>` of package references.

#### 2. Initialize Sentry in Program.cs

**File**: `api/Program.cs`

**Intent**: Wire the SDK into the minimal-hosting-model builder, reading the DSN from configuration (so it comes from an App Service application setting in production and is simply absent — a safe no-op — everywhere else: local dev by default, `Testing`, and the `OpenApiGenerateDocumentsOnBuild` mock-server startup).

**Contract**: Immediately after `var builder = WebApplication.CreateBuilder(args);` (line 15), add:
```csharp
// Dsn comes from configuration ("Sentry:Dsn" — an App Service application setting in
// production, nothing here in Testing or at build-time OpenAPI generation; UseSentry
// no-ops safely with no Dsn configured). Error tracking only: no tracing, no PII (decided
// 2026-10-11) — see context/audits/observability/2026-10-11_login-generate.md.
builder.WebHost.UseSentry(o =>
{
    o.TracesSampleRate = 0;
    o.SendDefaultPii = false;
});
```

#### 3. Document the manual production setup step

**File**: N/A (operational step, not a code change — record it in this plan's Manual Testing Steps and in a short note for the team)

**Intent**: Production needs `Sentry__Dsn` set as an Azure App Service Application Setting, the same way `Health:ReadyKey` and the SQL connection string are set today. This is a one-time manual portal step, not something CI does.

**Contract**: N/A — tracked as a Manual Verification item below.

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds from `api/` (package restores; confirms `UseSentry` with no Dsn configured tolerates the OpenAPI-generation mock-server startup)
- `dotnet test api.Tests` passes with no regressions (confirms the `Testing` environment stays Sentry-free with no extra guard code)

#### Manual Verification:

- Locally, set a real DSN via `dotnet user-secrets set Sentry:Dsn "<dsn>"` (never commit a real DSN to `appsettings.Development.json`), run the API, trigger any unhandled exception, and confirm the event arrives in the Sentry dashboard
- Set `Sentry__Dsn` as an Application Setting on the production App Service (one-time manual portal step)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 2: Wire API-side audit findings to Sentry (L1, G4)

### Overview

Add the missing `ILogger` calls at the two specific API-side locations the audit named — `DataProtectionHealthCheck` (L1, an already-caught exception that never reaches Sentry's automatic capture) and `MapEndpoints.Generate`'s call into `BspGenerator.Generate` (G4, where Sentry's automatic unhandled-exception capture already catches the bare exception, but with none of the seed/params context).

### Changes Required:

#### 1. Log the Data Protection round-trip failure

**File**: `api/Health/DataProtectionHealthCheck.cs`

**Intent**: The exception is already captured on the `HealthCheckResult` (line 26) but nothing downstream ever reads or reports it. Log it so it reaches Sentry via the SDK's automatic `LogError` capture.

**Contract**: Add an `ILogger<DataProtectionHealthCheck> logger` primary-constructor parameter (DI resolves it automatically via `AddCheck<DataProtectionHealthCheck>` — no registration change needed). In the existing `catch (Exception ex)` block, call `logger.LogError(ex, "Data Protection round-trip failed");` before returning `HealthCheckResult.Unhealthy(...)`.

#### 2. Log and attach context to a map-generation failure

**File**: `api/Maps/MapEndpoints.cs`

**Intent**: A `BspGenerator.Generate` exception is already caught by Sentry's own automatic unhandled-exception capture (see Key Discoveries) — but with no seed/room-count/encounter context, it's unattributable. Add that context without changing the existing bare-500 response behavior.

**Contract**: Wrap the existing `return TypedResults.Ok(BspGenerator.Generate(...))` call (currently line 52) in a `try`/`catch (Exception ex)`. In the catch, call `logger.LogError(ex, "Map generation failed for seed {Seed}, roomCount {RoomCount}, encounter {Encounter}", request.Seed, request.RoomCount, request.Encounter)` (structured parameters, so Sentry gets them as searchable attributes — confirmed via Context7 that `ILogger`'s structured parameters are attached this way), then `throw;` to preserve the existing response. Add an `ILogger<MapEndpoints> logger` parameter to the `Generate` method signature (minimal-API endpoints resolve `ILogger<T>` as a service parameter automatically, matching the existing `IOptions<HttpJsonOptions> json` parameter's pattern).

### Success Criteria:

#### Automated Verification:

- `dotnet build` succeeds from `api/`
- `dotnet test api.Tests` passes with no regressions

#### Manual Verification:

- With a local DSN set (per Phase 1), force the Data Protection round-trip to fail (or temporarily throw inside the try block) and confirm a logged event with the right message reaches Sentry
- Temporarily force `BspGenerator.Generate` to throw and confirm the event in Sentry carries the seed/roomCount/encounter attributes, then remove the temporary throw

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 3: Install and initialize Sentry in web/

### Overview

Add `@sentry/react`, initialize it at the top of `root.tsx` reading the DSN from a new build-time env var, and wire that env var into the production build step.

### Changes Required:

#### 1. Add the @sentry/react package

**File**: `web/package.json`

**Intent**: Bring in the browser SDK.

**Contract**: Add `@sentry/react` to `dependencies`.

#### 2. Initialize Sentry at the earliest available client hook

**File**: `web/app/root.tsx`

**Intent**: Initialize as early as possible given there's no custom `entry.client.tsx` to hook into (confirmed in Key Discoveries) — the top of `root.tsx`, before any component code, is the earliest available point. No tracing integrations, no `Sentry.setUser()` call anywhere (matches the no-PII, no-tracing decisions).

**Contract**: Near the top of the file, after the existing imports, add:
```typescript
import * as Sentry from "@sentry/react";

// VITE_SENTRY_DSN comes from a build-time env var (ci.yml leaves it unset — a safe no-op;
// deploy.yml's "Build web" step sets it from the vars.VITE_SENTRY_DSN repo variable).
// Error tracking only: no tracing integration added, no Sentry.setUser() call anywhere
// (decided 2026-10-11) — see context/audits/observability/2026-10-11_login-generate.md.
Sentry.init({ dsn: import.meta.env.VITE_SENTRY_DSN });
```

#### 3. Wire the DSN into the production build

**File**: `.github/workflows/deploy.yml`

**Intent**: The SPA is built once and its static output is baked into the deployed artifact — Vite inlines `import.meta.env.VITE_*` at build time, so the env var must be present on the "Build web" step specifically (not reachable via an Azure app setting the way the API's DSN is).

**Contract**: Add `env: { VITE_SENTRY_DSN: ${{ vars.VITE_SENTRY_DSN }} }` to the existing "Build web" step.

#### 4. Create the GitHub Actions repository variable

**File**: N/A (operational step, not a code change)

**Intent**: `vars.VITE_SENTRY_DSN` needs to actually exist as a repo variable for step 3 to have any effect.

**Contract**: N/A — tracked as a Manual Verification item below; run with the user's explicit go-ahead since it's a repo-settings change, e.g. `gh variable set VITE_SENTRY_DSN --body "<dsn>"`.

### Success Criteria:

#### Automated Verification:

- `npm run typecheck` passes from `web/`
- `npm test` passes with no regressions (confirms `Sentry.init` with no DSN set, as in the test environment, is a safe no-op)
- `npm run build` succeeds locally with `VITE_SENTRY_DSN` unset

#### Manual Verification:

- Locally, create a gitignored `.env.local` (or export the var directly) with a real DSN, run `npm run dev`, trigger a thrown error in the browser, and confirm the event arrives in the Sentry dashboard
- Create the `VITE_SENTRY_DSN` repository variable (with the user's go-ahead)
- After the next merge to `main`, confirm the deploy workflow's build step picked up the variable (no error, and a subsequent production error — or a one-off manual test — reaches Sentry)

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding to the next phase.

---

## Phase 4: Wire web-side audit findings to Sentry (reportError, ErrorBoundary)

### Overview

Update `reportError()` (the choke point added in `client-error-reporting`, explicitly designed for this) to also call `Sentry.captureException`, and add the same call to `root.tsx`'s `ErrorBoundary` for real thrown errors (finding L3).

### Changes Required:

#### 1. Report through Sentry from reportError

**File**: `web/app/api/client.ts`

**Intent**: `reportError()` was designed so "wiring a real tracker later only means changing this function's body." Do exactly that, keeping the existing `console.error` call.

**Contract**: Import `* as Sentry from "@sentry/react"`. In `reportError`, after the existing `console.error` call, add `Sentry.captureException(error, { tags: { call: label } });` (tags, not extra — small-cardinality, searchable/filterable in Sentry). `reportError`'s signature and all 4 call sites in `client.ts`/`auth.ts` stay unchanged.

#### 2. Report real thrown errors from the root ErrorBoundary

**File**: `web/app/root.tsx`

**Intent**: Close finding L3 — today, outside `import.meta.env.DEV`, a real thrown `Error` reaching this boundary is discarded with nothing reported. Report it regardless of DEV/PROD; don't report `isRouteErrorResponse` cases (404s etc.) — those are expected, not failures.

**Contract**: In the `error instanceof Error` branch (currently gated entirely by `import.meta.env.DEV`), add `Sentry.captureException(error);` unconditionally, before the existing `if (import.meta.env.DEV) { details = error.message; stack = error.stack; }` logic. The displayed message in production is unchanged (still generic) — only the reporting is added.

### Success Criteria:

#### Automated Verification:

- `npm run typecheck` passes
- `npm test` passes, including the existing `client.test.ts`/`auth.test.ts` from `client-error-reporting` (verify `Sentry.captureException` running against an uninitialized SDK in that isolated test context doesn't fail or warn-fail the test; if it does, mock `@sentry/react` in those test files)

#### Manual Verification:

- With a local DSN set (per Phase 3), trigger a network failure on login/register/logout/generate and confirm each reaches Sentry tagged with its call label
- Temporarily force a real thrown error into a route loader, confirm it reaches the `ErrorBoundary` and is reported to Sentry, then remove the temporary throw

**Implementation Note**: After completing this phase and all automated verification passes, pause here for manual confirmation from the human that the manual testing was successful before proceeding.

---

## Testing Strategy

### Unit Tests:

- Phase 4 must keep `client-error-reporting`'s existing `client.test.ts`/`auth.test.ts` green with the added `Sentry.captureException` call in `reportError`.

### Manual Testing Steps:

1. Phase 1: `dotnet user-secrets set Sentry:Dsn "<dsn>"` locally, trigger an exception, confirm it in Sentry.
2. Phase 1: set `Sentry__Dsn` as a production App Service Application Setting.
3. Phase 2: force each of the two named failures, confirm the logged context reaches Sentry.
4. Phase 3: local `.env.local` with a real DSN, trigger a browser error, confirm it in Sentry; create the `VITE_SENTRY_DSN` repo variable.
5. Phase 4: trigger each of the 4 `reportError` call sites and a real `ErrorBoundary` throw, confirm all reach Sentry.

## References

- Observability audit: `context/audits/observability/2026-10-11_login-generate.md` (findings P1, P3, L1, L3, G4)
- Prior change: `context/changes/client-error-reporting/plan.md` (`reportError()`'s design, explicitly anticipating this change)
- Sentry docs (verified via Context7, `/getsentry/sentry-docs`): minimal-hosting-model `UseSentry`, config-binding-before-callback behavior, automatic `LogError`/unhandled-exception capture

## Progress

> Convention: `- [ ]` pending, `- [x]` done. Append ` — <commit sha>` when a step lands. Do not rename step titles. See `references/progress-format.md`.

### Phase 1: Install and initialize Sentry in api/

#### Automated

- [x] 1.1 `dotnet build` succeeds from `api/` — 8e01223
- [x] 1.2 `dotnet test api.Tests` passes with no regressions — 8e01223

#### Manual

- [x] 1.3 Local DSN via user-secrets; triggered exception confirmed in Sentry dashboard — 8e01223
- [x] 1.4 Production `Sentry__Dsn` Application Setting created — 8e01223

### Phase 2: Wire API-side audit findings to Sentry (L1, G4)

#### Automated

- [x] 2.1 `dotnet build` succeeds from `api/` — 358e810
- [x] 2.2 `dotnet test api.Tests` passes with no regressions — 358e810

#### Manual

- [x] 2.3 Data Protection failure confirmed reaching Sentry with the right message — 358e810
- [x] 2.4 Map-generation failure confirmed reaching Sentry with seed/roomCount/encounter attributes — 358e810

### Phase 3: Install and initialize Sentry in web/

#### Automated

- [x] 3.1 `npm run typecheck` passes — 7711c8b
- [x] 3.2 `npm test` passes with no regressions — 7711c8b
- [x] 3.3 `npm run build` succeeds locally with `VITE_SENTRY_DSN` unset — 7711c8b

#### Manual

- [x] 3.4 Local `.env.local` DSN; browser error confirmed in Sentry dashboard — 7711c8b
- [x] 3.5 `VITE_SENTRY_DSN` repository variable created — 7711c8b
- [ ] 3.6 Post-merge deploy build confirmed to pick up the variable (pending next merge to main)

### Phase 4: Wire web-side audit findings to Sentry (reportError, ErrorBoundary)

#### Automated

- [x] 4.1 `npm run typecheck` passes
- [x] 4.2 `npm test` passes, including existing `client.test.ts`/`auth.test.ts`

#### Manual

- [x] 4.3 All 4 `reportError` call sites confirmed reaching Sentry tagged with their call label
- [x] 4.4 Root `ErrorBoundary` throw confirmed reaching Sentry
