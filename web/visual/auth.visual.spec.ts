import { test, expect } from "./fixtures";
import type { Page } from "@playwright/test";

// Screenshot baselines for the login and register pages. The API is mocked here, so these pin
// the SPA only; the real flow against the .NET host is tested in ../../e2e/. /api/auth/me answers
// 401 for every test (a logged-out visitor), so neither clientLoader redirects to "/".
test.use({ account: null });

async function shot(page: Page, name: string) {
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await expect(page).toHaveScreenshot(`${name}.png`, {
    animations: "disabled",
    caret: "hide",
    fullPage: test.info().project.name === "mobile",
  });
}

async function openLogin(page: Page) {
  await page.goto("/login");
  await expect(page.getByRole("button", { name: "Log in", exact: true })).toBeEnabled();
}

async function openRegister(page: Page) {
  await page.goto("/register");
  await expect(page.getByRole("button", { name: "Create account", exact: true })).toBeEnabled();
}

test("login-idle", async ({ page }) => {
  await openLogin(page);
  await shot(page, "login-idle");
});

test("login-error-invalid-credentials", async ({ page }) => {
  await page.route("**/api/auth/login", (route) => route.fulfill({ status: 401 }));
  await openLogin(page);
  await page.getByLabel("Email").fill("dm@example.com");
  await page.getByLabel("Password").fill("wrong-password");
  await page.getByRole("button", { name: "Log in", exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText("Incorrect email or password.");
  await shot(page, "login-error-invalid-credentials");
});

test("register-idle", async ({ page }) => {
  await openRegister(page);
  await shot(page, "register-idle");
});

test("register-error-validation", async ({ page }) => {
  await page.route("**/api/auth/register", (route) =>
    route.fulfill({
      status: 400,
      contentType: "application/problem+json",
      body: JSON.stringify({
        errors: { password: ["Passwords must be at least 8 characters."] },
      }),
    }),
  );
  await openRegister(page);
  await page.getByLabel("Email").fill("dm@example.com");
  await page.getByLabel("Password").fill("short1");
  await page.getByRole("button", { name: "Create account", exact: true }).click();
  await expect(page.getByRole("alert")).toHaveText("Passwords must be at least 8 characters.");
  await shot(page, "register-error-validation");
});
