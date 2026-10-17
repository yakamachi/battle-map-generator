import { afterEach, describe, expect, test, vi } from "vitest";

// openapi-fetch resolves its default fetch (globalThis.fetch) once, when createClient() runs at
// module-eval time (see app/api/client.ts), and vi.resetModules() does not re-evaluate it in
// browser mode. So the global is stubbed with one mock before the first (dynamic) import, and
// each test only changes what that mock answers.
const fetchMock = vi.fn<typeof fetch>();
vi.stubGlobal("fetch", fetchMock);
const { deleteSavedMap, generateMap, getSavedMap, listSavedMaps, renameSavedMap, saveMap } =
  await import("./client");

afterEach(() => {
  fetchMock.mockReset();
  vi.restoreAllMocks();
});

describe("generateMap", () => {
  test("reports a swallowed network error without changing the result", async () => {
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));
    const spy = vi.spyOn(console, "error");

    const result = await generateMap({ roomCount: 6, encounter: "skirmish" });

    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("POST /api/maps/generate");
    expect(result).toEqual({ ok: false, error: { kind: "network" } });
  });
});

// Each library call maps a failed response to a UI-safe kind. The 401 redirect is not exercised
// here: window.location.assign cannot be stubbed in browser mode.
const MAP_ID = "3f2b1c4d-0000-4000-8000-000000000001";

describe("library calls", () => {
  test("a 404 is not-found", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 404 }));
    expect(await getSavedMap(MAP_ID)).toEqual({ ok: false, error: { kind: "not-found" } });
  });

  test("a 400 carries the API's errors record", async () => {
    const errors = { request: ["You can keep at most 100 saved maps. Delete one to save another."] };
    fetchMock.mockResolvedValue(
      new Response(JSON.stringify({ status: 400, errors }), {
        status: 400,
        headers: { "Content-Type": "application/problem+json" },
      }),
    );
    const result = await saveMap({
      title: "Boss lair",
      seed: 42,
      roomCount: 6,
      encounter: "skirmish",
      bossSize: null,
    });
    expect(result).toEqual({ ok: false, error: { kind: "invalid", errors } });
  });

  test("a 400 without an errors record is a plain HTTP error", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 400 }));
    expect(await renameSavedMap(MAP_ID, "x")).toEqual({
      ok: false,
      error: { kind: "http", status: 400 },
    });
  });

  test("rename sends the trimmed title to the map's route", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 500 }));
    await renameSavedMap(MAP_ID, "  Boss lair  ");
    const request = fetchMock.mock.calls[0][0] as Request;
    expect(request.method).toBe("PATCH");
    expect(new URL(request.url).pathname).toBe(`/api/maps/${MAP_ID}`);
    expect(await request.json()).toEqual({ title: "Boss lair" });
  });

  test("a 204 delete succeeds", async () => {
    fetchMock.mockResolvedValue(new Response(null, { status: 204 }));
    expect(await deleteSavedMap(MAP_ID)).toEqual({ ok: true });
  });

  test("a network failure is reported once and mapped to network", async () => {
    fetchMock.mockRejectedValue(new TypeError("Failed to fetch"));
    const spy = vi.spyOn(console, "error");
    expect(await listSavedMaps()).toEqual({ ok: false, error: { kind: "network" } });
    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("GET /api/maps");
  });
});
