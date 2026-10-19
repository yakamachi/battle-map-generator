# Archive SHA repoint

## 2026-10-19

- **Target**: `origin/main` (remote default branch, `git ls-remote --symref origin HEAD`), snapshot `4e5b36af6288c61d37334045c5f360e73dad71b2`
- **Integration commit**: `4e5b36a` "Regenerate via dice states on the Generate button (S-02) (#25)"
- **Association**: PR #25, https://github.com/yakamachi/battle-map-generator/pull/25, state MERGED into `main`. Its commit list is `09a1096`, `8326442`, `0013153`, `0587d29`, `1042ab3`, squash-merged as `4e5b36a`.
- **Evidence and scope**:
  - `09a1096` and `0013153` are not ancestors of the target (`git merge-base --is-ancestor` exited 1, full history, not shallow).
  - `git diff 1042ab3 4e5b36a` is empty, so the squash tree is byte-identical to the PR head, which contains both phases' implementation.
- **Decision**: the user chose **Update and archive** in `/10x-archive`.

| Row ID | Old suffix (resolved OID) | New SHA |
|---|---|---|
| 1.1–1.10 | `09a1096` (feat(regenerate-with-new-seed): Button states, dice and flash (p1)) | `4e5b36a` |
| 2.1–2.5 | `0013153` (feat(regenerate-with-new-seed): Gates and docs (p2)) | `4e5b36a` |

Affected rows: 15.
