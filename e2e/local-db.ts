import { fileURLToPath } from "node:url";

// The SQL Server from ../compose.yaml, as ../api/appsettings.Development.json names it. Shared by
// scripts/db-up.mjs (migrations) and playwright.config.ts (the host's connection string).
export const LOCAL_DB_CONNECTION_STRING =
  "Server=localhost,1433;Database=battlemap;User Id=sa;Password=LocalDev_Only_Passw0rd!;TrustServerCertificate=True";

// The session tests/auth.setup.ts saves and both browser projects load; gitignored. Resolved from
// this file, so it does not depend on the directory Playwright is started from.
export const AUTH_FILE = fileURLToPath(new URL("./playwright/.auth/user.json", import.meta.url));
