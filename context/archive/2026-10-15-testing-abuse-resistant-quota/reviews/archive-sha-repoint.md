# Archive SHA repoint

## 2026-10-17

- **Target**: `origin/main` @ `b5f7fcfa78c8780fcc03644fbac9b9cac27fccbd` (resolved via `git ls-remote --symref origin HEAD`, refreshed with `git fetch`)
- **Integration commit**: `b5f7fcf` "Abuse-resistant quota: 50/min global ceiling on generate and auth (test-plan Phase 3) (#20)"
- **PR**: https://github.com/yakamachi/battle-map-generator/pull/20 (MERGED, squash, base `main`)
- **Evidence**:
  - The original branch commits were cherry-picked onto `test/abuse-resistant-quota` before the PR was opened. Each pair has the same `git patch-id --stable`:
    - `c5944fc` ≡ `1fac383` (`19349bbb1871`)
    - `e8efda2` ≡ `a5e004e` (`95ca40ca6897`)
  - Both `1fac383` and `a5e004e` are in PR #20's commit list.
  - The PR head `c7fdb69` has a tree identical to `b5f7fcf`.
  - `b5f7fcf` is an ancestor of the target snapshot (exit 0).
- **Scope**: all implementation for Phases 1 and 2 (api/Program.cs, MapEndpoints/AuthEndpoints constants, api.Tests seam + AbuseResistantQuotaTests, docs).
- **Decision**: user chose "Update and archive".

| Row | Old suffix (resolved OID) | New SHA |
|---|---|---|
| 1.1 | c5944fc (c5944fcd0ca0746dc51d723404ac62bb1ac7c25b) | b5f7fcf |
| 1.2 | c5944fc (c5944fcd0ca0746dc51d723404ac62bb1ac7c25b) | b5f7fcf |
| 1.3 | c5944fc (c5944fcd0ca0746dc51d723404ac62bb1ac7c25b) | b5f7fcf |
| 2.1 | e8efda2 (e8efda2b2840ebdf5b001da7c993c64f98a5055e) | b5f7fcf |
| 2.2 | e8efda2 (e8efda2b2840ebdf5b001da7c993c64f98a5055e) | b5f7fcf |

Rows repointed: 5.
