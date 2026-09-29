import { defineConfig, devices } from "@playwright/test";

const BASE_URL = "http://localhost:5108";

// End-to-end tests against the real .NET host serving the built SPA from api/wwwroot
// (run `npm run e2e:prepare` first). Both browsers are required by the PRD.
//
// The generate endpoint allows 10 calls per minute per IP, and both projects hit the same
// server: keep the whole run well under that (today one call per browser). A reused local
// server keeps its counts between runs, so rapid reruns can meet a 429.
export default defineConfig({
  testDir: "e2e",
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: BASE_URL,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "chromium", use: { ...devices["Desktop Chrome"] } },
    { name: "firefox", use: { ...devices["Desktop Firefox"] } },
  ],
  webServer: {
    // No launch profile and the Production environment, so a local run matches CI: no
    // appsettings.Development.json and no database (startup and generation never touch it).
    command: `dotnet run --project ../api --no-launch-profile --urls ${BASE_URL}`,
    env: { ASPNETCORE_ENVIRONMENT: "Production" },
    url: `${BASE_URL}/api/health/live`,
    reuseExistingServer: !process.env.CI,
    // dotnet run builds the API first, which takes a while on a cold CI runner.
    timeout: 180_000,
    stdout: "pipe",
  },
});
