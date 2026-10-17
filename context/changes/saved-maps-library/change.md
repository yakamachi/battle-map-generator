---
change_id: saved-maps-library
title: Saved maps library (save, list, rename, delete a DM's maps)
status: impl_reviewed
created: 2026-10-17
updated: 2026-10-18
archived_at: null
---

## Notes

Closes the 10xBuilder certification gap: the requirements ask for CRUD on a domain entity in a way that fits the domain, and today maps are only generated and downloaded, never stored. The PRD Non-Goals currently defer "Zapis mapy w bibliotece z tytułem" to stage 2, so this change pulls that forward and the PRD and roadmap need updating (new slice).

Seed scope: a logged-in DM saves the current map with a title, sees a list of their own saved maps, opens one (grid rendered as before), renames it and deletes it. Store the grid, not only the seed (root AGENTS.md: a seed is only stable while the algorithm is unchanged). Only the owner may see or change their maps (PRD NFR: account data invisible to other accounts). Target: certification deadline 1, 2026-11-04.
