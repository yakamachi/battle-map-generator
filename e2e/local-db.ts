// The SQL Server from ../compose.yaml, as ../api/appsettings.Development.json names it. Shared by
// scripts/db-up.mjs (migrations) and playwright.config.ts (the host's connection string).
export const LOCAL_DB_CONNECTION_STRING =
  "Server=localhost,1433;Database=battlemap;User Id=sa;Password=LocalDev_Only_Passw0rd!;TrustServerCertificate=True";
