# Archive SHA repoint: saved-maps-library

## 2026-10-18

- **Target**: `origin/main` (base of PR #23, resolved with `git ls-remote --symref origin HEAD` and refreshed with `git fetch --no-tags origin +refs/heads/main:refs/remotes/origin/main`), snapshot `8d8ae1373a58a68d5ec71921b874c70acef96ab6`.
- **Integration commit**: `8d8ae13` "Saved maps library: save, list, open, rename and delete a DM's maps (S-05) (#23)", the squash merge of https://github.com/yakamachi/battle-map-generator/pull/23.
- **Evidence**:
  - All five old SHAs resolve to commits in PR #23's commit list.
  - None of them is an ancestor of the target snapshot (`git merge-base --is-ancestor` exit 1). The repository is not shallow.
  - `8d8ae13` is an ancestor of the target (exit 0).
  - Its tree is identical to the PR head `c5efa29` (`git diff --quiet c5efa29 8d8ae13`).
  - Its diff against the previous `main` (`f48a702`) is the whole branch: 109 files, +7463 −319 (API, migration, web UI, e2e, docs).
- **Decision**: the user chose "Update and archive" on 2026-10-18.

| Row ID | Old suffix (resolved OID) | New SHA |
|--------|---------------------------|---------|
| 1.1–1.5 | 3e9afdf "feat(saved-maps-library): API and contract (p1)" | 8d8ae13 |
| 2.1–2.4 | 161484e "feat(saved-maps-library): compact storage and review fixes (p2)" | 8d8ae13 |
| 3.1–3.8 | 396515c "feat(saved-maps-library): web UI (p3)" | 8d8ae13 |
| 4.1–4.4 | aeeb753 "test(saved-maps-library): end-to-end library lifecycle (p4)" | 8d8ae13 |
| 5.1–5.3 | de54b5e "docs(saved-maps-library): foundation docs bring the library into scope (p5)" | 8d8ae13 |

Rows repointed: 24.
