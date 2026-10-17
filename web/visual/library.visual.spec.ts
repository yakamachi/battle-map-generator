import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { type Page, type Route } from "@playwright/test";
import { test, expect } from "./fixtures";

// Screenshot baselines for the saved-map library: the Save dialog on home, My maps (with maps,
// empty, and the delete confirmation) and one saved map (drawn, and not found). The API is
// mocked, so these pin the SPA only; the real lifecycle against the .NET host is in ../../e2e/.
// The list shows dates in the viewer's locale and time zone, so both are pinned here.
test.use({ locale: "en-US", timezoneId: "UTC" });

const GENERATE = "**/api/maps/generate";
const LIBRARY = "**/api/maps";
const SEED_42 = JSON.parse(
  readFileSync(resolve(import.meta.dirname, "../../fixtures/grids/seed-42.json"), "utf8"),
);
// Atlas load and a 4200×2800 draw on a cold CI runner can outlast the 5 s default.
const RENDER_TIMEOUT_MS = 20_000;

const GOBLIN_WARREN_ID = "0b6f4a52-3c1e-4d8a-9f10-000000000001";

const SUMMARIES = [
  {
    id: GOBLIN_WARREN_ID,
    title: "Goblin warren",
    seed: 42,
    width: 30,
    height: 20,
    parameters: { roomCount: 6, encounter: "skirmish", bossSize: null },
    createdAt: "2026-10-16T12:00:00Z",
    updatedAt: "2026-10-16T12:00:00Z",
  },
  {
    id: "0b6f4a52-3c1e-4d8a-9f10-000000000002",
    title: "Dragon lair beneath the old mill",
    seed: 7,
    width: 54,
    height: 30,
    parameters: { roomCount: 12, encounter: "boss", bossSize: "huge" },
    createdAt: "2026-10-12T12:00:00Z",
    updatedAt: "2026-10-14T12:00:00Z",
  },
  {
    // 80 characters, the longest title the API accepts.
    id: "0b6f4a52-3c1e-4d8a-9f10-000000000003",
    title: "The Sunken Cathedral of the Drowned King: crypts, bell tower and the flood halls",
    seed: 20260925,
    width: 40,
    height: 24,
    parameters: { roomCount: 8, encounter: "skirmish", bossSize: null },
    createdAt: "2026-09-30T12:00:00Z",
    updatedAt: "2026-09-30T12:00:00Z",
  },
];

const GOBLIN_WARREN = {
  id: GOBLIN_WARREN_ID,
  title: "Goblin warren",
  createdAt: "2026-10-16T12:00:00Z",
  updatedAt: "2026-10-16T12:00:00Z",
  map: SEED_42,
};

function json(route: Route, body: unknown, status = 200) {
  return route.fulfill({ status, contentType: "application/json", body: JSON.stringify(body) });
}

// The mobile shots take the full page, so content below the 844 px fold is pinned too.
async function shot(page: Page, name: string) {
  await page.evaluate(() => document.fonts.ready.then(() => undefined));
  await expect(page).toHaveScreenshot(`${name}.png`, {
    animations: "disabled",
    caret: "hide",
    fullPage: test.info().project.name === "mobile",
  });
}

async function openList(page: Page, maps: unknown[]) {
  await page.route(LIBRARY, (route) => json(route, maps));
  await page.goto("/maps");
  await expect(page.getByRole("heading", { name: "My maps" })).toBeVisible();
}

// The Save request itself (seed and parameters from the drawn map) is covered end to end in
// ../../e2e/; a second map render here would only add load to the gate.
test("save-dialog", async ({ page }) => {
  await page.route(GENERATE, (route) => json(route, SEED_42));
  await page.goto("/");
  await page.getByRole("button", { name: "Generate", exact: true }).click();
  const save = page.getByRole("button", { name: "Save", exact: true });
  await expect(save).toBeEnabled({ timeout: RENDER_TIMEOUT_MS });
  await save.click();
  const dialog = page.getByRole("dialog", { name: "Save map" });
  await expect(dialog.getByLabel("Title")).toBeFocused();
  await dialog.getByLabel("Title").fill("Goblin warren");
  await page.mouse.move(0, 0);
  await shot(page, "save-dialog");

  // Escape cancels and returns focus to Save.
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(save).toBeFocused();
});

test("library-list", async ({ page }) => {
  await openList(page, SUMMARIES);
  await expect(page.getByRole("listitem")).toHaveCount(3);
  await expect(page.getByText("30×20 · Skirmish · Saved Oct 16, 2026")).toBeVisible();
  await shot(page, "library-list");
});

test("library-empty", async ({ page }) => {
  await openList(page, []);
  await expect(page.getByRole("link", { name: "Back to the generator" })).toBeVisible();
  await shot(page, "library-empty");
});

test("library-delete-confirm", async ({ page }) => {
  await openList(page, SUMMARIES);
  const trigger = page.getByRole("button", { name: "Delete Goblin warren" });
  await trigger.click();
  const dialog = page.getByRole("dialog", { name: "Delete map?" });
  await expect(dialog).toContainText("“Goblin warren” will be deleted");
  await page.mouse.move(0, 0);
  await shot(page, "library-delete-confirm");

  // Cancel keeps the map and returns focus to its Delete button.
  await dialog.getByRole("button", { name: "Cancel" }).click();
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
  await expect(page.getByRole("listitem")).toHaveCount(3);
});

test("saved-map", async ({ page }) => {
  await page.route(`**/api/maps/${GOBLIN_WARREN_ID}`, (route) => json(route, GOBLIN_WARREN));
  await page.goto(`/maps/${GOBLIN_WARREN_ID}`);
  await expect(page.getByRole("button", { name: "Download PNG" })).toBeEnabled({
    timeout: RENDER_TIMEOUT_MS,
  });
  await expect(page.getByRole("heading", { name: "Goblin warren" })).toBeVisible();
  await expect(page.getByText("Seed: 42", { exact: true })).toBeVisible();
  await shot(page, "saved-map");
});

test("saved-map-not-found", async ({ page }) => {
  await page.route(`**/api/maps/${GOBLIN_WARREN_ID}`, (route) => route.fulfill({ status: 404 }));
  await page.goto(`/maps/${GOBLIN_WARREN_ID}`);
  await expect(page.getByRole("heading", { name: "Map not found" })).toBeVisible();
  await expect(page.getByRole("link", { name: "Back to My maps" })).toBeVisible();
  await shot(page, "saved-map-not-found");
});
