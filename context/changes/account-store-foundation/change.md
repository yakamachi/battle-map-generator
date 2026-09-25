---
change_id: account-store-foundation
title: Cloud account store and persistent sessions
status: impl_reviewed
created: 2026-09-23
updated: 2026-09-25
archived_at: null
---

## Notes

<!-- Free-form notes for this change: links, ad-hoc context, decisions that don't belong in research/frame/plan. -->

Phase 3 outcome (2026-09-25):

- SQL server `sql-battlemap-2a28f4` (Entra-only, admin = owner guest account). Database `sqldb-battlemap` on the free offer (serverless GP_S_Gen5, auto-pause 60 min, AutoPause at the limit), in polandcentral.
- The `Microsoft.Sql` resource provider was `NotRegistered` on the subscription, so the plan's `az sql` commands failed until `az provider register --namespace Microsoft.Sql` was run first. Minor adaptation.
- App system-assigned identity principal `1fb03bc7-6505-4795-87c6-2625fe59d102`. Database users: `battle-map-generator` (datareader, datawriter) and `id-battlemap-deploy` (ddladmin, datareader, datawriter).
- Custom role `SQL Firewall Rule Operator`, defined at `rg-battlemap` and assigned to `id-battlemap-deploy` on the SQL server only.
- Firewall: only `AllowAzureServices`.
