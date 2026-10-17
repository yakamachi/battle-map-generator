import { expect, test } from "@playwright/test";

// Generate, atlas load and a 4200×2800 draw on a cold CI runner can outlast the 5 s default.
const RENDER_TIMEOUT_MS = 20_000;

// The saved-map library end to end: save a generated map, find it in My maps, open it, rename it
// and delete it. Both browsers share the one e2e account and may run at the same time, so every
// step finds the map by this run's unique title and never counts the list. One generate call per
// browser (2 per run; with map.spec.ts and parameters.spec.ts 8 of the 10 allowed per minute).
// The spec deletes what it saves; a run interrupted mid-test leaves at most one uniquely titled row.
test("save, list, open, rename and delete a map", async ({ page }, testInfo) => {
  const title = `${testInfo.project.name} ${Date.now()}`;
  const renamed = `${title} renamed`;

  // 1. Generate a map and save it under the unique title.
  await page.goto("/");
  await page.getByRole("button", { name: "Generate" }).click();
  await expect(page.getByRole("button", { name: "Download PNG" })).toBeEnabled({ timeout: RENDER_TIMEOUT_MS });
  const seedText = await page.getByText(/^Seed: \d+$/).textContent();

  await page.getByRole("button", { name: "Save", exact: true }).click();
  const saveDialog = page.getByRole("dialog");
  await saveDialog.getByLabel("Title").fill(title);
  await saveDialog.getByRole("button", { name: "Save", exact: true }).click();
  await expect(saveDialog.getByRole("heading", { name: "Map saved", exact: true })).toBeVisible();
  await saveDialog.getByRole("button", { name: "Close" }).click();
  await expect(saveDialog).toBeHidden();
  // Home now shows the server's regeneration of the same seed.
  await expect(page.getByText(seedText!, { exact: true })).toBeVisible({ timeout: RENDER_TIMEOUT_MS });

  // 2. The map is listed in My maps.
  await page.getByRole("link", { name: "My maps" }).click();
  await expect(page.getByRole("heading", { name: "My maps", level: 1 })).toBeVisible();
  await expect(page.getByRole("heading", { name: title, exact: true })).toBeVisible();

  // 3. Opening it shows the title and the seed it was generated with.
  await page.getByRole("link", { name: `Open ${title}`, exact: true }).click();
  await expect(page).toHaveURL(/\/maps\/[^/]+$/);
  const savedMapUrl = page.url();
  await expect(page.getByRole("heading", { name: title, exact: true, level: 1 })).toBeVisible();
  await expect(page.getByText(seedText!, { exact: true })).toBeVisible({ timeout: RENDER_TIMEOUT_MS });

  // 4. Renaming it shows the new title in the list.
  await page.getByRole("link", { name: "My maps" }).click();
  await page.getByRole("button", { name: `Rename ${title}`, exact: true }).click();
  const renameDialog = page.getByRole("dialog");
  await expect(renameDialog.getByLabel("Title")).toHaveValue(title);
  await renameDialog.getByLabel("Title").fill(renamed);
  await renameDialog.getByRole("button", { name: "Rename", exact: true }).click();
  // The list behind an open dialog is hidden from the accessibility tree, so wait for it to close.
  await expect(renameDialog).toBeHidden();
  await expect(page.getByRole("heading", { name: renamed, exact: true })).toBeVisible();
  await expect(page.getByRole("heading", { name: title, exact: true })).toHaveCount(0);

  // 5. Deleting it, once confirmed, removes it from the list.
  await page.getByRole("button", { name: `Delete ${renamed}`, exact: true }).click();
  const deleteDialog = page.getByRole("dialog");
  await expect(deleteDialog.getByRole("heading", { name: "Delete map?", exact: true })).toBeVisible();
  await deleteDialog.getByRole("button", { name: "Delete", exact: true }).click();
  await expect(deleteDialog).toBeHidden();
  await expect(page.getByRole("heading", { name: renamed, exact: true })).toHaveCount(0);

  // The list above is the page's own state; prove the server deleted the map too. A fresh load of
  // My maps no longer lists it once the server's list has rendered (the count line, or the empty
  // state when the other browser has nothing saved), and its old address is not found.
  await page.reload();
  await expect(page.getByText(/ of 100 saved maps|You have no saved maps yet/)).toBeVisible();
  await expect(page.getByRole("heading", { name: renamed, exact: true })).toHaveCount(0);

  await page.goto(savedMapUrl);
  await expect(page.getByRole("heading", { name: "Map not found", exact: true, level: 1 })).toBeVisible();
});
