// Starts the SQL Server from compose.yaml, waits until it accepts logins, and applies the
// migrations, so the host that e2e starts has a schema to work with. Safe to rerun.
import { execFileSync } from "node:child_process";
import { fileURLToPath } from "node:url";
import { LOCAL_DB_CONNECTION_STRING } from "../local-db.ts";

const root = fileURLToPath(new URL("../../", import.meta.url));
const run = (command, args) => execFileSync(command, args, { cwd: root, stdio: "inherit" });

run("docker", ["compose", "up", "-d", "--wait"]);
run("dotnet", ["tool", "restore"]);
// ef does not restore, and on a fresh CI runner nothing has restored the API yet (as in deploy.yml).
run("dotnet", ["restore", "api"]);
run("dotnet", ["ef", "database", "update", "--project", "api", "--connection", LOCAL_DB_CONNECTION_STRING]);
