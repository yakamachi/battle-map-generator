import { fileURLToPath } from "node:url";
import { expect, test as setup } from "@playwright/test";

// One fixed account for every run; the browser projects start with its session.
const EMAIL = "e2e@example.com";
const PASSWORD = "e2e-only-password";
// Must match `storageState` in playwright.config.ts (resolved from e2e/, whatever the cwd).
const AUTH_FILE = fileURLToPath(new URL("../playwright/.auth/user.json", import.meta.url));

// Register and login share 10 calls per minute per IP: this costs one call, two when the
// account already exists from an earlier run.
setup("log in as the e2e account", async ({ request }) => {
  const credentials = { data: { email: EMAIL, password: PASSWORD } };

  const registered = await request.post("/api/auth/register", credentials);
  if (registered.status() === 400) {
    const loggedIn = await request.post("/api/auth/login", credentials);
    expect(loggedIn.status(), "login of the existing e2e account").toBe(200);
  } else {
    expect(registered.status(), "register of the e2e account").toBe(200);
  }

  const me = await request.get("/api/auth/me");
  expect(me.status()).toBe(200);
  expect(await me.json()).toEqual({ email: EMAIL });

  await request.storageState({ path: AUTH_FILE });
});
