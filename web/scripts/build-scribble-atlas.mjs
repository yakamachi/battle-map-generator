// Builds the Scribble Dungeons atlas: every piece resampled once to exactly 140 px and
// pre-rotated, so the renderer only copies atlas cells 1:1 with smoothing off.
// Output is committed and must be byte-identical on every run.
import { readFile, writeFile } from "node:fs/promises";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";
import sharp from "sharp";

const TILE = 140;
const COLUMNS = 6;

const webRoot = join(dirname(fileURLToPath(import.meta.url)), "..");
const sourceDir = join(webRoot, "tileset-src", "scribble");
const outDir = join(webRoot, "app", "map", "tileset");

// Floor pieces are symmetric enough to use as shipped; edge pieces need every rotation.
const pieces = [
  { name: "tiles", rotations: [0] },
  { name: "tiles_cracked", rotations: [0] },
  { name: "tiles_decorative", rotations: [0] },
  { name: "tile", rotations: [0] },
  { name: "wall", rotations: [0, 90, 180, 270] },
  { name: "wall_corner", rotations: [0, 90, 180, 270] },
  { name: "wall_edge", rotations: [0, 90, 180, 270] },
  { name: "inner_round", rotations: [0, 90, 180, 270] },
  { name: "door_closed", rotations: [0, 90, 180, 270] },
];

// SIMD paths are chosen per CPU; the scalar path keeps the resampled bytes machine-independent.
sharp.simd(false);
sharp.cache(false);

const cells = pieces.flatMap((p) => p.rotations.map((rotation) => ({ name: p.name, rotation })));
const rows = Math.ceil(cells.length / COLUMNS);
const width = COLUMNS * TILE;
const height = rows * TILE;
const atlas = Buffer.alloc(width * height * 4);
const index = {};

const resized = new Map();
for (const { name } of pieces) {
  const input = await readFile(join(sourceDir, `${name}.png`));
  // Resize first, rotate after: quarter turns of the 140 px piece are exact pixel moves,
  // so all four rotations share one resampling.
  resized.set(
    name,
    await sharp(input)
      .ensureAlpha()
      .resize(TILE, TILE, { kernel: sharp.kernel.lanczos3, fit: "fill" })
      .png()
      .toBuffer(),
  );
}

for (const [i, { name, rotation }] of cells.entries()) {
  const x = (i % COLUMNS) * TILE;
  const y = Math.floor(i / COLUMNS) * TILE;
  const { data, info } = await sharp(resized.get(name))
    .rotate(rotation)
    .ensureAlpha()
    .raw()
    .toBuffer({ resolveWithObject: true });
  if (info.width !== TILE || info.height !== TILE || info.channels !== 4) {
    throw new Error(`${name}@${rotation}: unexpected ${info.width}x${info.height}x${info.channels}`);
  }
  for (let row = 0; row < TILE; row++) {
    data.copy(atlas, ((y + row) * width + x) * 4, row * TILE * 4, (row + 1) * TILE * 4);
  }
  index[`${name}@${rotation}`] = { x, y };
}

const png = await sharp(atlas, { raw: { width, height, channels: 4 } })
  .png({ compressionLevel: 9, adaptiveFiltering: false, palette: false })
  .toBuffer();

await writeFile(join(outDir, "scribble-atlas.png"), stripColourChunks(png));
await writeFile(join(outDir, "scribble-atlas.json"), `${JSON.stringify(index, null, 2)}\n`);
console.log(`atlas ${width}x${height}, ${cells.length} pieces`);

// Browsers colour-manage images that carry colour metadata, which would change pixels per
// display and per engine. Keep only the chunks needed to decode the image.
function stripColourChunks(buffer) {
  const keep = new Set(["IHDR", "PLTE", "tRNS", "IDAT", "IEND"]);
  const signature = buffer.subarray(0, 8);
  const out = [signature];
  let offset = 8;
  while (offset < buffer.length) {
    const length = buffer.readUInt32BE(offset);
    const type = buffer.toString("latin1", offset + 4, offset + 8);
    const end = offset + 12 + length;
    if (keep.has(type)) out.push(buffer.subarray(offset, end));
    offset = end;
  }
  return Buffer.concat(out);
}
