<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: DM Email Login (S-04)

- **Plan**: context/changes/dm-email-login/plan.md
- **Scope**: Phase 1 of 4
- **Reviewed phases**: 1
- **Date**: 2026-09-30
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 5 warnings, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Automated criteria rerun on commit 3d13765: API tests 56/56, configuration-free build, no contract drift, migrations bundle, no pending migration, web typecheck. Manual row 1.7 was run with curl on the user's request (register 200, me 200, logout 204, me 401, login 200).

## Findings

### F1 — Register with an email longer than 256 characters returns 500

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Auth/AuthEndpoints.cs:40
- **Detail**: Identity's validators accept a 300-character email, the INSERT into AspNetUsers fails on the column length, and the DbUpdateException escapes the endpoint (confirmed by the reviewer with a request). Two concurrent registers of one email can hit the same unhandled path through the unique index (not run).
- **Fix**: Reject an email longer than 256 characters before CreateAsync with a validation problem keyed `email`, and add a test.
- **Decision**: FIXED

### F2 — No upper bound on password length

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Auth/AuthEndpoints.cs:40, :60
- **Detail**: A 20 MB password was accepted by register and processed by login (confirmed). Each such request holds the body plus a UTF-16 string of about 40 MB, ten times a minute per IP, on the F1 plan.
- **Fix**: Cap the password at 128 characters in register (400 keyed `password`) and login (401), and add tests.
- **Decision**: FIXED

### F3 — The cookie's Secure flag and the auth limit depend on an app setting nothing pins

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:45-52, :99
- **Detail**: `ASPNETCORE_FORWARDEDHEADERS_ENABLED` appears only in comments and api/AGENTS.md. If it is missing on the App Service, the app sees every request as HTTP from the proxy: the 14-day session cookie is issued without `Secure`, and all clients share one `auth` bucket, so ten requests a minute from anyone block register and login for everybody. The plan defers the check to after the merge (row 4.11). Whether the setting is present is unverified.
- **Fix A ⭐ Recommended**: Check the setting on the App Service now (read-only `az webapp config appsettings list`), set it if missing (owner), and record the result in the plan
  - Strength: Settles both the Secure flag and the shared bucket before any login code ships; no code change.
  - Tradeoff: Still relies on configuration outside the repo; a re-created App Service could lose it.
  - Confidence: HIGH — it is the documented App Service mechanism and api/AGENTS.md already names it.
  - Blind spot: The client IP taken from X-Forwarded-For behind App Service has not been tested against the real proxy.
- **Fix B**: Set `CookieSecurePolicy.Always` outside Development and Testing
  - Strength: The Secure flag no longer depends on proxy configuration.
  - Tradeoff: e2e runs the Production host over plain http://localhost; a Secure cookie there may not be stored or sent by the Playwright request context, so phase 2 would need an override. Does nothing for the shared rate-limit bucket.
  - Confidence: MEDIUM — browser handling of Secure cookies on localhost differs by client.
  - Blind spot: Not tried with Playwright's storageState.
- **Decision**: FIXED via Fix A — checked 2026-09-30 with `az webapp config appsettings list`: `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is `true` on the App Service (not a slot setting). No change needed; recorded in plan-brief.md.

### F4 — Authentication runs for static files and liveness, so a cookie on any URL can touch the database

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:147
- **Detail**: `UseAuthentication` sits ahead of `UseDefaultFiles`/`UseStaticFiles`. With an unreachable database, `GET /api/health/live` carrying a cookie shaped like a protected payload took 1.5 s: the key ring load was attempted (confirmed). An anonymous caller can thus wake a paused database on any URL after a cold start. Unconfirmed but likely: a logged-in user whose security stamp is due for revalidation gets a 500 or a long wait on `/` and assets while the database is paused. Violates the spirit of the lesson "Only work that needs the database may touch it".
- **Fix**: Move `UseDefaultFiles`/`UseStaticFiles` above `UseAuthentication` so the shell and assets never authenticate; pin it with a test (static file with a session-shaped cookie answers quickly with the database unreachable); state in api/AGENTS.md that API routes and the SPA fallback still authenticate.
- **Decision**: FIXED

### F5 — Anyone can keep a known account locked out

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:82-83, api/Auth/AuthEndpoints.cs:60
- **Detail**: Five wrong passwords lock an account for 5 minutes and the per-IP limit allows ten calls a minute, so five requests every 5 minutes keep a victim out indefinitely. Register's duplicate-email answer tells the attacker which emails exist, and there is no reset or unlock path. This is the plan's own decision (lockout chosen in planning), reviewed here as a plan issue.
- **Fix A ⭐ Recommended**: Accept for the MVP and record it under the plan's open risks
  - Strength: Lockout still stops password guessing, which is the larger risk with open registration; the user base is the author and a few known DMs.
  - Tradeoff: A targeted nuisance attack on a known email is possible.
  - Confidence: HIGH — matches the plan's scope (no password reset in the MVP).
  - Blind spot: None significant.
- **Fix B**: Shorten the lockout to 1 minute
  - Strength: A victim is never out for long once the attacker stops.
  - Tradeoff: Password guessing speeds up from 5 to 25 attempts per 5 minutes per account; a persistent attacker still locks the account.
  - Confidence: MEDIUM — reduces, does not remove.
  - Blind spot: None significant.
- **Decision**: ACCEPTED via Fix A (risk recorded in plan-brief.md)

### F6 — The no-antiforgery design rests on behaviour no test pins

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api.Tests/AuthEndpointTests.cs
- **Detail**: A form-encoded or text/plain POST to register or login does not match the JSON-only endpoints and gets 404 (confirmed by the reviewer), which is what stops a cross-site form from driving them. Nothing pins this; a later change that accepts form bodies would silently open login CSRF.
- **Fix**: Add a test: form-encoded and text/plain POSTs to register and login return no session cookie and a non-success status.
- **Decision**: FIXED

### F7 — A database failure on register or login is an unhandled 500

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Auth/AuthEndpoints.cs:40, :54
- **Detail**: With the database unreachable, login throws SqlException out of the pipeline (confirmed). In production that is a bare 500 with no details, so nothing leaks, but the SPA cannot tell "database waking up" from a bug. A resuming Azure SQL database first holds the request through the EF retries.
- **Fix A ⭐ Recommended**: Leave the API as it is; in phase 4 the forms show "The server is not ready yet. Try again in a minute." for any 5xx
  - Strength: No new error-mapping code in the API; the user-visible outcome is the same.
  - Tradeoff: A real bug also reads as "try again".
  - Confidence: MEDIUM — depends on phase 4 wording.
  - Blind spot: None significant.
- **Fix B**: Map database connection failures on register and login to 503 with Retry-After
  - Strength: The contract says what happened; the client can be precise.
  - Tradeoff: Exception-type matching on SqlException/RetryLimitExceededException to get right and test.
  - Confidence: MEDIUM.
  - Blind spot: Which exception types a resuming Azure SQL database surfaces after retries has not been observed.
- **Decision**: FIXED via Fix A (5xx wording added to phase 4 of the plan)

### F8 — Validation messages: a confusing extra message for an empty password

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Auth/AuthEndpoints.cs:85-108
- **Detail**: An empty or missing password returns two messages, the second being "Passwords must use at least 1 different characters." (confirmed). Also noted, no change proposed: when both fields are invalid only password errors come back (Identity validates the password first), and a request with no body or malformed JSON gets the framework's plain 400 without `errors`, so the phase 4 client must not assume `errors` is present.
- **Fix**: Drop the `PasswordRequiresUniqueChars` error from the response and pin the empty-password case in a test.
- **Decision**: FIXED

### F9 — Login comment overstates "indistinguishable"

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Auth/AuthEndpoints.cs:50
- **Detail**: The responses are identical, but an unknown email returns without hashing a password and a locked-out account skips hashing too, so response time reveals both. Low severity because register already discloses which emails exist, by decision.
- **Fix**: Reword the comment to say the responses are identical and that timing is not equalised, with the reason.
- **Decision**: FIXED

### F10 — Test gaps and duplicated helpers

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/AuthEndpointTests.cs:20-22, :280-291
- **Detail**: The database-free rule is pinned for anonymous `me` but not for anonymous logout, which resolves SignInManager (it passes today). `UnreachableConnectionString` is now copied in three test classes and `NewEmail()` duplicates the generator in ApiFactory.
- **Fix**: Add anonymous logout to the unreachable-database test; move `UnreachableConnectionString` and the email generator into ApiFactory and use them from the three classes.
- **Decision**: FIXED

## Noted, no action proposed

- Logout deletes the browser cookie only; a copied cookie stays valid until it expires (14 days sliding), because the security stamp is not rotated on logout. Logout can also be triggered by a cross-site form POST, which is a nuisance only.
- The `auth` limit partitions by full IP address, so an IPv6 client with a /64 has many buckets. The per-account lockout still bounds password guessing.
- No session fixation, email lookup is case-insensitive, unknown `/api/*` paths still return 404, and the code matches the sibling files' patterns.
