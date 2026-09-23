<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Cloud Account Store and Persistent Sessions

- **Plan**: context/changes/account-store-foundation/plan.md
- **Scope**: Phase 1 of 4
- **Reviewed phases**: 1
- **Date**: 2026-09-23
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 3 warnings, 3 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | WARNING |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

## Findings

### F1 — Every cold start reads the database, so any request can wake it

- **Severity**: ⚠️ WARNING
- **Impact**: 🔬 HIGH — architectural stakes; think carefully before deciding
- **Dimension**: Architecture
- **Location**: api/Program.cs:40-42
- **Detail**: `AddDataProtection()` registers the internal `DataProtectionHostedService`, which loads the key ring in `StartAsync` before Kestrel listens. With `PersistKeysToDbContext`, that is a `SELECT` on `DataProtectionKeys`, plus an `INSERT` on an empty database. The local logs from the 1.4 and 1.6 runs confirm it: the key query, and on the first run the insert, came before `Now listening`. On F1 (the app unloads after 20 minutes idle, and the database auto-pauses after 60 minutes), any visitor, crawler or `/api/health/live` ping after an idle hour resumes the database. It then stays up at least 60 minutes, spending free vCore-seconds, and startup also waits for the resume. This breaks the plan's claim that only `ready` (key-gated) wakes the database and that `live` is safe for monitoring (plan: Desired End State, Performance Considerations). It has no effect until Phase 3 sets the production connection string. After S-04, any request that carries an auth cookie will load keys anyway; that part is expected.
- **Fix A ⭐ Recommended**: Remove the key-ring preload hosted service after `AddDataProtection()`, so keys load lazily on the first protect or unprotect call. Add a Phase 2 test that `live` answers without touching the database. State the invariant in the plan.
  - Strength: Restores the plan's guarantee: after this, only `ready`, and later authenticated requests, touch the database.
  - Tradeoff: `DataProtectionHostedService` is internal, so it has to be matched by type name. A framework rename would silently bring the preload back unless a test pins it.
  - Confidence: MED — confirmed in logs; the fix mechanism has not been exercised yet.
  - Blind spot: whether any other framework component (antiforgery, Identity) resolves the key ring at startup. The logs show only Data Protection today.
- **Fix B**: Accept the startup read and update the plan: cold starts wake the database, and the free-limit usage check becomes a recurring task.
  - Strength: No framework workaround; the startup-time key-load failure is still logged at boot.
  - Tradeoff: Traffic spaced more than 60 minutes apart burns the free-tier budget. If it runs out, AutoPause takes the database offline until the 1st.
  - Confidence: MED — depends on real traffic patterns, which are unknown.
  - Blind spot: crawler and bot traffic volume on a public `azurewebsites.net` host.
- **Decision**: FIXED (Fix A) — preload removed in api/Program.cs; verified locally: 0 DataProtectionKeys queries at startup and on live, 1 on the first ready. Test + plan invariant queued in follow-ups/review-fixes.md

### F2 — Local SQL Server listens on all interfaces with a public SA password

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: compose.yaml:9-10
- **Detail**: `"1433:1433"` publishes the port on 0.0.0.0, and Docker's iptables rules bypass host firewalls such as ufw or firewalld. Anyone on the same network can log in as `sa` with the password committed to this public repo.
- **Fix**: Change the port mapping to `"127.0.0.1:1433:1433"`.
- **Decision**: FIXED — compose.yaml binds 127.0.0.1:1433; verified with `docker port`

### F3 — appsettings.Development.json ships in the production publish output

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/battle-map-generator-api.csproj
- **Detail**: The Web SDK publishes every `appsettings*.json` file. If App Service were ever switched to `ASPNETCORE_ENVIRONMENT=Development` to debug, `Health:ReadyKey` would fall back to the public `local-dev-only-ready-key` wherever `Health__ReadyKey` is unset. Unlikely, but that fallback turns a debugging switch into an open `ready` endpoint.
- **Fix**: Add `<Content Update="appsettings.Development.json" CopyToPublishDirectory="Never" />` to the csproj.
- **Decision**: FIXED — csproj excludes appsettings.Development.json from publish; verified the publish output holds only appsettings.json

### F4 — Ready-key comparison reveals the key's length

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:113-116
- **Detail**: `FixedTimeEquals` returns false immediately when the lengths differ, so timing reveals the byte length. Not exploitable in practice with a 64-hex-character random key and only 404 responses.
- **Fix**: Compare `SHA256.HashData` of both values, which have equal length.
- **Decision**: FIXED — compares SHA-256 hashes with FixedTimeEquals; ready probes re-verified (200 with key, 404 without or wrong)

### F5 — The Data Protection check's comment overstates what it proves

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Health/DataProtectionHealthCheck.cs:6-7
- **Detail**: After the first load, the key ring is cached in memory (refreshed about every 24 hours). Later `ready` calls round-trip in memory and never read `DataProtectionKeys`. The database side is still covered by `AddDbContextCheck`. If F1 Fix A is applied, `ready` becomes the first real key-ring load after each start, and the comment becomes accurate.
- **Fix**: None needed if F1 Fix A is applied; otherwise reword the comment to "key ring is usable".
- **Decision**: FIXED (via F1 Fix A) — with lazy loading, the first ready after each start is the real key-ring load from the database, so the comment is accurate

### F6 — `ready` has no request time limit while the database resumes

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:44-46
- **Detail**: No health-check timeout is set, so `ready` can run through the whole `EnableRetryOnFailure` window. That is intended, so the probe can wait for a resume. The Phase 4 CI curl has no `--max-time`, so a hung request would not become a retry.
- **Fix**: In Phase 4, add `--max-time` (e.g. 120) to the `ready` smoke-test curl; leave the app side as is.
- **Decision**: FIXED (queued) — `--max-time` for the Phase 4 ready curl queued in follow-ups/review-fixes.md
