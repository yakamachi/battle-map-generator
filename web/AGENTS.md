# Battle Map Generator Web

## Context

Before scoping features, read `context\foundation\prd.md`. For decision history or unresolved requirements, read `context\foundation\shape-notes.md`. For snapshot provenance and updates, read `context\foundation\README.md`.

Before scaffolding or changing infrastructure, read `context\foundation\tech-stack.md`. This repository currently contains planning inputs only; preserve them when bootstrapping into this repository root.

## Ownership and integration

This frontend owns login UI, encounter parameters, generation/regeneration requests, PNG preview and download. The sibling `..\battle-map-generator-api` repository owns authentication enforcement, procedural generation, rendering and the API. Both repositories share one product PRD; implement only this repository's responsibilities.

When implementing API calls, use backend-generated OpenAPI as the contract source. The contract has not been created yet; coordinate endpoint, error and authentication shapes with the backend rather than defining a second independent contract.

Use React Router with TypeScript in SPA mode (`ssr: false`). ASP.NET Core will serve the compiled assets on Azure App Service. The hand-off's `self-host` target means this shared application host, not an additional VPS or Node.js backend. Artifact transfer and authentication integration remain implementation work.

## Bootstrap boundary

Run scaffolding only on explicit request. Apply SPA mode explicitly: the starter defaults to SSR and the hand-off schema has no SPA field. Bootstrapper availability must be checked before invocation; the skill was not installed during context preparation.
