import type { CellKind, GeneratedMap } from "../api/client";
import atlasIndex from "./tileset/scribble-atlas.json";
import { mulberry32 } from "./prng";

export const TILE_SIZE = 140;

export type Rotation = 0 | 90 | 180 | 270;

export type Piece =
  | "tiles"
  | "tiles_cracked"
  | "tiles_decorative"
  | "tile"
  | "wall"
  | "wall_corner"
  | "wall_edge"
  | "inner_round"
  | "door_closed";

export type DrawOp = { piece: Piece; rotation: Rotation; cellX: number; cellY: number };

export type MapGrid = Pick<GeneratedMap, "seed" | "width" | "height" | "cells">;

const atlas: Record<string, { x: number; y: number } | undefined> = atlasIndex;

export function atlasPosition(piece: Piece, rotation: Rotation): { x: number; y: number } {
  const position = atlas[`${piece}@${rotation}`];
  if (!position) throw new Error(`Atlas has no ${piece}@${rotation}`);
  return position;
}

const CRACKED_CHANCE = 0.06;

// Sides in clockwise order; a piece's rotation is the clockwise turn from its N-facing art.
const SIDES = [
  { dx: 0, dy: -1, rotation: 0 },
  { dx: 1, dy: 0, rotation: 90 },
  { dx: 0, dy: 1, rotation: 180 },
  { dx: -1, dy: 0, rotation: 270 },
] as const;

// Corner i lies between side i and the side before it: NW, NE, SE, SW. The corner art
// (wall_corner, wall_edge, inner_round) faces NW at 0 degrees.
const CORNERS = [
  { a: 3, b: 0, dx: -1, dy: -1, rotation: 0 },
  { a: 0, b: 1, dx: 1, dy: -1, rotation: 90 },
  { a: 1, b: 2, dx: 1, dy: 1, rotation: 180 },
  { a: 2, b: 3, dx: -1, dy: 1, rotation: 270 },
] as const;

// The boss arena's floor is room floor in every rule; only its piece differs.
function isRoomFloor(kind: CellKind | undefined): boolean {
  return kind === "floor" || kind === "bossArena";
}

function isWalkable(kind: CellKind | undefined): boolean {
  return isRoomFloor(kind) || kind === "corridor" || kind === "door";
}

// Edge-wall model (research.md, Model B): wall and void cells stay blank paper, and every
// walkable cell draws the walls on its own sides that face rock or the map edge.
export function drawOps(map: MapGrid): DrawOp[] {
  const { width, height, cells } = map;
  const kindAt = (x: number, y: number): CellKind | undefined =>
    x < 0 || y < 0 || x >= width || y >= height ? undefined : cells[y * width + x];

  const floors: DrawOp[] = [];
  const walls: DrawOp[] = [];
  const doors: DrawOp[] = [];
  const random = mulberry32(map.seed);

  for (let cellY = 0; cellY < height; cellY++) {
    for (let cellX = 0; cellX < width; cellX++) {
      const kind = kindAt(cellX, cellY);
      if (!isWalkable(kind)) continue;

      if (kind === "bossArena") {
        // Every arena cell gets the decorative piece, so plain floors never do.
        floors.push({ piece: "tiles_decorative", rotation: 0, cellX, cellY });
      } else if (kind === "floor") {
        const piece: Piece = random() < CRACKED_CHANCE ? "tiles_cracked" : "tiles";
        floors.push({ piece, rotation: 0, cellX, cellY });
      } else {
        // Corridors and doors both lie on a plain tile; the door bar goes on top.
        floors.push({ piece: "tile", rotation: 0, cellX, cellY });
      }

      const closed = SIDES.map((s) => !isWalkable(kindAt(cellX + s.dx, cellY + s.dy)));
      const cornerPiece: Piece = kind === "corridor" ? "wall_edge" : "wall_corner";

      // Two adjacent closed sides share one corner piece; a side that belongs to no closed
      // corner gets a plain strip. Three or four closed sides become two or four corners,
      // whose overlapping bands are identical, so dead ends need no extra art.
      const covered = [false, false, false, false];
      for (const corner of CORNERS) {
        if (closed[corner.a] && closed[corner.b]) {
          walls.push({ piece: cornerPiece, rotation: corner.rotation, cellX, cellY });
          covered[corner.a] = covered[corner.b] = true;
        }
      }
      SIDES.forEach((side, i) => {
        if (closed[i] && !covered[i]) {
          walls.push({ piece: "wall", rotation: side.rotation, cellX, cellY });
        }
      });

      for (const corner of CORNERS) {
        const diagonalClosed = !isWalkable(kindAt(cellX + corner.dx, cellY + corner.dy));
        if (!closed[corner.a] && !closed[corner.b] && diagonalClosed) {
          walls.push({ piece: "inner_round", rotation: corner.rotation, cellX, cellY });
        }
      }

      if (kind === "door") {
        const rotation = doorRotation(kindAt, cellX, cellY);
        doors.push({ piece: "door_closed", rotation, cellX, cellY });
      }
    }
  }

  return [...floors, ...walls, ...doors];
}

// The door bar lies on the door cell's edge that faces the room, along the room's wall line.
function doorRotation(
  kindAt: (x: number, y: number) => CellKind | undefined,
  x: number,
  y: number,
): Rotation {
  const northSouth = isWalkable(kindAt(x, y - 1)) && isWalkable(kindAt(x, y + 1));
  if (northSouth) {
    if (isRoomFloor(kindAt(x, y - 1))) return 0;
    if (isRoomFloor(kindAt(x, y + 1))) return 180;
    return 0;
  }
  if (isRoomFloor(kindAt(x - 1, y))) return 270;
  if (isRoomFloor(kindAt(x + 1, y))) return 90;
  return 270;
}
