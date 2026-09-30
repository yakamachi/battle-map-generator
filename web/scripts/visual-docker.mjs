// Runs the screenshot gate in the image CI uses, so local runs and baseline updates match CI
// (fonts and anti-aliasing on other hosts differ). Requires Docker.
// Usage: node scripts/visual-docker.mjs [--update]
//   npm run visual:docker   check against the committed baselines
//   npm run visual:update   rewrite the baselines (explain the visual change in the PR)
//
// - The whole repo is mounted (the specs read ../fixtures/grids/), as the host user, so new
//   baselines and reports are owned by you, not root.
// - web/node_modules is a named volume: the container installs Linux binaries there instead of
//   overwriting the host's node_modules. Docker creates a new volume owned by root, so a
//   one-off root container in the same image chowns it to the host user first (idempotent,
//   and it needs no extra image).
// - HOME and the npm cache point at /tmp, which the host user can write in the container.
import { spawnSync } from "node:child_process";
import { resolve } from "node:path";

const IMAGE = "mcr.microsoft.com/playwright:v1.63.0-noble";
const VOLUME = "bmg-visual-node-modules";
const REPO = resolve(import.meta.dirname, "../..");

const update = process.argv.includes("--update");
const uid = process.getuid?.() ?? 1000;
const gid = process.getgid?.() ?? 1000;

function docker(args) {
  const result = spawnSync("docker", args, { stdio: "inherit" });
  if (result.error) {
    console.error(`visual-docker: could not run docker: ${result.error.message}`);
    process.exit(1);
  }
  return result.status ?? 1;
}

const chown = docker(["run", "--rm", "-v", `${VOLUME}:/nm`, IMAGE, "chown", `${uid}:${gid}`, "/nm"]);
if (chown !== 0) process.exit(chown);

const test = `npx playwright test -c playwright.visual.config.ts${update ? " --update-snapshots" : ""}`;
const status = docker([
  "run",
  "--rm",
  "--ipc=host",
  "--init",
  "--user",
  `${uid}:${gid}`,
  "-e",
  "HOME=/tmp",
  "-e",
  "npm_config_cache=/tmp/.npm",
  "-e",
  "CI=1",
  "-v",
  `${REPO}:/work`,
  "-v",
  `${VOLUME}:/work/web/node_modules`,
  "-w",
  "/work/web",
  IMAGE,
  "bash",
  "-lc",
  `npm ci && npm run build && ${test}`,
]);
process.exit(status);
