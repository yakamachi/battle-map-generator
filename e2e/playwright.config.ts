import { defineConfig, devices } from "@playwright/test";
import { AUTH_FILE, LOCAL_DB_CONNECTION_STRING } from "./local-db";

const BASE_URL = "http://localhost:5108";

// End-to-end tests against the real .NET host serving the built SPA from api/wwwroot
// (run `npm run build:app` first) and the SQL Server from ../compose.yaml (run `npm run db:up`
// first). Both browsers are required by the PRD. The setup project logs in once and both
// browser projects start with its saved session.
//
// The generate endpoint allows 10 calls per minute per account, and both projects use the one
// e2e account: keep the whole run well under that (today 6 generations: 4 in map.spec.ts,
// 2 in parameters.spec.ts). Register and login share 10 calls per minute per IP, and a full run
// uses at most 6. A reused local server keeps its counts between runs, so rapid reruns can meet
// a 429. Locally a server already on 5108 is reused as it is: stop any API there that was not
// started against the compose database.
export default defineConfig({
  testDir: "tests",
  forbidOnly: !!process.env.CI,
  retries: 0,
  reporter: [["list"], ["html", { open: "never" }]],
  use: {
    baseURL: BASE_URL,
    trace: "retain-on-failure",
  },
  projects: [
    { name: "setup", testMatch: /.*\.setup\.ts/ },
    {
      name: "chromium",
      use: { ...devices["Desktop Chrome"], storageState: AUTH_FILE },
      dependencies: ["setup"],
    },
    {
      name: "firefox",
      use: { ...devices["Desktop Firefox"], storageState: AUTH_FILE },
      dependencies: ["setup"],
    },
  ],
  webServer: {
    // No launch profile and the Production environment, so a local run matches CI: no
    // appsettings.Development.json, and the database is the compose SQL Server named here.
    command: `dotnet run --project ../api --no-launch-profile --urls ${BASE_URL}`,
    env: { ASPNETCORE_ENVIRONMENT: "Production", ConnectionStrings__AppDb: LOCAL_DB_CONNECTION_STRING },
    url: `${BASE_URL}/api/health/live`,
    reuseExistingServer: !process.env.CI,
    // dotnet run builds the API first, which takes a while on a cold CI runner.
    timeout: 180_000,
    stdout: "pipe",
  },
});
