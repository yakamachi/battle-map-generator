import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { type Page, type Route } from "@playwright/test";
import { test, expect } from "./fixtures";

// Screenshot baselines for every reachable state of the home view. The API is mocked here, so
// these pin the SPA only; the real flow against the .NET host is tested in ../../e2e/. The home
// view now sits behind routes/protected.tsx, which the shared mockAuthMe fixture answers with a
// fixed account (./fixtures.ts) for every test here, same as the hermetic-fonts fixture it shares
// with visual/auth.visual.spec.ts.

const GENERATE = "**/api/maps/generate";
const SEED_42 = readFileSync(resolve(import.meta.dirname, "../../fixtures/grids/seed-42.json"), "utf8");
const SEED_42_BOSS_HUGE = readFileSync(
  resolve(import.meta.dirname, "../../fixtures/grids/seed-42-boss-huge.json"),
  "utf8",
);
// Atlas load and a 4200×2800 draw on a cold CI runner can outlast the 5 s default.
const RENDER_TIMEOUT_MS = 20_000;

function fulfilMap(route: Route, body: string) {
  return route.fulfill({ status: 200, contentType: "application/json", body });
}

// Leaves the request unanswered, so the view stays in its loading state. It must not await
// anything or call fulfill/continue; the test ends with unrouteAll({ behavior: "ignoreErrors" }).
function hold() {}

// The mobile shots take the full page, so content below the 844 px fold is pinned too.
async function shot(page: Page, name: string) {
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await expect(page).toHaveScreenshot(`${name}.png`, {
    animations: "disabled",
    caret: "hide",
    fullPage: test.info().project.name === "mobile",
  });
}

async function openHome(page: Page) {
  await page.goto("/");
  await expect(page.getByRole("button", { name: "Generate", exact: true })).toBeEnabled();
}

// Clicks Generate, then moves the pointer off the buttons so no hover style lands in the shot.
async function clickGenerate(page: Page) {
  await page.getByRole("button", { name: "Generate", exact: true }).click();
  await page.mouse.move(0, 0);
}

async function waitForReady(page: Page) {
  await expect(page.getByRole("button", { name: "Download PNG" })).toBeEnabled({
    timeout: RENDER_TIMEOUT_MS,
  });
}

async function waitForLoading(page: Page) {
  await expect(page.getByRole("button", { name: "Generating…" })).toBeDisabled();
}

test("idle", async ({ page }) => {
  await openHome(page);
  await expect(page.getByRole("button", { name: "Download PNG" })).toBeDisabled();
  await shot(page, "idle");
});

test.describe("pointer and keyboard", () => {
  test.beforeEach(({}, testInfo) => {
    test.skip(
      testInfo.project.name === "mobile",
      "No pointer hover or keyboard on the mobile viewport",
    );
  });

  test("idle-focus", async ({ page }) => {
    await openHome(page);
    // The protected layout's header (Log out) comes first, then Rooms and Encounter.
    for (let i = 0; i < 4; i++) await page.keyboard.press("Tab");
    await expect(page.getByRole("button", { name: "Generate", exact: true })).toBeFocused();
    await shot(page, "idle-focus");
  });

  test("idle-hover", async ({ page }) => {
    await openHome(page);
    await page.getByRole("button", { name: "Generate", exact: true }).hover();
    await shot(page, "idle-hover");
  });
});

test("loading-first", async ({ page }) => {
  await page.route(GENERATE, hold);
  await openHome(page);
  await clickGenerate(page);
  await waitForLoading(page);
  await shot(page, "loading-first");
  await page.unrouteAll({ behavior: "ignoreErrors" });
});

test("ready", async ({ page }) => {
  await page.route(GENERATE, (route) => fulfilMap(route, SEED_42));
  await openHome(page);
  await clickGenerate(page);
  await waitForReady(page);
  await expect(page.getByText("Seed: 42")).toBeVisible();
  await shot(page, "ready");
});

test("loading-regenerate", async ({ page }) => {
  // The first map is drawn; the second request is held, so the first stays on screen, dimmed.
  let calls = 0;
  await page.route(GENERATE, (route) => {
    calls += 1;
    if (calls === 1) return fulfilMap(route, SEED_42);
    hold();
  });
  await openHome(page);
  await clickGenerate(page);
  await waitForReady(page);
  await clickGenerate(page);
  await waitForLoading(page);
  await expect(page.getByRole("button", { name: "Download PNG" })).toBeDisabled();
  await shot(page, "loading-regenerate");
  await page.unrouteAll({ behavior: "ignoreErrors" });
});

test("error-rate-limited", async ({ page }) => {
  await page.route(GENERATE, (route) =>
    route.fulfill({ status: 429, contentType: "text/plain", body: "Too Many Requests" }),
  );
  await openHome(page);
  await clickGenerate(page);
  await expect(page.getByRole("alert")).toHaveText(
    "Too many maps in a short time. Please wait a minute and try again.",
  );
  await shot(page, "error-rate-limited");
});

test("boss-form", async ({ page }) => {
  await openHome(page);
  await expect(page.getByLabel("Boss size")).toHaveCount(0);
  await page.getByLabel("Encounter").selectOption("Boss fight");
  await expect(page.getByLabel("Boss size")).toHaveValue("large");
  await shot(page, "boss-form");
});

test("ready-boss", async ({ page }) => {
  let body: unknown;
  await page.route(GENERATE, (route) => {
    body = route.request().postDataJSON();
    return fulfilMap(route, SEED_42_BOSS_HUGE);
  });
  await openHome(page);
  await page.getByLabel("Encounter").selectOption("Boss fight");
  await page.getByLabel("Boss size").selectOption("Huge");
  await clickGenerate(page);
  await waitForReady(page);
  await expect(page.getByText("Seed: 42")).toBeVisible();
  expect(body).toEqual({ seed: null, roomCount: 6, encounter: "boss", bossSize: "huge" });
  await shot(page, "ready-boss");
});

test("not-found", async ({ page }) => {
  await page.goto("/no-such-page");
  await expect(page.getByRole("heading", { name: "404" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Back to the generator" })).toBeVisible();
  await shot(page, "not-found");
});
