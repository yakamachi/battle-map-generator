// Serves the built SPA (build/client) for the visual tests, without .NET or new dependencies.
// Usage: node scripts/serve-spa.mjs <port>
//
// Unknown paths fall back to index.html, as the ASP.NET Core host does. /api/* always returns
// 404 (the tests mock it with page.route), so a missing mock fails loudly instead of loading
// the SPA shell as JSON.
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { createServer } from "node:http";
import { extname, join, normalize, resolve, sep } from "node:path";

const port = Number(process.argv[2] ?? 4173);
if (!Number.isInteger(port) || port <= 0) {
  console.error(`Usage: node scripts/serve-spa.mjs <port> (got ${process.argv[2]})`);
  process.exit(1);
}

const ROOT = resolve(import.meta.dirname, "../build/client");
const INDEX = join(ROOT, "index.html");

const TYPES = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".map": "application/json; charset=utf-8",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".webp": "image/webp",
  ".svg": "image/svg+xml",
  ".ico": "image/x-icon",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".txt": "text/plain; charset=utf-8",
};

async function fileAt(pathname) {
  const path = normalize(join(ROOT, decodeURIComponent(pathname)));
  // Never serve anything outside build/client.
  if (path !== ROOT && !path.startsWith(ROOT + sep)) return null;
  try {
    const info = await stat(path);
    return info.isFile() ? path : null;
  } catch {
    return null;
  }
}

function send(res, status, path) {
  res.writeHead(status, {
    "Content-Type": TYPES[extname(path).toLowerCase()] ?? "application/octet-stream",
  });
  createReadStream(path).pipe(res);
}

const server = createServer(async (req, res) => {
  let pathname;
  try {
    pathname = new URL(req.url ?? "/", "http://localhost").pathname;
  } catch {
    res.writeHead(400).end();
    return;
  }

  if (pathname === "/api" || pathname.startsWith("/api/")) {
    res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" }).end("Not found");
    return;
  }

  let file = null;
  try {
    file = await fileAt(pathname);
  } catch {
    // A malformed escape in the path: treat it as unknown.
  }
  send(res, 200, file ?? INDEX);
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Serving ${ROOT} on http://127.0.0.1:${port}`);
});
