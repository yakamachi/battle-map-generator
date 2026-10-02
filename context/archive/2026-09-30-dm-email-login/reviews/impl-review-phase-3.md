<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: DM Email Login (S-04)

- **Plan**: context/changes/dm-email-login/plan.md
- **Scope**: Phase 3 of 4
- **Reviewed phases**: 3
- **Date**: 2026-10-01
- **Verdict**: NEEDS ATTENTION
- **Findings**: 0 critical, 1 warning, 5 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | PASS |

Commit 0dbe607. Gates run before the commit: API tests 65/65, no contract drift, e2e 5/5 in both browsers, map.spec.ts and the Generate handler unchanged against main, two deliberate-break checks red (guard removed; limit keyed by IP). Manual 3.6: anonymous generate returned 401 with no Set-Cookie or Location; a registered account got 200. The reviewer also confirmed that casing, trailing slashes, dot segments and other methods never reach generation without a session, all with no SQL, and that the tests pin both middleware-order regressions.

## Findings

### F1 — A forged session cookie makes any request query the database, unthrottled

- **Severity**: ⚠️ WARNING
- **Impact**: 🔎 MEDIUM — real tradeoff; pause to reason through it
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:171 (UseAuthentication), context/foundation/lessons.md:38, api/AGENTS.md:22
- **Detail**: A cookie shaped like a Data Protection payload with an unknown key id (4-byte magic header plus a random GUID) makes the key ring reload on every request: 200 such requests to generate ran 200 SQL commands in 0.21 s, and the same happens on the SPA fallback and on /api/health/live (confirmed with a query counter). The answer is always 401, but it comes before the rate limiter and there is no global limiter, so anyone can keep the auto-pausing Azure SQL database awake and burn its free vCore-seconds without an account. A garbage or absent cookie costs nothing. The behaviour dates from phase 1; this phase's lesson update, api/AGENTS.md and commit message state the opposite for liveness and 401s.
- **Fix A ⭐ Recommended**: Authenticate only where a session matters, and throttle cookie-carrying requests per IP before authentication
  - Authentication runs only for `/api/*` outside `/api/health` (UseWhen), so liveness, static files and the SPA fallback never read a cookie; a per-IP limiter for requests that carry the session cookie runs before it (for example 60 per minute), bounding key-ring reloads per client. Correct the lesson's S-04 bullet and api/AGENTS.md; add tests with a forged cookie against a query counter or a fresh database.
  - Strength: Restores the lesson for liveness and the shell, and caps the cost per IP at a rate no real user reaches.
  - Tradeoff: Two more pipeline branches to keep in order; many IPs can still wake the database, only slower.
  - Confidence: MEDIUM — UseWhen and a partitioned limiter are standard; the exact reload behaviour of the key ring under the limiter needs the test to prove it.
  - Blind spot: Whether the key ring can be told to stop reloading on unknown keys (a Data Protection option) was not researched.
- **Fix B**: Accept and document
  - Correct the lesson and api/AGENTS.md to "any request carrying a session-shaped cookie, valid or not", and add the unthrottled reload to the infrastructure risk register.
  - Strength: No code change before the login UI.
  - Tradeoff: The free-offer database can be kept awake by anyone who knows the trick.
  - Confidence: HIGH — only documentation.
  - Blind spot: How quickly an attack would exhaust the monthly free vCore-seconds.
- **Decision**: FIXED via Fix A — `api/Auth/SessionCookieGate.cs` strips the session cookie outside API routes (and under /api/health) and limits cookie-carrying API requests to 60/min per IP before authentication; tests with a forged cookie on an empty key ring and on the limit (both went red when broken). Moving UseAuthentication into a UseWhen branch was tried first and made the framework insert its own UseAuthentication ahead of static files; documented in api/AGENTS.md.

### F2 — A cookie stays valid up to 30 minutes after its account changes

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:77-94
- **Detail**: With the user row deleted, the old cookie still generated (200) and `me` returned the deleted email, with no SQL: Identity re-checks the security stamp every 30 minutes by default. No account-deletion or password-change feature exists yet, so this is the framework's default and harmless today.
- **Fix**: Note in api/AGENTS.md that a feature which deletes an account or changes a password must lower `SecurityStampValidatorOptions.ValidationInterval` or check the stamp on that action.
- **Decision**: FIXED (note in api/AGENTS.md)

### F3 — A generate during a database resume can read as a logout

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: api/Program.cs:62-73
- **Detail**: A valid cookie sent to an instance that cannot read the key ring got 401, not 5xx (confirmed with an unreachable database). On Azure, if a logged-in generate arrives while the paused database is resuming and the EF retries run out, the user gets 401, and phase 4's client turns that into a redirect to /login. Unconfirmed on Azure.
- **Fix**: Record it in the infrastructure risk register and add a note to phase 4 of the plan: the login page shown after such a redirect should say the session may have ended because the server was waking up.
- **Decision**: FIXED (risk-register row in infrastructure.md; phase 4 plan note: redirect to /login?expired=1 with an explanatory message)

### F4 — The generate endpoint declares only 200 in the OpenAPI document

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api/Maps/MapEndpoints.cs:16-19
- **Detail**: `me` declares 401 and register/login declare 429, but generate declares neither. The brief limited MapEndpoints.cs to adding `.RequireAuthorization()` because S-03 edits the file in parallel, so this is not drift. The client handles 401 and 429 by status, so nothing depends on the declaration.
- **Fix**: After S-03 merges, add `.Produces(401)` and `.Produces(429)` to the generate chain and regenerate both contract files (a follow-up, not in this change).
- **Decision**: FIXED as a follow-up (follow-ups/review-fixes.md, after S-03 merges)

### F5 — MapEndpointTests class comment is inaccurate

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: api.Tests/MapEndpointTests.cs:10-14, :19-20
- **Detail**: The comment says the anonymous tests point at an unreachable database, but `Anonymous_generate_returns_401_and_spends_no_permits` uses the real one. A double blank line is left from the phase 2 helper move.
- **Fix**: Reword to "the tests that assert the database is never touched point at an unreachable database" and remove the extra blank line.
- **Decision**: FIXED

### F6 — The roadmap still calls generation public until S-04

- **Severity**: 🔍 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Plan Adherence
- **Location**: context/foundation/roadmap.md:65, :153
- **Detail**: The baseline says generation is public and rate-limited until S-04, and open question 2 asks about public generation before login. S-04 resolves both. Roadmap state normally changes at closeout.
- **Fix**: Update both in the S-04 closeout pull request.
- **Decision**: FIXED as a follow-up (follow-ups/review-fixes.md, S-04 closeout)

## Noted, no action proposed

- The per-account limiter's partitions are keyed by user id (a GUID) and scoped per policy, so accounts cannot collide; idle partitions are dropped by the limiter (not measured).
- access.spec.ts runs once per browser project: redundant but harmless (2 anonymous calls, no permits).
