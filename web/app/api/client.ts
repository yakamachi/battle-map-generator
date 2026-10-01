import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type GeneratedMap = components["schemas"]["GeneratedMap"];
export type CellKind = components["schemas"]["CellKind"];
export type EncounterType = components["schemas"]["EncounterType"];
// The generated type includes null (a skirmish echoes no boss size); a request names a real size.
export type BossSize = NonNullable<components["schemas"]["BossSize"]>;

export type GenerateError =
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type GenerateResult =
  | { ok: true; map: GeneratedMap }
  | { ok: false; error: GenerateError };

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
// Exported so app/api/auth.ts shares the same client and base URL.
export const client = createClient<paths>({ baseUrl: "" });

export async function generateMap({
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
  if (response.status === 401) {
    // The session ended mid-use, or this 401 came from a database that was still resuming
    // (phase 3 review F3); login.tsx reads expired=1 and explains both possibilities.
    window.location.assign("/login?expired=1");
    return { ok: false, error: { kind: "http", status: 401 } };
  }
  return { ok: false, error: { kind: "http", status: response.status } };
}
