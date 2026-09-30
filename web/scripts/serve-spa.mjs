// Serves the built SPA (build/client) for the visual tests, without .NET or new dependencies.
// Usage: node scripts/serve-spa.mjs <port>
//
// Unknown extensionless paths fall back to index.html, as the ASP.NET Core host does; a missing
// file path (one with an extension, such as a lost /assets chunk) returns 404, as it does there.
// /api/* always returns 404 (the tests mock it with page.route), so a missing mock fails loudly
// instead of loading the SPA shell as JSON.
import { createReadStream, existsSync } from "node:fs";
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

if (!existsSync(INDEX)) {
  console.error("serve-spa: build/client/index.html is missing — run npm run build");
  process.exit(1);
}

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

// Headers go out only once the file is open, so a read error before that becomes a 500; one
// mid-body destroys the response, so a truncated file is never taken as complete.
function send(res, status, path) {
  const stream = createReadStream(path);
  stream.on("open", () => {
    res.writeHead(status, {
      "Content-Type": TYPES[extname(path).toLowerCase()] ?? "application/octet-stream",
    });
    stream.pipe(res);
  });
  stream.on("error", (error) => {
    console.error(`serve-spa: cannot read ${path}: ${error.message}`);
    if (res.headersSent) res.destroy(error);
    else res.writeHead(500, { "Content-Type": "text/plain; charset=utf-8" }).end("Internal error");
  });
}

function notFound(res) {
  res.writeHead(404, { "Content-Type": "text/plain; charset=utf-8" }).end("Not found");
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
    notFound(res);
    return;
  }

  let file = null;
  try {
    file = await fileAt(pathname);
  } catch {
    // A malformed escape in the path: treat it as unknown.
  }
  if (file) send(res, 200, file);
  else if (extname(pathname)) notFound(res);
  else send(res, 200, INDEX);
});

server.listen(port, "127.0.0.1", () => {
  console.log(`Serving ${ROOT} on http://127.0.0.1:${port}`);
});
