import { afterEach, describe, expect, test, vi } from "vitest";
// Same reasoning as client.test.ts: auth.ts's register/login/logout call the shared `client`
// from app/api/client.ts, whose openapi-fetch instance resolves globalThis.fetch once, at
// createClient() time. Stubbing the global only takes effect for a client created afterward, so
// each test resets the module cache and re-imports both modules dynamically after stubbing.
import type * as AuthModule from "./auth";

afterEach(() => {
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
});

describe("register", () => {
  test("reports a swallowed network error without changing the result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const spy = vi.spyOn(console, "error");
    vi.resetModules();
    const { register } = (await import("./auth")) as typeof AuthModule;

    const result = await register("dm@example.com", "correct-horse-battery-staple");

    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("POST /api/auth/register");
    expect(result).toEqual({ ok: false, error: { kind: "network" } });
  });
});

describe("login", () => {
  test("reports a swallowed network error without changing the result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const spy = vi.spyOn(console, "error");
    vi.resetModules();
    const { login } = (await import("./auth")) as typeof AuthModule;

    const result = await login("dm@example.com", "correct-horse-battery-staple");

    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("POST /api/auth/login");
    expect(result).toEqual({ ok: false, error: { kind: "network" } });
  });
});

describe("logout", () => {
  test("reports a swallowed network error without changing the result", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const spy = vi.spyOn(console, "error");
    vi.resetModules();
    const { logout } = (await import("./auth")) as typeof AuthModule;

    const result = await logout();

    expect(spy).toHaveBeenCalledOnce();
    expect(spy.mock.calls[0][0]).toContain("POST /api/auth/logout");
    expect(result).toEqual({ ok: false, error: { kind: "network" } });
  });
});
