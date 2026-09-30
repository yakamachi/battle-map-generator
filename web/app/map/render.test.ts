import { beforeAll, describe, expect, test } from "vitest";
import { server } from "vitest/browser";
import { MAX_CANVAS_SIDE, loadAtlas, renderMap } from "./render";
import { TILE_SIZE, type MapGrid } from "./tileset";

type Baselines = Record<string, Partial<Record<string, string>>>;

declare module "vitest/browser" {
  interface BrowserCommands {
    renderBaseline: (
      fixture: string,
      browser: string,
      hash: string,
    ) => Promise<{ updated: boolean; baselines: Baselines }>;
  }
}

const fixtures = import.meta.glob<MapGrid>("../../../fixtures/grids/*.json", {
  eager: true,
  import: "default",
});

function fixtureName(path: string): string {
  return path.slice(path.lastIndexOf("/") + 1).replace(/\.json$/, "");
}

async function sha256(bytes: Uint8ClampedArray): Promise<string> {
  const digest = await crypto.subtle.digest("SHA-256", new Uint8Array(bytes));
  return [...new Uint8Array(digest)].map((b) => b.toString(16).padStart(2, "0")).join("");
}

let atlas: ImageBitmap;
beforeAll(async () => {
  atlas = await loadAtlas();
});

describe("renderMap", () => {
  test.each(Object.entries(fixtures).map(([path, map]) => [fixtureName(path), map] as const))(
    "%s renders the pinned image",
    async (name, map) => {
      const canvas = document.createElement("canvas");
      const ctx = canvas.getContext("2d");
      if (!ctx) throw new Error("no 2d context");

      renderMap(ctx, map, atlas);

      expect(canvas.width).toBe(map.width * TILE_SIZE);
      expect(canvas.height).toBe(map.height * TILE_SIZE);

      const pixels = ctx.getImageData(0, 0, canvas.width, canvas.height).data;
      let inked = 0;
      for (let i = 0; i < pixels.length; i += 4) {
        if (pixels[i] !== 255 || pixels[i + 1] !== 255 || pixels[i + 2] !== 255) inked++;
      }
      expect(inked).toBeGreaterThan(0);

      const hash = await sha256(pixels);
      const browser = server.browser;
      const { updated, baselines } = await server.commands.renderBaseline(name, browser, hash);
      // The command logs whether the other browser's baseline matches; that is not asserted.
      if (!updated) {
        expect(hash, `baseline for ${name} in ${browser}; UPDATE_BASELINES=1 rewrites it`).toBe(
          baselines[name]?.[browser],
        );
      }
    },
  );

  test("refuses a canvas side beyond the browser limit", () => {
    const ctx = document.createElement("canvas").getContext("2d");
    if (!ctx) throw new Error("no 2d context");
    const huge: MapGrid = { seed: 1, width: 118, height: 1, cells: Array(118).fill("void") };
    expect(() => renderMap(ctx, huge, atlas)).toThrow(/too large/);
  });

  test("refuses a canvas area beyond the limit even when each side fits", () => {
    const ctx = document.createElement("canvas").getContext("2d");
    if (!ctx) throw new Error("no 2d context");
    // 117 squares is 16380 px, inside the side cap; the area is 268 M pixels.
    const wide: MapGrid = { seed: 1, width: 117, height: 117, cells: Array(117 * 117).fill("void") };
    expect(117 * TILE_SIZE).toBeLessThanOrEqual(MAX_CANVAS_SIDE);
    expect(() => renderMap(ctx, wide, atlas)).toThrow(/area/);
  });

  test("renders the largest S-03 map (54×30 squares, 7560×4200 px)", () => {
    const ctx = document.createElement("canvas").getContext("2d");
    if (!ctx) throw new Error("no 2d context");
    const largest: MapGrid = { seed: 1, width: 54, height: 30, cells: Array(54 * 30).fill("void") };
    renderMap(ctx, largest, atlas);
    expect(ctx.canvas.width).toBe(7560);
    expect(ctx.canvas.height).toBe(4200);
  });
});
