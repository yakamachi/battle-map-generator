---
change_id: client-error-reporting
title: Report swallowed fetch errors in the client API wrapper
status: archived
created: 2026-10-11
updated: 2026-10-20
archived_at: 2026-10-20T16:23:49Z
---

## Notes

fix swallowed errors in the web client API wrapper (web/app/api/client.ts and auth.ts): the shared catch-and-map pattern discards the real fetch error on 4 call sites (register, login, logout, generate), returning a generic {kind:"network"} with no logging or reporting. Report the error before mapping it to the UI-safe error kind. Findings L2/G1 from context/audits/observability/2026-10-11_login-generate.md.
