# Battle Map Generator — Web

Frontend for the battle map generator, built with [React Router](https://reactrouter.com/) in SPA mode (`ssr: false`). It compiles to static files that are served by the ASP.NET Core backend in `../api/` of this monorepo on Azure App Service.

## Stack

- React Router (SPA mode) + React
- TypeScript
- Vite
- Tailwind CSS

## Getting Started

Install the dependencies:

```bash
npm install
```

Start the development server with HMR:

```bash
npm run dev
```

The app is available at `http://localhost:5173`.

Type-check the project:

```bash
npm run typecheck
```

## Building for Production

```bash
npm run build
```

The deployable output is `build/client/`:

```
build/client/
├── index.html   # SPA entry point
├── assets/      # Hashed JS/CSS bundles
└── favicon.ico
```

`build/server/` is only used by the build itself and is not deployed.

## Deployment

The frontend is not deployed on its own. It is hosted by the ASP.NET Core API as static files:

1. CI builds this app (`npm ci && npm run build` in `web/`).
2. The contents of `web/build/client/` are copied into the published `wwwroot/` of `api/`.
3. The backend is deployed to Azure App Service.

The backend serves the SPA with `UseDefaultFiles()` / `UseStaticFiles()` and falls back to `index.html` for non-API routes (`MapFallbackToFile("index.html")`), so client-side routes work on page refresh. API endpoints live under `/api` on the same origin, so the frontend calls relative URLs and needs no CORS configuration in production.

Caching: files in `assets/` are content-hashed and can be cached long-term; `index.html` should be served with `Cache-Control: no-cache` so new deployments are picked up.

## Styling

[Tailwind CSS](https://tailwindcss.com/) is configured via `@tailwindcss/vite`.
