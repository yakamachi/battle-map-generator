import { readFileSync, writeFileSync } from "node:fs";
import { resolve } from "node:path";
import { playwright } from "@vitest/browser-playwright";
import { defineConfig } from "vitest/config";
import type { BrowserCommand } from "vitest/node";

// Standalone on purpose: the reactRouter() plugin in vite.config.ts does not run under Vitest.

const BASELINES = resolve(import.meta.dirname, "app/map/render-baselines.json");
const BROWSERS = ["chromium", "firefox"] as const;
type Baselines = Record<string, Partial<Record<(typeof BROWSERS)[number], string>>>;

// The page cannot write files, so render.test.ts reports hashes through this command, which
// runs in Node. Synchronous I/O keeps the parallel browser instances from interleaving writes.
const renderBaseline: BrowserCommand<[fixture: string, browser: string, hash: string]> = (
  _context,
  fixture,
  browser,
  hash,
) => {
  if (!/^[\w-]+$/.test(fixture)) throw new Error(`Bad fixture name: ${fixture}`);
  if (!BROWSERS.includes(browser as (typeof BROWSERS)[number])) {
    throw new Error(`Unknown browser: ${browser}`);
  }
  const key = browser as (typeof BROWSERS)[number];
  let baselines: Baselines = {};
  try {
    baselines = JSON.parse(readFileSync(BASELINES, "utf8")) as Baselines;
  } catch {
    // No file yet: the first UPDATE_BASELINES run creates it.
  }

  // Engines may round alpha blending differently, so cross-browser equality is only reported.
  const other = baselines[fixture]?.[key === "chromium" ? "firefox" : "chromium"];
  if (other) {
    console.log(`render ${fixture}: ${key} ${other === hash ? "matches" : "differs from"} the other browser`);
  }

  if (process.env.UPDATE_BASELINES !== "1") {
    return { updated: false, baselines };
  }

  baselines[fixture] = { ...baselines[fixture], [key]: hash };
  const sorted: Baselines = {};
  for (const name of Object.keys(baselines).sort()) {
    const entry = baselines[name];
    sorted[name] = Object.fromEntries(BROWSERS.flatMap((b) => (entry[b] ? [[b, entry[b]]] : [])));
  }
  writeFileSync(BASELINES, `${JSON.stringify(sorted, null, 2)}\n`);
  return { updated: true, baselines: sorted };
};

export default defineConfig({
  resolve: {
    tsconfigPaths: true,
  },
  server: {
    // The shared fixture grids live in the repo root, outside the Vite root. Only that folder is
    // opened (an explicit list replaces Vite's default, so web/ itself is listed too).
    fs: { allow: [".", "../fixtures"] },
  },
  test: {
    include: ["app/**/*.test.ts"],
    browser: {
      enabled: true,
      headless: true,
      provider: playwright(),
      instances: [{ browser: "chromium" }, { browser: "firefox" }],
      commands: { renderBaseline },
    },
  },
});
