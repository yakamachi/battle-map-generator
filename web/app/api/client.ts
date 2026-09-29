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
const client = createClient<paths>({ baseUrl: "" });

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
  return { ok: false, error: { kind: "http", status: response.status } };
}
