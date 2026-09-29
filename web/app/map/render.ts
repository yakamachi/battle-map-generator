// ?no-inline keeps the atlas a separate, content-hashed file even if it ever shrinks below
// Vite's inlining limit, so browsers cache it.
import atlasUrl from "./tileset/scribble-atlas.png?no-inline";
import { TILE_SIZE, atlasPosition, drawOps, type MapGrid } from "./tileset";

export const PAPER = "#ffffff";

// A deliberately conservative per-side cap. Browsers allow sides up to about 32767 px but also
// cap the area (Chromium at about 16384² px) and fail silently beyond it; staying at or under
// 16384 per side keeps any map inside that. Larger maps (S-03) need an area check too.
export const MAX_CANVAS_SIDE = 16384;

export async function loadAtlas(): Promise<ImageBitmap> {
  const response = await fetch(atlasUrl);
  if (!response.ok) throw new Error(`Tileset atlas failed to load (${response.status})`);
  // No colour conversion and no premultiplication, so the atlas bytes reach the canvas as-is.
  return createImageBitmap(await response.blob(), {
    colorSpaceConversion: "none",
    premultiplyAlpha: "none",
  });
}

// Renders once at 140 px per square. The preview is this canvas scaled with CSS and the
// download is this canvas's bitmap, so screen size and devicePixelRatio never reach it.
export function renderMap(ctx: CanvasRenderingContext2D, map: MapGrid, atlas: ImageBitmap): void {
  const width = map.width * TILE_SIZE;
  const height = map.height * TILE_SIZE;
  if (width > MAX_CANVAS_SIDE || height > MAX_CANVAS_SIDE) {
    throw new Error(`Map is too large to render: ${width}x${height} px exceeds ${MAX_CANVAS_SIDE} px`);
  }

  // Resizing resets the context state, so smoothing is switched off afterwards.
  ctx.canvas.width = width;
  ctx.canvas.height = height;
  ctx.imageSmoothingEnabled = false;
  ctx.fillStyle = PAPER;
  ctx.fillRect(0, 0, width, height);

  for (const op of drawOps(map)) {
    const source = atlasPosition(op.piece, op.rotation);
    ctx.drawImage(
      atlas,
      source.x,
      source.y,
      TILE_SIZE,
      TILE_SIZE,
      op.cellX * TILE_SIZE,
      op.cellY * TILE_SIZE,
      TILE_SIZE,
      TILE_SIZE,
    );
  }
}
