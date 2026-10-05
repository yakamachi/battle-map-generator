# Test Plan

> Phased test rollout for this project. Strategy is frozen at the top
> (§1–§5); cookbook patterns at the bottom (§6) fill in as phases ship.
> Read before writing any new test.
>
> Refresh: re-run `/10x-test-plan --refresh` when stale (see §8).
>
> Last updated: 2026-10-05

## 1. Strategy

Tests follow three non-negotiable principles for this project:

1. **Cost × signal.** The cheapest test that gives a real signal for the
   risk wins. Do not promote to e2e because e2e "feels safer." Do not put a
   vision model on top of a deterministic visual diff that already catches
   the regression.
2. **User concerns are first-class evidence.** Risks anchored in "the
   team is worried about X, and the failure would surface somewhere in
   area Y" carry the same weight as PRD lines or hot-spot data.
3. **Risks are scenarios, not code locations.** This plan documents *what
   could fail* and *why we believe it's likely* — drawn from documents,
   interview, and codebase *signal* (churn, structure, test base). It does
   NOT claim to know which line owns the failure. That knowledge is
   produced by `/10x-research` during each rollout phase. If the plan and
   research disagree about where the failure lives, research is the
   ground truth.

Hot-spot scope used for likelihood weighting: `api/`, `web/app/`, `e2e/`,
excluding `api/.claude/` (skill files, not application code), `node_modules/`,
`build/`, `bin/`, `obj/`.

## 2. Risk Map

The top failure scenarios this project must protect against, ordered by
risk = impact × likelihood. Risks are failure scenarios in user / business
terms, not test names. The Source column cites the *evidence that surfaced
this risk* — never a specific file as "where the failure lives" (that is
research's job, see §1 principle #3).

| # | Risk (failure scenario) | Impact | Likelihood | Source (evidence — not anchor) |
|---|---|---|---|---|
| 1 | After an algorithm change, or for a seed × room-count × encounter combination no test pins, the generated map is invalid: a room is unreachable, tiles fall outside the grid, or half-cells appear, and the DM gets an unusable map | High | High | interview Q1 (generation stops working, artifacts appear), Q3 and Q4 (no confidence changing the layout logic outside the fixtures); PRD Guardrail (grid alignment is the one must-have); hot-spot dir `api/Maps/` (6 commits/30d); PRD FR-007 and FR-008 (planned generator changes) |
| 2 | The same seed and the same parameters produce a different map on a later request, after a restart, or in the browser render (sprite variant changes between renders), so a DM cannot reproduce a map they liked | High | Medium | interview Q4 (the user's largest current worry; impact rated High at the user's request); AGENTS.md determinism rule (same seed → same grid, web variants derived from seed); infrastructure.md pre-mortem (a .NET upgrade changed the layout for the same seed) |
| 3 | A scripted client creates accounts and generates maps, bypassing the per-account generation limit, and exhausts the free plan's allowance, so the app stops serving everyone | High | Medium | PRD Access Control (open registration; the per-account limit exists to protect the free plan); AGENTS.md rate limits (`auth` 10/min per IP, `generate` 10/min per account); infrastructure.md risk register |
| 4 | Generation cost grows (new features, larger maps, a slower host CPU than the dev machine) until it exhausts the free plan's CPU or bandwidth quota, and the whole app returns 403 until the quota resets | High | Low | interview Q1 (cost concern); infrastructure.md: F1 limits (60 CPU-min and 165 MB per day); the current "1000 generations under 1 s" figure was measured locally, not on F1; PRD FR-007 and FR-008 may raise per-generation work |
| 5 | The first request after idle (database auto-pause, App Service unload) ends in 401 or a silent logout instead of a correct response | High | Low | infrastructure.md: Data Protection key persistence and the auto-pause resume error; AGENTS.md (`EnableRetryOnFailure`, key ring in the database); infrastructure.md register row (M/L) |
| 6 | The boss arena is indistinguishable from ordinary rooms, or sits below its minimum floor for its boss size; the hardest combination is 12 rooms with a Gargantuan boss | Medium | Medium | interview Q5 (the only shape check the user wants: boss arena differs from other rooms); PRD Business Logic (floors 8×8 / 10×10 / 12×12); roadmap S-03 unknown (12 rooms with Gargantuan boss vs. the 60×60 size cap) |

**Impact × Likelihood rubric.**

| Rating | Impact | Likelihood |
|--------|--------|------------|
| High   | user loses access, data, or money; failure is publicly visible | area changes weekly, or we have already been burned here |
| Medium | feature degrades, a workaround exists, only some users affected | touched occasionally, has been a source of bugs |
| Low    | cosmetic, easily reverted, no data effect | stable code, rarely touched |

Risks #3 is the abuse lens: the product has open registration and an
unauthenticated boundary, so at least one row is a scenario only an
attacker's path surfaces. No row covers resource ownership (IDOR): the API
exposes no endpoints for another account's data beyond the session.

### Risk Response Guidance

| Risk | What would prove protection | Must challenge | Context `/10x-research` must ground | Likely cheapest layer | Anti-pattern to avoid |
|------|-----------------------------|----------------|--------------------------------------|-----------------------|-----------------------|
| #1 | Every valid combination of seed, room count, encounter type and boss size yields a grid that is grid-aligned, has every room reachable, keeps every tile inside the bounds, and has no half-cells | "the fixtures already cover the algorithm" — they pin three seeds plus two special maps | the generator's real parameter surface; whether invariants are already asserted generically or only through fixed fixtures; the invariant definitions as the PRD states them | unit: parametrized sweep over the parameter space, no I/O | asserting the current output for each seed instead of the PRD invariants; adding one fixture per new seed (linear coverage) |
| #2 | Two independent requests with the same seed and parameters return the same grid; the browser render draws the same sprite variant on each re-render | "same seed in one process means same result" — restarts, a different process, or a different runtime version can still change output | where the PRNG state is created and consumed; every place randomness enters the web render; which inputs the seed covers | unit (repeat generation); integration (two endpoint calls); web render test (same grid, same variant on re-render) | comparing a result with itself after a single run; a test that passes because both sides read the same mutable object |
| #3 | A burst of account creation plus generate calls cannot exceed a bounded ceiling, regardless of how many accounts are created | "the per-account limit already protects the plan" — nothing bounds the number of accounts, so the real ceiling is (accounts per minute) × 10 | the `auth` policy's shared register and login bucket; the `generate` per-account policy; the realistic CPU and bandwidth cost of one generate at this project's scale | integration test on the rate-limit policy under scripted multi-account registration | concluding "limited is limited enough" without computing the ceiling; scoping the fix behind CAPTCHA or email verification (both are non-goals) |
| #4 | Measured CPU time, wall time and response size for the largest valid parameter set stay within a budget on the target host, and the budget fails the build before it fails the quota | "1000 generations under 1 s on the dev machine means it's cheap on F1" — the F1 CPU is slower and the planned features add work | the target host's real CPU per generation; the largest valid combination; the F1 quota arithmetic from infrastructure.md | integration timing test with a generous threshold, plus a one-off measurement on F1 | setting the threshold from a laptop run; a timing assertion tight enough to flake on shared CI runners |
| #5 | The first authenticated request after a real idle period completes correctly, possibly slower, never as a silent logout or a bare 401 | "the integration test database is always warm, so the retry path is untested" — it is never cold in tests | `EnableRetryOnFailure` behavior on the auto-pause error; the key-ring reload path under the same retry | integration test on the retry-then-succeed path; a post-idle probe is a gate candidate, not a unit test | treating "works after a retry locally" as proof of the first request's user-visible behavior |
| #6 | Every boss-fight map has a boss arena that is visibly larger than every other room and meets the floor for its boss size, including the largest combination | "the size table already handles every tier" — none of the pinned fixtures use the largest combination | the exact size table and its boundary values; whether the 60×60 cap can be exceeded by the largest valid combination | unit: parametrized over boss size and room count, shared with risk #1's sweep | asserting the current arena size instead of the PRD floor; checking only the default 6-room map |

## 3. Phased Rollout

Each row is a discrete rollout phase that will open its own change folder
via `/10x-new`. Status moves left-to-right through the values below; the
orchestrator updates Status as artifacts appear on disk.

| # | Phase name | Goal (one line) | Risks covered | Test types | Status | Change folder |
|---|---|---|---|---|---|---|
| 1 | Invariant and boss-arena sweep | Prove every valid parameter combination yields a valid grid and a correctly sized boss arena | #1, #6 | unit (parametrized sweep) | change opened | context/changes/testing-invariant-boss-sweep/ |
| 2 | Seed determinism | Prove the same seed and parameters reproduce the same grid and render variant, across requests and re-renders | #2 | unit, integration, web render test | not started | — |
| 3 | Abuse-resistant quota | Turn "the per-account limit protects the plan" into a measured, tested ceiling | #3 | integration under scripted multi-account load | not started | — |
| 4 | Generation cost budget | Measure CPU, wall time and response size on the target host, then gate on a budget | #4 | integration timing test; one-off F1 measurement | not started | — |
| 5 | Cold-start resilience | Automated guard for the first request after idle | #5 | integration (retry-then-succeed); probe as gate candidate | not started | — |

Phase order follows impact × likelihood. The user may reorder; Phase 2
(determinism) is the user's stated top worry and is cheap to do early.

**Status vocabulary** (fixed — parser literals): `not started` →
`change opened` → `researched` → `planned` → `implementing` → `complete`.

## 4. Stack

The classic test base for this project. No AI-native layer is proposed.

| Layer | Tool | Version | Notes |
|---|---|---|---|
| unit + integration (api) | xUnit + Testcontainers.MsSql | per `api.Tests` project file; verify in research | `api.Tests/`; hosts the app via `WebApplicationFactory` in the `Testing` environment; checked: 2026-10-05 |
| unit + integration (web) | Vitest (browser mode) | per `web/package.json`; verify in research | `web/app/map/*.test.ts`, real Chromium and Firefox, draws the shared fixture grids; checked: 2026-10-05 |
| visual (deterministic) | Playwright (`web/playwright.visual.config.ts`) | per `web/package.json`; verify in research | 3 pinned themes and screens; checked: 2026-10-05 |
| e2e | Playwright (`e2e/playwright.config.ts`) | per `e2e/package.json`; verify in research | Chromium and Firefox against the real .NET host and SQL Server; no mocks; checked: 2026-10-05 |
| contract | `openapi-typescript` + committed diff gate | per `web/package.json` | CI fails on drift between the OpenAPI document and `schema.d.ts`; checked: 2026-10-05 |
| (optional) AI-native | none proposed | n/a | The classic layers give deterministic signal for all six risks. |

**Stack grounding tools (current session):**
- Docs: Context7 available in session — not queried; the stack is already in use and no new library is needed. checked: 2026-10-05
- Search: Exa available in session — not queried for the same reason. checked: 2026-10-05
- Runtime/browser: no Playwright MCP exposed in session; the project drives Playwright directly. checked: 2026-10-05
- Provider/platform: no GitHub or Azure MCP exposed in session; not used. checked: 2026-10-05

## 5. Quality Gates

"Required for §3 Phase <N>" means the gate is enforced once that rollout
phase lands; before that, the gate is `planned`.

| Gate | Where | Required? | Catches |
|---|---|---|---|
| lint + typecheck | local + CI | required (wired; `ci.yml` jobs `api`, `web`) | syntactic / type drift |
| unit + integration (api, web) | local + CI | required (wired; `ci.yml` jobs `api`, `web-tests`) | logic regressions |
| contract drift | CI | required (wired; `ci.yml` job `contract`) | API/client schema drift |
| visual diff (deterministic) | CI on PR | required (wired; `ci.yml` job `visual`) | rendering regressions on the 3 pinned screens |
| e2e on critical flows | CI on PR | required (wired; `ci.yml` job `e2e`) | broken critical flows, Chromium + Firefox |
| invariant sweep | local + CI | required after §3 Phase 1 | invalid grids for untested combinations; boss arena size |
| determinism | local + CI | required after §3 Phase 2 | same seed producing a different map or variant |
| rate-limit ceiling | local + CI | required after §3 Phase 3 | quota exhaustion via account cycling |
| generation cost budget | CI | required after §3 Phase 4 (threshold set from the F1 measurement) | CPU or response-size growth beyond the plan's budget |
| cold-start guard | local + CI | required after §3 Phase 5 | silent auth or generate failure after idle |
| post-edit hook | local | not proposed in this rollout | — |
| multimodal visual review | — | not proposed | the deterministic visual gate covers the pinned screens |
| pre-prod smoke | between merge and prod | already exists | `deploy.yml` probes `/api/health/live` and `/api/health/ready` |

## 6. Cookbook Patterns

How to add new tests in this project. Each sub-section is filled in once
the relevant rollout phase ships; before that, the sub-section reads
"TBD — see §3 Phase <N>."

### 6.1 Adding an invariant or boss-arena test for the generator

- **Location**: `api.Tests/MapGeneratorTests.cs`, theory `Every_guarantee_holds_for_consecutive_seeds`, fed by `ParameterCombinations` (every room count for a skirmish and for each boss size, 44 combinations).
- **Naming**: `Every_guarantee_*` for a sweep over the parameter space and all seeds; `*_follows_*` for a rule checked over a table (for example `Map_size_follows_the_room_count_and_grows_for_a_boss`).
- **Reference test**: `Every_guarantee_holds_for_consecutive_seeds`.
- **Run**: `dotnet test api.Tests --filter "FullyQualifiedName~MapGeneratorTests"`.
- **Seed budget**: 1000 consecutive seeds per combination (`SweepSeedCount`). The determinism and different-seeds tests keep their own budget (`SeedCount`).
- **Adding an invariant**: write it as an `Assert*` helper taking the `Grid`, the `seed`, and any parameters it needs (for example `AssertArena(grid, bossSize, seed)`), and call it from `Every_guarantee_holds_for_consecutive_seeds`. Take its oracle from the PRD (the invariant as the PRD states it), never from the generator's current output.
- **Not here**: a determinism check or a fixture change does not belong in this theory. Determinism tests are separate theories in the same class (for example `The_same_seed_and_parameters_give_identical_maps`); fixed-seed grids live in `fixtures/grids/*.json` and are pinned by `MapFixtureTests`.

### 6.2 Adding a determinism test

TBD — see §3 Phase 2 (repeat generation, endpoint round-trip, web re-render).

### 6.3 Adding a rate-limit ceiling test

TBD — see §3 Phase 3 (scripted multi-account load against the `auth` and `generate` policies).

### 6.4 Adding a generation cost budget test

TBD — see §3 Phase 4 (timing and response-size budget for the largest valid parameters).

### 6.5 Adding a cold-start test

TBD — see §3 Phase 5 (simulated auto-pause, retry-then-succeed path).

### 6.6 Adding a test for a new API endpoint

- **Test type**: integration (preferred), following the existing pattern in `api.Tests/MapEndpointTests.cs` and `AuthEndpointTests.cs`.
- **Pattern**: host via `WebApplicationFactory` in the `Testing` environment (`api.Tests/Infrastructure/ApiFactory.cs`); assert request → response shape and the database-touch rules from `api/AGENTS.md`.
- **When to add e2e instead**: only if the failure mode needs the full deployed shape (session cookie, auth, and handler together); see `e2e/AGENTS.md`.

### 6.7 Per-rollout-phase notes

(Appended by `/10x-implement`'s final sub-phase as each phase lands.)

## 7. What We Deliberately Don't Test

Exclusions agreed during the rollout (Phase 2 interview, Q5). Future
contributors should respect these unless the underlying assumption
changes.

- **Admin panel** — there is no administrator concept in the MVP. Re-evaluate if an admin role is added. (Source: interview Q5.)
- **Standalone public API tests** — the API is served only together with the frontend. Re-evaluate if the API is published separately. (Source: interview Q5.)
- **Exact room and corridor shape and look** — only the boss-arena distinctness is tested (risk #6). Re-evaluate if shape becomes a PRD requirement. (Source: interview Q5.)
- **The generated OpenAPI client, beyond the existing drift gate** — the gate already fails the build on drift. (Source: interview Q5, by extension.)
- **Visual snapshots beyond the 3 pinned themes and screens** — diminishing returns at this scale. (Source: interview Q5.)
- **Map library, CRUD, offline mode** — PRD non-goals; no test budget until they enter scope.

## 8. Freshness Ledger

- Strategy (§1–§5) last reviewed: 2026-10-05
- Stack versions last verified: not yet — versions to be confirmed in per-phase research
- AI-native tool references last verified: n/a (none proposed)

Refresh (`/10x-test-plan --refresh`) when:

- a new top-3 risk surfaces from the roadmap or archive,
- a recommended tool's `checked:` date is older than three months,
- the project's tech stack changes (new framework, new test runner),
- §7 negative-space no longer matches what the team believes.
