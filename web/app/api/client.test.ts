import { afterEach, describe, expect, test, vi } from "vitest";
// openapi-fetch resolves its default fetch (globalThis.fetch) once, when createClient() runs
// at module-eval time (see app/api/client.ts). Stubbing the global only takes effect for a
// client created afterward, so each test resets the module cache and re-imports dynamically
// *after* stubbing, rather than using the static import this file would otherwise have at top.
import type * as ClientModule from "./client";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("generateMap", () => {
  test("reports a swallowed network error without changing the result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const spy = vi.spyOn(console, "error");
    vi.resetModules();
    const { generateMap } = await import("./client") as typeof ClientModule;

    const result = await generateMap({ roomCount: 6, encounter: "skirmish" });

    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("POST /api/maps/generate");
    expect(result).toEqual({ ok: false, error: { kind: "network" } });
  });
});
