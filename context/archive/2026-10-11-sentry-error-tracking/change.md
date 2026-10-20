---
change_id: sentry-error-tracking
title: Install Sentry error tracking in api and web
status: archived
created: 2026-10-11
updated: 2026-10-20
archived_at: 2026-10-20T16:23:49Z
---

## Notes

install the Sentry SDK in both apps (Sentry.AspNetCore in api/Program.cs, a browser SDK in web/) so production errors actually reach a tracker. Precondition for observability audit finding P1 (context/audits/observability/2026-10-11_login-generate.md) and the prerequisite the client-error-reporting change deferred. User already created a Sentry project; DSN must be stored as config/app settings/env var, never hardcoded in source.
