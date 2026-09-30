# Archive SHA repoint: home-view-ui-contract

- **Date**: 2026-09-30
- **Target**: `origin/main`, snapshot `dcb83c91b31580019dab585a21ebab8e05bd3274`
- **Integration commit**: `dcb83c9` "Home view UI contract: tokens, components, preview states and a visual gate (#9)"
- **Association**: PR https://github.com/yakamachi/battle-map-generator/pull/9 (state MERGED, base `main`, merge commit `dcb83c9`). Its commit list includes all four old SHAs.
- **Evidence**:
  - `git merge-base --is-ancestor` returned 1 for each old SHA against the target, so none of them is in `main`'s history.
  - The PR was squash-merged, so its phase commits collapsed into `dcb83c9`, and the PR diff covers all four phases.
- **Decision**: the user chose "Update and archive" on 2026-09-30.

| Row IDs | Old suffix (resolved OID) | New SHA |
|---|---|---|
| 1.1–1.6 | `06b97cf` (06b97cfc80e15b6870a3fc684198a5fb0385761e) | `dcb83c9` |
| 2.1–2.6 | `8672ddb` (8672ddb98664c869ad3c6fdaab113448e327ca14) | `dcb83c9` |
| 3.1–3.8 | `8336eea` (8336eeaa724ee30f07467e41b5440f1a19127435) | `dcb83c9` |
| 4.1–4.8 | `5de944b` (5de944ba4beec984bf8f472403367cfc05343a15) | `dcb83c9` |

**Total**: 28 rows.
