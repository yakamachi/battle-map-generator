// Builds the SPA in ../web and copies it into the API's wwwroot, so `dotnet run` serves the same
// app the deploy ships. api/wwwroot is gitignored.
import { execSync } from "node:child_process";
import { cpSync, rmSync } from "node:fs";
import { fileURLToPath } from "node:url";

const root = fileURLToPath(new URL("../../", import.meta.url));
const client = `${root}web/build/client`;
const wwwroot = `${root}api/wwwroot`;

execSync("npm run build", { cwd: `${root}web`, stdio: "inherit" });
rmSync(wwwroot, { recursive: true, force: true });
cpSync(client, wwwroot, { recursive: true });
console.log(`Copied ${client} to ${wwwroot}`);
