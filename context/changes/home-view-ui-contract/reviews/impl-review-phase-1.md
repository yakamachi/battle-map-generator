<!-- IMPL-REVIEW-REPORT -->
# Implementation Review: Home View UI Contract

- **Plan**: context/changes/home-view-ui-contract/plan.md
- **Scope**: Phase 1 of 4 (commit 876dbbc)
- **Reviewed phases**: 1
- **Date**: 2026-09-29
- **Verdict**: APPROVED
- **Findings**: 0 critical, 1 warning, 0 observations

Plan drift: every Phase 1 item matches. Details:
- **Dependencies:** `cn` 0.4.0 and `class-variance-authority` 0.7.1, plus one transitive dependency, `clsx` 2.1.1. No forbidden packages.
- **`components.json` and `lib/utils.ts`:** as planned.
- **`app.css`:** all 17 tokens are set in `:root` and the dark media query, at the plan's values. `@theme inline`, the radius scale and the base layer are there. There is no `@custom-variant`, and the body literals are gone.
- **`tokens.md`:** the contrast ratios were recomputed independently and match exactly.
- **Extra files:** none outside the expected change-folder bootstrap.

The built CSS was inspected:
- `:root` and `@media (prefers-color-scheme: dark)` are emitted.
- The `*` rule gives `border-color: var(--border)` and a `color-mix` 50% ring outline.
- `dark:` utilities still compile to the media query.
- The canvas bitmap is unaffected.

The automated criteria passed on this tree just before the commit:
- typecheck and build
- `npm test` 36/36 (Chromium and Firefox)
- e2e 2/2 (Chromium and Firefox)
- the `app.css` grep

The manual criteria 1.5 and 1.6 were confirmed by the user.

## Verdicts

| Dimension | Verdict |
|-----------|---------|
| Plan Adherence | PASS |
| Scope Discipline | PASS |
| Safety & Quality | PASS |
| Architecture | PASS |
| Pattern Consistency | WARNING |
| Success Criteria | PASS |

## Findings

### F1 — `shadcn add` can quietly undo the OS-driven dark mode and the token set

- **Severity**: ⚠️ WARNING
- **Impact**: 🏃 LOW — quick decision; fix is obvious and narrowly scoped
- **Dimension**: Pattern Consistency
- **Location**: web/components.json:3-4, web/app/app.css:14-54
- **Detail**:
  - With `tailwind.css: "app/app.css"` and `cssVariables: true`, the shadcn CLI may write into `app.css` when it adds a component that needs extra variables (Select, Dropdown and Popover need `--popover`; others need `--chart-*` and `--sidebar-*`).
  - It may also add a `.dark {}` block and `@custom-variant dark (&:is(.dark *))` with its neutral values.
  - The planned `ui:scan` (Phase 4) doesn't read `app.css`, and the `@custom-variant` grep exists only as a Phase 1 criterion. So the first S-03/S-04 `shadcn add` could silently switch dark mode to a class nothing sets. Every `dark:` refinement in `ui/` primitives would stop working, and neutral values would appear next to the parchment tokens.
- **Fix**: In Phase 4:
  - Extend `ui:scan` to fail on `@custom-variant` or a `.dark` selector in `app/app.css`.
  - Add to the `## UI` rule: "after `shadcn add`, review the `app/app.css` diff: remove any `.dark` block or `@custom-variant`, and add new variables (e.g. `--popover`) by hand in both `:root` and the dark media block, recorded in `tokens.md`."
- **Decision**: FIXED — Phase 4 amended: `ui:scan` token-source scope (`@custom-variant`/`.dark` in app.css), post-`shadcn add` rule line, new criterion and Progress row 4.8
