import { readFile } from "node:fs/promises";
import { expect, test, type Page, type TestInfo } from "@playwright/test";

// 30×20 squares at 140 px each.
const WIDTH = 4200;
const HEIGHT = 2800;
// A blank white canvas of this size encodes to about 230 KB in Chromium and 50 KB in Firefox;
// a drawn map is about 700–950 KB (measured 2026-09-29).
const BLANK_PNG_MAX_BYTES = 400_000;
// Generate, atlas load and a 4200×2800 draw on a cold CI runner can outlast the 5 s default.
const RENDER_TIMEOUT_MS = 20_000;

// SHA-256 of the canvas bitmap, or of a PNG decoded the same way, as hex. Runs in the page.
async function pixelHash(page: Page, png?: string): Promise<string> {
  return page.evaluate(async (base64) => {
    let data: Uint8ClampedArray<ArrayBuffer>;
    if (base64 === undefined) {
      const canvas = document.querySelector("canvas");
      if (!canvas) throw new Error("No canvas");
      data = canvas.getContext("2d")!.getImageData(0, 0, canvas.width, canvas.height).data;
    } else {
      const bytes = Uint8Array.from(atob(base64), (c) => c.charCodeAt(0));
      const bitmap = await createImageBitmap(new Blob([bytes], { type: "image/png" }), {
        colorSpaceConversion: "none",
        premultiplyAlpha: "none",
      });
      const decoded = new OffscreenCanvas(bitmap.width, bitmap.height).getContext("2d")!;
      decoded.drawImage(bitmap, 0, 0);
      data = decoded.getImageData(0, 0, bitmap.width, bitmap.height).data;
    }
    const digest = await crypto.subtle.digest("SHA-256", data);
    return Array.from(new Uint8Array(digest), (b) => b.toString(16).padStart(2, "0")).join("");
  }, png);
}

// Generates a map, checks the preview and the downloaded PNG, and returns the preview's hash.
async function generateAndDownload(page: Page, testInfo: TestInfo): Promise<string> {
  await page.getByRole("button", { name: "Generate" }).click();

  // The button is enabled only once the map is drawn, after the atlas has loaded.
  const download = page.getByRole("button", { name: "Download PNG" });
  await expect(download).toBeEnabled({ timeout: RENDER_TIMEOUT_MS });

  const canvas = page.locator("canvas");
  await expect(canvas).toBeVisible();
  const bitmap = await canvas.evaluate((element: HTMLCanvasElement) => {
    const ctx = element.getContext("2d");
    if (!ctx) throw new Error("No 2D context");
    const pixels = new Uint32Array(ctx.getImageData(0, 0, element.width, element.height).data.buffer);
    const distinct = new Set<number>();
    // A prime stride samples every row and column phase without walking all 11.8 M pixels.
    for (let i = 0; i < pixels.length; i += 997) distinct.add(pixels[i]);
    return { width: element.width, height: element.height, distinct: distinct.size };
  });
  expect(bitmap.width).toBe(WIDTH);
  expect(bitmap.height).toBe(HEIGHT);
  expect(bitmap.distinct).toBeGreaterThan(1);

  const seedText = await page.getByText(/^Seed: \d+$/).textContent();
  const seed = seedText!.replace("Seed: ", "");

  const downloadEvent = page.waitForEvent("download");
  await download.click();
  const file = await downloadEvent;
  expect(file.suggestedFilename()).toBe(`battle-map-${seed}.png`);

  const path = testInfo.outputPath(file.suggestedFilename());
  await file.saveAs(path);
  const png = await readFile(path);
  expect(png.subarray(0, 8)).toEqual(Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]));
  expect(png.toString("ascii", 12, 16)).toBe("IHDR");
  expect(png.readUInt32BE(16)).toBe(WIDTH);
  expect(png.readUInt32BE(20)).toBe(HEIGHT);
  expect(png.length).toBeGreaterThan(BLANK_PNG_MAX_BYTES);

  // The file holds exactly the pixels the preview shows (FR-006).
  const previewHash = await pixelHash(page);
  expect(await pixelHash(page, png.toString("base64"))).toBe(previewHash);
  return previewHash;
}

// Two maps per browser: 4 generate calls per run, within the 10-per-minute limit.
test("generate, preview and download a map, then a second one", async ({ page }, testInfo) => {
  await page.goto("/");
  const first = await generateAndDownload(page, testInfo);
  // A second Generate must not leave the first map's pixels on screen or in the download.
  const second = await generateAndDownload(page, testInfo);
  expect(second).not.toBe(first);
});
