import { readFile } from "node:fs/promises";
import { expect, test } from "@playwright/test";

// 8 rooms with a Huge boss: the 8-room size (34×24) widened by the arena's 10 squares, at 140 px each.
const WIDTH = 44 * 140;
const HEIGHT = 24 * 140;
// Generate, atlas load and a 6160×3360 draw on a cold CI runner can outlast the 5 s default.
const RENDER_TIMEOUT_MS = 20_000;

type GeneratedMap = {
  seed: number;
  parameters: { roomCount: number; encounter: string; bossSize: string | null };
  width: number;
  height: number;
  rooms: { kind: string }[];
};

// One generate call per browser (2 per run; with map.spec.ts 6 of the 10 allowed per minute).
test("generate a boss map with non-default parameters and download it", async ({ page }, testInfo) => {
  await page.goto("/");
  await page.getByLabel("Rooms").selectOption("8");
  await page.getByLabel("Encounter").selectOption("Boss fight");
  await page.getByLabel("Boss size").selectOption("Huge");

  // The real API's answer, observed on its way to the page (never mocked here).
  const response = page.waitForResponse("**/api/maps/generate");
  await page.getByRole("button", { name: "Generate" }).click();
  const map = (await (await response).json()) as GeneratedMap;

  expect(map.parameters).toEqual({ roomCount: 8, encounter: "boss", bossSize: "huge" });
  expect(map.rooms).toHaveLength(8);
  expect(map.rooms.filter((room) => room.kind === "bossArena")).toHaveLength(1);
  expect([map.width, map.height]).toEqual([44, 24]);

  const download = page.getByRole("button", { name: "Download PNG" });
  await expect(download).toBeEnabled({ timeout: RENDER_TIMEOUT_MS });
  const canvas = await page.locator("canvas").evaluate((element: HTMLCanvasElement) => {
    const ctx = element.getContext("2d");
    if (!ctx) throw new Error("No 2D context");
    const pixels = new Uint32Array(ctx.getImageData(0, 0, element.width, element.height).data.buffer);
    const distinct = new Set<number>();
    // A prime stride samples every row and column phase without walking all 20.7 M pixels.
    for (let i = 0; i < pixels.length; i += 997) distinct.add(pixels[i]);
    return { width: element.width, height: element.height, distinct: distinct.size };
  });
  expect([canvas.width, canvas.height]).toEqual([WIDTH, HEIGHT]);
  // Attributes alone hold for a blank canvas too; a drawn map has ink on paper.
  expect(canvas.distinct).toBeGreaterThan(1);

  // A blank white PNG of the same size, encoded by this browser, as the floor for the download.
  const blankPngBytes = await page.evaluate(
    ([width, height]) => {
      const blank = document.createElement("canvas");
      blank.width = width;
      blank.height = height;
      const ctx = blank.getContext("2d")!;
      ctx.fillStyle = "#ffffff";
      ctx.fillRect(0, 0, width, height);
      return new Promise<number>((resolve) => blank.toBlob((blob) => resolve(blob!.size), "image/png"));
    },
    [WIDTH, HEIGHT],
  );

  const downloadEvent = page.waitForEvent("download");
  await download.click();
  const file = await downloadEvent;
  expect(file.suggestedFilename()).toBe(`battle-map-${map.seed}.png`);
  const path = testInfo.outputPath(file.suggestedFilename());
  await file.saveAs(path);
  const png = await readFile(path);
  expect(png.toString("ascii", 12, 16)).toBe("IHDR");
  expect(png.readUInt32BE(16)).toBe(WIDTH);
  expect(png.readUInt32BE(20)).toBe(HEIGHT);
  // A drawn map compresses far worse than blank paper (about 3-4 times the size at 4200×2800).
  expect(png.length).toBeGreaterThan(2 * blankPngBytes);
});
