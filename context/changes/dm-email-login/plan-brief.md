# DM Email Login (S-04) — Plan Brief

> Full plan: `context/changes/dm-email-login/plan.md`
> Research: `context/changes/dm-email-login/research.md`

## What & Why

The DM registers and logs in with email and password; without a session nobody can generate a map, in the UI or through the API; each account may generate 10 maps per minute. Open registration lets other DMs start without the author, and the limit replaces the protection that "no accounts" gives the free hosting plan today.

## Starting Point

The account store from F-01 is in place (Identity tables, key ring in the database), but there is no cookie scheme, no endpoint and no auth middleware. Generation is public, limited per IP; the SPA has one route; e2e runs without a database.

## Desired End State

Opening the app logged out shows the login page, with a link to register. After logging in, the DM sees the home view under a header with their email and a Log out button, and generates and downloads as before. A direct API call without a session gets 401, and the 11th generation in a minute by one account gets 429.

## Key Decisions Made

| Decision | Choice | Why (1 sentence) | Source |
| --- | --- | --- | --- |
| Generation limit | 10 per minute per account, in memory | Smallest change, keeps the "wait a minute" message in `home.tsx` true, fits e2e's 4 calls, no migration | Plan |
| Register and login guard | Per-IP limit of 10 per minute across both, plus lockout | These stay public and hash passwords on the F1 plan | Plan |
| Endpoints | Hand-written `register`, `login`, `logout`, `me` under `/api/auth` | The contract holds only what the PRD needs | Plan |
| 401 on generate mid-session | `client.ts` sends the browser to `/login` | `home.tsx` cannot take a new error kind | Research / Plan |
| Logout | Header row in the protected layout | Logout where users look for it; costs regenerated home baselines | Plan |
| `client.ts` against S-03 | Auth calls in a new `auth.ts`; `client.ts` changes by a few lines | S-03 must change the generate request in the same file | Research / Plan |
| e2e database | `compose.yaml` locally and in CI, `dotnet ef database update` | One definition of the database everywhere | Plan |
| e2e session | Setup project registers or logs in through the API and saves `storageState` | `map.spec.ts` stays unchanged | Brief |
| Phase order | Guard generation only after e2e holds a session | Every phase leaves CI green | Plan |
| Password rule | At least 8 characters, no composition rules | Simple to explain on the form | Plan |
| Duplicate email | 400 naming the email as taken | Without email verification there is no way to hide it | Plan |
| Failed login | One 401 for wrong password, unknown email and lockout | Does not reveal which emails have accounts | Plan |

## Scope

**In scope:**
- Cookie auth and four endpoints in `api/`, a per-IP limit on register and login
- `.RequireAuthorization()` on generate, limiter keyed by account
- Logged-in client helper and moved generation tests in `api.Tests/`
- Login and register pages, protected layout with header, `Input` and `Label` primitives
- Visual baselines: 22 home regenerated, 12 new for login and register
- SQL Server for e2e locally and in CI, setup project, access and auth specs

**Out of scope:**
- Stored or daily quota, any migration
- Email verification, password reset or change, two-factor, external logins, roles
- Edits to `home.tsx`, the `Generate` handler, the body of `map.spec.ts`
- Hand merges of the generated contract files
- Changes to `deploy.yml` or App Service settings

## Architecture / Approach

Identity's application cookie, protected by the database key ring, carries the session. Four minimal endpoints under `/api/auth` sit on `SignInManager`. The pipeline runs authentication, then authorization, then the rate limiter, so an anonymous generate gets 401 before spending a permit and the limiter can key by account id. In the SPA a layout route's `clientLoader` asks `GET /api/auth/me` and redirects to `/login` on 401; login and register are sibling routes outside the layout. e2e logs in once through the API and shares the saved cookie with both browsers.

## Phases at a Glance

| Phase | What it delivers | Key risk |
| --- | --- | --- |
| 1. API auth endpoints | Register, login, logout, `me`, cookie, per-IP limit, test helper, regenerated contract | Startup must stay configuration-free for the OpenAPI build and the migrations bundle |
| 2. e2e database and saved session | Healthchecked compose database, `db:up`, setup project, CI step | SQL Server start time and image pull in the required `e2e` job |
| 3. Guard generation, limit per account | 401 for anonymous, 10 per minute per account, generation tests on a real database | Reverses the "generation never touches the database" tests; the anonymous path must stay database-free |
| 4. Login UI | Pages, protected layout with header, 401 redirect, visual baselines, e2e auth spec | Header changes all home baselines and the preview's height budget |

**Prerequisites:** Docker for the local SQL Server; the S-03 session knows this change edits a few lines of `client.ts` and may add `Input`/`Label`.
**Estimated effort:** about 4 sessions, one per phase, each followed by `/10x-impl-review`.

## Open Risks & Assumptions

- Whether `ASPNETCORE_FORWARDEDHEADERS_ENABLED` is set in production is unknown. Without it the cookie lacks `Secure` and all users share one `auth` bucket; the deployed-app check in phase 4 finds out, and setting it is the owner's step.
- The limit resets on every restart, and each new account gets its own 10 per minute. Accepted for the MVP; a stored quota is a later change.
- Register and login share 10 calls per minute per IP, and a full e2e run uses 6. Quick local reruns can meet a 429.
- A login right after the database auto-paused can take a minute or fail once.
- `AddIdentityCookies()` and the 401-instead-of-redirect behaviour are taken from docs and general knowledge; phase 1 tests pin both.
- The plan rewords one comment in `MapEndpoints.cs` that becomes false, beyond the brief's "only add `.RequireAuthorization()`".

## Success Criteria (Summary)

- A new DM can register, generate, download, log out and log in again in Chrome and Firefox.
- Without a session, the app shows only login and register, and the generate API answers 401.
- Restarting the deployed app does not log the DM out.
