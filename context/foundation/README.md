# Foundation provenance

This repository owns the frontend-specific `tech-stack.md`, selected on 2026-09-17. It selects React Router with TypeScript for `battle-map-generator-web`; it does not replace the .NET selection.

## Shared product snapshots

`prd.md` and `shape-notes.md` are unchanged snapshots copied on 2026-09-17 from `..\10x-project\context\foundation` relative to this repository root. PRD version: 1, created 2026-09-16. Shape notes updated: 2026-09-16. Source files were uncommitted at snapshot time, so SHA-256 hashes identify the exact revisions:

| File | SHA-256 |
| --- | --- |
| prd.md | 3DAC66890702C86F3296CA25F5F03C35B1707F25FF80A68E261D01A5AD671C49 |
| shape-notes.md | 7BA40CEEB1F39ECDD1EC94A3AE580284A14BE1F59881BBAC9769BFE3DF376FE4 |

For product requirement changes, update the canonical documents in `10x-project` first, then refresh both implementation repositories' snapshots and provenance together. Keep repository-specific stack decisions local. Historical warnings in shape notes must be interpreted alongside the newer PRD resolutions.

The backend companion is `battle-map-generator-api`. Repository responsibilities and bootstrap constraints are in the root `AGENTS.md`.
