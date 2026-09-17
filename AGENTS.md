# Battle Map Generator API

## Context

Before scoping features, read `context\foundation\prd.md`. For decision history or unresolved requirements, read `context\foundation\shape-notes.md`. For snapshot provenance and updates, read `context\foundation\README.md`.

Before scaffolding or changing infrastructure, read `context\foundation\tech-stack.md`. This repository currently contains planning inputs only; preserve them when bootstrapping into this repository root.

## Ownership and integration

This backend owns email/password authentication, access enforcement, procedural BSP generation, PNG rendering and the API. The sibling `..\battle-map-generator-web` repository owns the React UI. Both repositories share one product PRD; implement only this repository's responsibilities.

When implementing or changing API endpoints, make backend-generated OpenAPI the contract source and coordinate client changes with the frontend repository. The contract has not been created yet; settle transport and error shapes during implementation rather than inventing independent contracts in each repository.

The selected deployment serves the frontend's compiled SPA through ASP.NET Core on Azure App Service. Preserve API routes when adding SPA fallback routing. Frontend artifact transfer and authentication integration remain implementation work.

## Bootstrap boundary

Run scaffolding only on explicit request. Use the .NET starter here; frontend source belongs in its own repository. Bootstrapper availability must be checked before invocation; the skill was not installed during context preparation.
