<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Cloud Account Store and Persistent Sessions

- **Plan**: context/changes/account-store-foundation/plan.md
- **Scope**: Full plan
- **Reviewed phases**: 1, 2, 3, 4
- **Date**: 2026-09-25
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 2 observations

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | WARNING |
| Architecture | PASS |
| Pattern Consistency | PASS |
| Success Criteria | WARNING |

Evidence:

- **Phase 4 contract:** every item matches `deploy.yml`, with two documented adaptations.
  - The retry loop plus `Connect Timeout=60` (`change.md`, Phase 4 adaptation). The resume took about 3 minutes, and EF's retries gave up after about 77 s.
  - `--max-time 120` on the ready smoke test (follow-up F6).
- **Harmless additions:** the job timeouts (build 20 min, deploy 40 min) and passing the key through a step `env`.
- **SqlClient version:** it resolves to 6.1.6, as the Critical Implementation Details section requires.
- **Desired End State:** every bullet is met by code plus recorded production proofs (`change.md`, Phase 4 production proofs, runs 36138357554 and 36139716216).
- **Cross-phase consistency:** config keys match between `Program.cs` and `ApiFactory`, `Test API` still runs before publish, and the `.http` file and smoke tests use the current probe paths.
- **"What We're NOT Doing":** every item holds.
- **Success criteria:**
  - Re-run during this review: 1.1 and 1.2 pass, 2.1 / 4.3 pass 14/14, 2.2 shows no `bin/` or `obj/`, and 4.1 passes.
  - Not re-run: 1.3, 4.2 and 3.x. The tests migrate a real SQL Server, the bundle was built earlier on 2026-09-25, and the Azure state was re-verified in the Phase 3 review.
  - Progress: 24/24 rows done. The epilogue deploy run 36144687358 is green.
- **Workflow security:** secret handling, per-job OIDC scope and both artifact contents were checked, with no issues found.

## Findings

### F1 — Firewall cleanup treats any `show` failure as "rule not found"

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/deploy.yml:144-152 (Close SQL firewall for runner)
- **Detail**: `if az sql server firewall-rule show … 2>/dev/null; then delete; else echo "not found"; fi`. A transient ARM error, throttling or an expired token makes `show` fail. The step then logs "nothing to remove" and exits 0, leaving `gh-runner-<run_id>` open with no failure anywhere. This was noted as a minor point in Phase 4 and deliberately accepted then. The safety review raised it independently, so it's recorded here for a decision.
- **Fix**: Drop the `show` pre-check and `2>/dev/null`. Run `az sql server firewall-rule delete` directly, which is idempotent on ARM (deleting a missing rule succeeds), so any real failure fails the job. First confirm locally that deleting a missing rule exits 0.
- **Decision**: FIXED — the cleanup step now runs `az sql server firewall-rule delete` directly, with no `show` pre-check and no `2>/dev/null`. Verified that deleting a missing rule exits 0 (a check against a made-up rule name changed nothing), so any error now fails the job.

### F2 — "Missing connection string → live 200, ready 503" is not tested

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Success Criteria
- **Location**: api/Program.cs:21-32; api.Tests/HealthProbeTests.cs
- **Detail**: The plan's Critical Implementation Details say "the running app must fail readiness, not crash at startup, when it is missing". The design-time half is proven by the CI bundle build. At run time, only an *unreachable* connection string is tested, never an *absent* `ConnectionStrings:AppDb`.
- **Fix**: Add a `HealthProbeTests` case with no `ConnectionStrings:AppDb` that asserts `live` → 200 and `ready` (with the key) → 503.
- **Decision**: FIXED — added `Missing_connection_string_keeps_live_up_and_fails_ready` (`ApiFactory` accepts a null connection string). The suite passes 15/15. Break-check: forcing a startup crash when AppDb is missing turned the test red; `Program.cs` was restored.

### F3 — A runner lost before cleanup would leave its firewall rule behind

- **Severity**: 💡 OBSERVATION
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Safety & Quality
- **Location**: .github/workflows/deploy.yml:110-123, 144-152
- **Detail**: `if: always()` covers failed and cancelled steps. That was shown on run 36137028290, whose migration failed while its rule was still removed. It can't cover a runner that dies before the cleanup step runs. The rule `gh-runner-<run_id>` would then stay, because later runs only target their own `run_id`. No such leak has happened.
- **Fix**: Add an accepted-risk row to the `infrastructure.md` risk register: "Orphaned `gh-runner-*` firewall rule if a runner is lost mid-deploy — L/L — check `az sql server firewall-rule list` occasionally; only `AllowAzureServices` should remain."
- **Decision**: FIXED — added an accepted-risk row to the `infrastructure.md` risk register (orphaned `gh-runner-*` rule if a runner is lost; check the firewall list occasionally).
