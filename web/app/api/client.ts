import createClient from "openapi-fetch";
import type { components, paths } from "./schema";

export type GeneratedMap = components["schemas"]["GeneratedMap"];
export type CellKind = components["schemas"]["CellKind"];

export type GenerateError =
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type GenerateResult =
  | { ok: true; map: GeneratedMap }
  | { ok: false; error: GenerateError };

// Relative /api URLs: the API serves this SPA in production and Vite proxies /api in dev.
// Exported so app/api/auth.ts shares the same client and base URL.
export const client = createClient<paths>({ baseUrl: "" });

export async function generateMap(seed?: number): Promise<GenerateResult> {
  let result;
  try {
    result = await client.POST("/api/maps/generate", {
      body: { seed: seed ?? null },
    });
  } catch {
    return { ok: false, error: { kind: "network" } };
  }

  const { data, response } = result;
  if (data) return { ok: true, map: data };
  // 429 comes from the rate limiter and is not part of the OpenAPI document.
  if (response.status === 429) return { ok: false, error: { kind: "rate-limited" } };
  if (response.status === 401) {
    // The session ended mid-use, or this 401 came from a database that was still resuming
    // (phase 3 review F3); login.tsx reads expired=1 and explains both possibilities.
    window.location.assign("/login?expired=1");
    return { ok: false, error: { kind: "http", status: 401 } };
  }
  return { ok: false, error: { kind: "http", status: response.status } };
}
