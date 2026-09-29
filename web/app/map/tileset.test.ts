import { describe, expect, test } from "vitest";
import type { CellKind } from "../api/client";
import { drawOps, type DrawOp, type MapGrid } from "./tileset";

const KINDS: Record<string, CellKind> = {
  " ": "void",
  "#": "wall",
  ".": "floor",
  ",": "corridor",
  D: "door",
};

// Hand-drawn grids: '#' wall, ' ' void, '.' floor, ',' corridor, 'D' door.
function grid(rows: string[], seed = 7): MapGrid {
  const width = rows[0].length;
  const cells = rows.flatMap((row) => {
    if (row.length !== width) throw new Error("ragged grid");
    return [...row].map((ch) => KINDS[ch]);
  });
  return { seed, width, height: rows.length, cells };
}

function opsAt(ops: DrawOp[], x: number, y: number): string[] {
  return ops
    .filter((op) => op.cellX === x && op.cellY === y)
    .map((op) => `${op.piece}@${op.rotation}`);
}

const fixtures = import.meta.glob<MapGrid>("../../../fixtures/grids/*.json", {
  eager: true,
  import: "default",
});

describe("drawOps", () => {
  test("door bar faces the room, along its wall line", () => {
    const vertical = grid([
      "#####",
      "#...#",
      "##D##",
      " #,# ",
      "##D##",
      "#...#",
      "#####",
    ]);
    const ops = drawOps(vertical);
    expect(opsAt(ops, 2, 2)).toContain("door_closed@0");
    expect(opsAt(ops, 2, 4)).toContain("door_closed@180");

    const horizontal = grid([
      "#########",
      "#..D,D..#",
      "#########",
    ]);
    const hops = drawOps(horizontal);
    expect(opsAt(hops, 3, 1)).toContain("door_closed@270");
    expect(opsAt(hops, 5, 1)).toContain("door_closed@90");
  });

  test("a door lies on a plain tile, with wall strips on its closed sides", () => {
    const ops = drawOps(grid(["#####", "#..D,", "#####"]));
    expect(opsAt(ops, 3, 1)).toEqual(["tile@0", "wall@0", "wall@180", "door_closed@270"]);
  });

  test("every side facing rock or the map edge gets a wall", () => {
    const ops = drawOps(
      grid([
        "#####",
        "#...#",
        "#...#",
        "#...#",
        "#####",
      ]),
    );
    const walls = (x: number, y: number) => opsAt(ops, x, y).filter((p) => !p.startsWith("tiles"));
    expect(walls(1, 1)).toEqual(["wall_corner@0"]);
    expect(walls(2, 1)).toEqual(["wall@0"]);
    expect(walls(3, 1)).toEqual(["wall_corner@90"]);
    expect(walls(3, 2)).toEqual(["wall@90"]);
    expect(walls(3, 3)).toEqual(["wall_corner@180"]);
    expect(walls(2, 3)).toEqual(["wall@180"]);
    expect(walls(1, 3)).toEqual(["wall_corner@270"]);
    expect(walls(1, 2)).toEqual(["wall@270"]);
    expect(walls(2, 2)).toEqual([]);

    // Off-map counts as closed, and void counts like wall.
    const edge = drawOps(grid([".. ", "..."]));
    expect(opsAt(edge, 1, 0).filter((p) => !p.startsWith("tiles"))).toEqual(["wall_corner@90"]);
    expect(opsAt(edge, 1, 1).filter((p) => !p.startsWith("tiles"))).toEqual([
      "wall@180",
      "inner_round@90",
    ]);
  });

  test("opposite closed sides get two strips; corridors bend with rounded corners", () => {
    const ops = drawOps(
      grid([
        "#####",
        "#,,,#",
        "###,#",
        "#####",
      ]),
    );
    expect(opsAt(ops, 2, 1)).toEqual(["tile@0", "wall@0", "wall@180"]);
    expect(opsAt(ops, 3, 1)).toEqual(["tile@0", "wall_edge@90", "inner_round@270"]);
    // A dead end (three closed sides) is two corners sharing the closed end.
    expect(opsAt(ops, 1, 1)).toEqual(["tile@0", "wall_edge@0", "wall_edge@270"]);
    expect(opsAt(ops, 3, 2)).toEqual(["tile@0", "wall_edge@180", "wall_edge@270"]);
  });

  test("inner corners where two open sides meet rock on the diagonal", () => {
    const ops = drawOps(
      grid([
        "####",
        "#..#",
        "#.##",
        "####",
      ]),
    );
    expect(opsAt(ops, 1, 1).filter((p) => !p.startsWith("tiles"))).toEqual([
      "wall_corner@0",
      "inner_round@180",
    ]);

    const cross = drawOps(grid(["#.#", "...", "#.#"]));
    expect(opsAt(cross, 1, 1).filter((p) => !p.startsWith("tiles"))).toEqual([
      "inner_round@0",
      "inner_round@90",
      "inner_round@180",
      "inner_round@270",
    ]);
  });

  test("a walkable cell closed on all four sides does not crash", () => {
    const ops = drawOps(grid(["###", "#.#", "###"]));
    expect(opsAt(ops, 1, 1).slice(1)).toEqual([
      "wall_corner@0",
      "wall_corner@90",
      "wall_corner@180",
      "wall_corner@270",
    ]);
  });

  test("rock and void get no ops; floors come first, then walls, then doors", () => {
    const map = grid(["#####", "#..D,", "#####"]);
    const ops = drawOps(map);
    for (const op of ops) {
      expect(map.cells[op.cellY * map.width + op.cellX]).not.toBe("wall");
    }
    const layer = (op: DrawOp) =>
      op.piece.startsWith("tile") ? 0 : op.piece === "door_closed" ? 2 : 1;
    const layers = ops.map(layer);
    expect(layers).toEqual([...layers].sort((a, b) => a - b));
  });

  test.each(Object.entries(fixtures))("%s: ops sit on integer cells inside the map", (_, map) => {
    const ops = drawOps(map);
    expect(ops.length).toBeGreaterThan(0);
    for (const op of ops) {
      expect(Number.isInteger(op.cellX) && Number.isInteger(op.cellY)).toBe(true);
      expect(op.cellX).toBeGreaterThanOrEqual(0);
      expect(op.cellY).toBeGreaterThanOrEqual(0);
      expect(op.cellX).toBeLessThan(map.width);
      expect(op.cellY).toBeLessThan(map.height);
      expect([0, 90, 180, 270]).toContain(op.rotation);
    }
  });

  test.each(Object.entries(fixtures))("%s: the same map always gives the same ops", (_, map) => {
    const copy: MapGrid = { ...map, cells: [...map.cells] };
    expect(drawOps(copy)).toEqual(drawOps(map));
  });

  test("floor variants follow the seed, not the call", () => {
    const rows = Array.from({ length: 12 }, () => ".".repeat(12));
    const pieces = (seed: number) =>
      drawOps(grid(rows, seed))
        .filter((op) => op.piece.startsWith("tiles"))
        .map((op) => op.piece);
    expect(pieces(1)).toEqual(pieces(1));
    expect(pieces(1)).not.toEqual(pieces(2));
    expect(new Set(pieces(1))).toEqual(new Set(["tiles", "tiles_cracked", "tiles_decorative"]));
  });
});
