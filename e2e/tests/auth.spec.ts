import { expect, test } from "@playwright/test";

// The whole access flow on the deployed shape: a logged-out visitor sees only login and
// register, registering signs the account in, logging out returns to /login, and logging back in
// reaches the generator. Register and login each spend one call of the shared 10/min-per-IP auth
// budget (see ../AGENTS.md): exactly 2 per browser, on top of setup's up to 2 and
// access.spec.ts's 0. No generate call is made.
test.use({ storageState: { cookies: [], origins: [] } });

test("register, log out and log in again", async ({ page }, testInfo) => {
  const email = `dm-${Date.now()}-${testInfo.project.name}@example.com`;
  const password = "e2e-only-password";

  await page.goto("/");
  await expect(page).toHaveURL(/\/login$/);

  await page.getByRole("link", { name: "Register" }).click();
  await expect(page).toHaveURL(/\/register$/);
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Create account", exact: true }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByText(email)).toBeVisible();

  await page.getByRole("button", { name: "Log out" }).click();
  await expect(page).toHaveURL(/\/login$/);

  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Password").fill(password);
  await page.getByRole("button", { name: "Log in", exact: true }).click();

  await expect(page).toHaveURL(/\/$/);
  await expect(page.getByRole("button", { name: "Generate", exact: true })).toBeVisible();
});
