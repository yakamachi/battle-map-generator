# CLAUDE.md

@AGENTS.md

## Commands and gotchas

- A reused local server on 5108 keeps its rate-limit counts between runs, so rapid reruns can meet a 429; stop it or wait a minute.
- `npm test` reuses a server already on 5108 outside CI, so rerun `npm run build:app` after web changes or you test the old build.
