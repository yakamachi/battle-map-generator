# Lessons Learned

> Append-only register of recurring rules and patterns. Re-read at start by /10x-frame, /10x-research, /10x-plan, /10x-plan-review, /10x-implement, /10x-impl-review.

## Take the GitHub OIDC subject from GitHub, never hand-build it

- **Context**: Any Azure federated identity credential for GitHub Actions (new environment such as staging, new or re-created repo, new deploy identity).
- **Problem**: New GitHub repos present an immutable-ID subject (`repo:owner@<ownerId>/repo@<repoId>:environment:<env>`); a hand-built `repo:owner/repo:...` subject fails login with AADSTS700213. It happened on the first deploy (2026-09-22).
- **Rule**: Before creating a federated credential, read the IDs with `gh api repos/<owner>/<repo> --jq '"\(.owner.id) \(.id)"'` (or copy the subject from a failed `azure/login` log) and use the ID form. Re-create the credential whenever the repo is deleted and re-created.
- **Applies to**: plan, implement, impl-review

## Check Azure billing state at billing-account scope

- **Context**: Any check or change of Azure budgets, credits, spending limits or subscription offer.
- **Problem**: Budgets and credits can live on the billing account rather than the subscription; `az consumption budget list` on the subscription returned empty while a budget with an email alert existed, which led to a false "no budget alert" claim.
- **Rule**: Never report billing guardrails as missing from a subscription-scoped query alone; check the billing account (portal Cost Management or `az rest` on Microsoft.Billing) or ask the owner before concluding.
- **Applies to**: research, plan-review, impl-review

## Keep course and local AI tooling out of the public repo

- **Context**: Pulling lesson packs (`10x get`), adding skills or prompts, and every commit in this public monorepo.
- **Problem**: Lesson packs write `.claude/` (skills, prompts, manifest) and `.10x-cli.json`; `git add -A` would publish course material in the public repo.
- **Rule**: Keep `/.claude/` and `/.10x-cli.json` in the root `.gitignore` and never force-add them; after a lesson pull, run `git status` before staging and confirm no tooling files appear.
- **Applies to**: implement, impl-review

## Unknown /api routes return 404, never the SPA shell

- **Context**: `api/Program.cs` routing, and every new endpoint or route group under `/api` while the API also serves the SPA from `wwwroot`.
- **Problem**: `MapFallbackToFile("index.html")` answers every unmatched path, so a typo or a removed `/api` route returns `200 text/html` instead of `404`; the client then fails with a JSON parse error rather than a clear not-found, and a missing endpoint looks healthy.
- **Rule**: Keep `app.MapFallback("/api/{**rest}", () => Results.NotFound())` before the SPA fallback and keep every API route under `/api`; any change to routing or static files must confirm that an unknown `/api/*` path returns 404 and a deep link returns `index.html`.
- **Applies to**: plan, implement, impl-review
