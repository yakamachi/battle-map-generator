# CLAUDE.md

@AGENTS.md

<!-- BEGIN @przeprogramowani/10x-cli -->

## 10xDevs AI Toolkit - Module 3, Lesson 5 (Debugging)

Turn a failure into a fix the agent can defend: evidence first, then a failing test, then the fix.

```
signal (monitoring | log | flaky E2E | stack trace) -> gather evidence -> reproduce -> failing test -> fix -> verify
```

### Task Router - Where to start

| Skill | Use it when |
| --- | --- |
| `/10x-frame` | The report arrives as "bug + proposed fix". Check that the observed symptom and the stated cause actually match before anyone fixes anything. |
| `/10x-new` -> `/10x-research` -> `/10x-plan` -> `/10x-implement` | The fix is more than a one-liner. Research collects the evidence; the plan starts with the failing regression test. |
| `/10x-tdd` | Writing the regression test that reproduces the bug before the fix. |
| `/10x-e2e` | The bug only shows up in the running app. Reproduce it in the browser and keep that test. |
| `/10x-observability-audit` | Errors are missing, noisy or impossible to diagnose in production. It audits the critical flows for code that hides failures and writes a dated report under `context/audits/observability/`. Use `--verify <report>` to re-check an earlier report after fixes. |

### Hard rules

- **Evidence before a hypothesis.** Use what the error tracker, the logs, a reproduction and the code each show. Never guess a cause from the stack trace alone.
- **The bug becomes a failing test first.** The fix is done when that test goes from red to green and stays in the suite.
- **Never hide the evidence.** No empty `catch`, no ignored promise rejections, no failures turned into redirects or `200`s, no dropped error causes. If the fix needs a `catch`, it logs or reports the error with its cause.
- **Fix the cause, not the test.** Don't edit an assertion to match the new behaviour unless the requirement itself changed.

### Lesson boundaries

- `/10x-observability-audit` reads code and writes a report. It never changes the audited code. Fixing what it finds goes through the change chain.
- Choosing a monitoring vendor is `/10x-infra-research`, not this lesson.

<!-- END @przeprogramowani/10x-cli -->

## Phase gate: review before the next phase

After `/10x-implement` finishes a phase (gates green, phase committed), **stop and run `/10x-impl-review <change-id> phase <N>`** before starting phase N+1, and triage its findings. Never chain one phase's implementation straight into the next, even when the user picks "continue" in the next-phase prompt or asks for several phases in a row; offer the review first and continue only once it is done or the user explicitly skips it for that phase.
