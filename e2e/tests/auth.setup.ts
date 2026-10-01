import { expect, test as setup } from "@playwright/test";
import { AUTH_FILE } from "../local-db";

// One fixed account for every run; the browser projects start with its session.
const EMAIL = "e2e@example.com";
const PASSWORD = "e2e-only-password";

// Register and login share 10 calls per minute per IP: this costs one call, two when the
// account already exists from an earlier run.
setup("log in as the e2e account", async ({ request }) => {
  const credentials = { data: { email: EMAIL, password: PASSWORD } };

  const registered = await request.post("/api/auth/register", credentials);
  if (registered.status() !== 200) {
    // Only "email taken" means the account is left from an earlier run; any other refusal is a
    // real failure, and logging in anyway would only spend auth permits and lockout attempts.
    const body = await registered.text();
    expect(registered.status(), `register of the e2e account: ${body}`).toBe(400);
    expect(body, "register refused the e2e account for a reason other than an existing email").toContain("is already taken");

    const loggedIn = await request.post("/api/auth/login", credentials);
    expect(
      loggedIn.status(),
      `${EMAIL} exists with another password; reset the local database with \`docker compose down -v\` and \`npm run db:up\``,
    ).toBe(200);
  }

  const me = await request.get("/api/auth/me");
  expect(me.status()).toBe(200);
  expect(await me.json()).toEqual({ email: EMAIL });

  await request.storageState({ path: AUTH_FILE });
});
