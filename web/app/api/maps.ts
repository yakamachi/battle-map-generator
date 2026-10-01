import createClient from "openapi-fetch";
import type { GenerateResult } from "./client";
import type { components, paths } from "./schema";

// The generate request with encounter parameters (S-03). It lives apart from client.ts while S-04
// changes that file in parallel; merge it into client.ts's generateMap once S-04 has landed, and
// fold in whatever S-04 added there (for example 401 handling).

export type EncounterType = components["schemas"]["EncounterType"];
// The generated type includes null (a skirmish echoes no boss size); a request names a real size.
export type BossSize = NonNullable<components["schemas"]["BossSize"]>;

// The API's limits (api/Maps/MapModels.cs, MapSize); it answers 400 outside them.
export const MIN_ROOM_COUNT = 2;
export const MAX_ROOM_COUNT = 12;
export const DEFAULT_ROOM_COUNT = 6;

export type MapRequest = {
  roomCount: number;
  encounter: EncounterType;
  // Required by the API for a boss fight; ignored on a skirmish.
  bossSize?: BossSize;
  seed?: number;
};

// Relative /api URLs: the API serves this SPA in production and Vite proxies /api in dev.
const client = createClient<paths>({ baseUrl: "" });

export async function generateMapWith({
  roomCount,
  encounter,
  bossSize,
  seed,
}: MapRequest): Promise<GenerateResult> {
  let result;
  try {
    result = await client.POST("/api/maps/generate", {
      body: { seed: seed ?? null, roomCount, encounter, bossSize: bossSize ?? null },
    });
  } catch {
    return { ok: false, error: { kind: "network" } };
  }

  const { data, response } = result;
  if (data) return { ok: true, map: data };
  // 429 comes from the rate limiter and is not part of the OpenAPI document. A 400 (parameters
  // the API rejects) is reported like any other failed status.
  if (response.status === 429) return { ok: false, error: { kind: "rate-limited" } };
  return { ok: false, error: { kind: "http", status: response.status } };
}
