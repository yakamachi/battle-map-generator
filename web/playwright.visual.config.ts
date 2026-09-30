import { defineConfig, devices } from "@playwright/test";

const PORT = 4173;
const BASE_URL = `http://127.0.0.1:${PORT}`;

// Screenshot gate for the home view: every reachable state, in both themes and at one mobile
// width. The API is mocked in each test, so only the built SPA is served (`npm run build` first).
//
// Baselines are generated and checked in mcr.microsoft.com/playwright:v1.63.0-noble, the image
// CI uses: fonts and anti-aliasing on other hosts differ. Update them only with
// `npm run visual:update`, never with a bare --update-snapshots on the host.
export default defineConfig({
  testDir: "visual",
  snapshotPathTemplate: "{testDir}/__screenshots__/{projectName}/{arg}{ext}",
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  expect: {
    // Baselines and CI render in the same image, so the tolerance can be strict. A 1% pixel ratio
    // (~9,200 px at 1280×720) and the default per-pixel threshold (0.2) both let a missing outline
    // border on parchment (~400 px at 1.45:1 contrast) pass unnoticed.
    toHaveScreenshot: { maxDiffPixels: 50, threshold: 0.05 },
  },
  use: {
    baseURL: BASE_URL,
    trace: "retain-on-failure",
  },
  projects: [
    {
      name: "desktop-light",
      use: { ...devices["Desktop Chrome"], viewport: { width: 1280, height: 720 } },
    },
    {
      name: "desktop-dark",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1280, height: 720 },
        colorScheme: "dark",
      },
    },
    {
      // A phone-sized Chromium viewport; no touch emulation, the layout is what is pinned.
      name: "mobile",
      use: { ...devices["Desktop Chrome"], viewport: { width: 390, height: 844 } },
    },
  ],
  webServer: {
    command: `node scripts/serve-spa.mjs ${PORT}`,
    url: BASE_URL,
    reuseExistingServer: !process.env.CI,
    stdout: "pipe",
  },
});
