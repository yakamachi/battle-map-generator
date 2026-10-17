---
change_id: testing-abuse-resistant-quota
title: Abuse-resistant quota
status: archived
created: 2026-10-15
updated: 2026-10-17
archived_at: 2026-10-17T08:16:36Z
---

## Notes

Rollout Phase 3 of context/foundation/test-plan.md ("Abuse-resistant quota"): turn "the per-account generation limit protects the free plan" into a measured, tested ceiling. Risk #3: a scripted client creates accounts and generates maps, bypassing the per-account limit, exhausting the free plan's allowance for everyone. Test type: integration test under scripted multi-account load.
