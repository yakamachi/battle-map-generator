import createClient from "openapi-fetch";
import * as Sentry from "@sentry/react";
import type { components, paths } from "./schema";

export type GeneratedMap = components["schemas"]["GeneratedMap"];
export type CellKind = components["schemas"]["CellKind"];
export type EncounterType = components["schemas"]["EncounterType"];
// The generated type includes null (a skirmish echoes no boss size); a request names a real size.
export type BossSize = NonNullable<components["schemas"]["BossSize"]>;
export type MapParameters = components["schemas"]["MapParameters"];
export type SavedMap = components["schemas"]["SavedMap"];
export type SavedMapSummary = components["schemas"]["SavedMapSummary"];

export type GenerateError =
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type GenerateResult =
  | { ok: true; map: GeneratedMap }
  | { ok: false; error: GenerateError };

// The saved-map library's failures. not-found is a missing map and another account's map alike
// (the API never says which). invalid carries the API's errors record: "title" for the title rule,
// "request" for the 100-map cap, or a generate field key.
export type LibraryError =
  | { kind: "not-found" }
  | { kind: "invalid"; errors: Record<string, string[]> }
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type SavedMapResult = { ok: true; savedMap: SavedMap } | { ok: false; error: LibraryError };
export type SavedMapListResult =
  | { ok: true; maps: SavedMapSummary[] }
  | { ok: false; error: LibraryError };
export type DeleteResult = { ok: true } | { ok: false; error: LibraryError };

export type SaveMapRequest = {
  title: string;
  seed: number;
  roomCount: number;
  encounter: EncounterType;
  bossSize: BossSize | null;
};

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

// Single choke point for a swallowed error: every catch block in this module and auth.ts
// reports through here before mapping to a UI-safe kind, so wiring a real tracker later only
// means changing this function's body. label names the HTTP method and route, e.g.
// "POST /api/maps/generate". Never throws; no return value.
export function reportError(label: string, error: unknown): void {
  console.error(`[api] ${label} failed`, error);
  Sentry.captureException(error, { tags: { call: label } });
}

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
  } catch (error) {
    reportError("POST /api/maps/generate", error);
    return { ok: false, error: { kind: "network" } };
  }

  const { data, response } = result;
  if (data) return { ok: true, map: data };
  // 429 comes from the rate limiter and is not part of the OpenAPI document. A 400 (parameters
  // the API rejects) is reported like any other failed status.
  if (response.status === 429) return { ok: false, error: { kind: "rate-limited" } };
  if (redirectIfSessionExpired(response)) {
    return { ok: false, error: { kind: "http", status: 401 } };
  }
  return { ok: false, error: { kind: "http", status: response.status } };
}

// The one place a 401 from a signed-in page is handled, shared by generateMap and every library
// call: the session ended mid-use, or this 401 came from a database that was still resuming
// (phase 3 review F3); login.tsx reads expired=1 and explains both possibilities. Returns whether
// it redirected, so the caller can stop and return its failure.
function redirectIfSessionExpired(response: Response): boolean {
  if (response.status !== 401) return false;
  window.location.assign("/login?expired=1");
  return true;
}

// Maps a library call's failed response to a UI-safe kind. A 400 without an errors record falls
// back to the generic HTTP error, as register does in auth.ts. 429 comes from SessionCookieGate's
// per-IP limit and is not part of the OpenAPI document.
function libraryError(response: Response, body: unknown): LibraryError {
  if (redirectIfSessionExpired(response)) return { kind: "http", status: 401 };
  if (response.status === 404) return { kind: "not-found" };
  if (response.status === 429) return { kind: "rate-limited" };
  const errors = (body as { errors?: Record<string, string[]> } | undefined)?.errors;
  if (response.status === 400 && errors) return { kind: "invalid", errors };
  return { kind: "http", status: response.status };
}

export async function listSavedMaps(): Promise<SavedMapListResult> {
  try {
    const { data, error, response } = await client.GET("/api/maps");
    if (data) return { ok: true, maps: data };
    return { ok: false, error: libraryError(response, error) };
  } catch (error) {
    reportError("GET /api/maps", error);
    return { ok: false, error: { kind: "network" } };
  }
}

export async function getSavedMap(id: string): Promise<SavedMapResult> {
  try {
    const { data, error, response } = await client.GET("/api/maps/{id}", {
      params: { path: { id } },
    });
    if (data) return { ok: true, savedMap: data };
    return { ok: false, error: libraryError(response, error) };
  } catch (error) {
    reportError("GET /api/maps/{id}", error);
    return { ok: false, error: { kind: "network" } };
  }
}

// The server regenerates the map from the seed and parameters and stores that, so the caller
// shows the map in the response, not the one it already holds (they differ only if the
// algorithm changed in between).
export async function saveMap({
  title,
  seed,
  roomCount,
  encounter,
  bossSize,
}: SaveMapRequest): Promise<SavedMapResult> {
  try {
    const { data, error, response } = await client.POST("/api/maps", {
      body: { title: title.trim(), seed, roomCount, encounter, bossSize },
    });
    if (data) return { ok: true, savedMap: data };
    return { ok: false, error: libraryError(response, error) };
  } catch (error) {
    reportError("POST /api/maps", error);
    return { ok: false, error: { kind: "network" } };
  }
}

export async function renameSavedMap(id: string, title: string): Promise<SavedMapResult> {
  try {
    const { data, error, response } = await client.PATCH("/api/maps/{id}", {
      params: { path: { id } },
      body: { title: title.trim() },
    });
    if (data) return { ok: true, savedMap: data };
    return { ok: false, error: libraryError(response, error) };
  } catch (error) {
    reportError("PATCH /api/maps/{id}", error);
    return { ok: false, error: { kind: "network" } };
  }
}

export async function deleteSavedMap(id: string): Promise<DeleteResult> {
  try {
    const { error, response } = await client.DELETE("/api/maps/{id}", {
      params: { path: { id } },
    });
    if (response.ok) return { ok: true };
    return { ok: false, error: libraryError(response, error) };
  } catch (error) {
    reportError("DELETE /api/maps/{id}", error);
    return { ok: false, error: { kind: "network" } };
  }
}
