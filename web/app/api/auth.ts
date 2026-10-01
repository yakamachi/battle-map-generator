import { client } from "./client";
import type { components } from "./schema";

export type AccountInfo = components["schemas"]["AccountInfo"];

export type LoginError =
  | { kind: "invalid-credentials" }
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type RegisterError =
  | { kind: "validation"; errors: Record<string, string[]> }
  | { kind: "rate-limited" }
  | { kind: "network" }
  | { kind: "http"; status: number };

export type LogoutError = { kind: "network" } | { kind: "http"; status: number };

export type LoginResult = { ok: true; account: AccountInfo } | { ok: false; error: LoginError };
export type RegisterResult =
  | { ok: true; account: AccountInfo }
  | { ok: false; error: RegisterError };
export type LogoutResult = { ok: true } | { ok: false; error: LogoutError };

// null on 401 (no session); throws on anything else (a network failure, or an unexpected status
// such as a 500 from a database still resuming), so a caller that cannot handle it surfaces it as
// a route error rather than silently treating it as "logged out".
export async function getSession(): Promise<AccountInfo | null> {
  const { data, response } = await client.GET("/api/auth/me");
  if (data) return data;
  if (response.status === 401) return null;
  throw new Error(`GET /api/auth/me returned ${response.status}`);
}

export async function register(email: string, password: string): Promise<RegisterResult> {
  let result;
  try {
    result = await client.POST("/api/auth/register", { body: { email, password } });
  } catch {
    return { ok: false, error: { kind: "network" } };
  }

  const { data, error, response } = result;
  if (data) return { ok: true, account: data };
  if (response.status === 429) return { ok: false, error: { kind: "rate-limited" } };
  // A malformed 400 body (no errors field) falls back to the generic HTTP error (phase 1 review F8).
  if (response.status === 400 && error?.errors) {
    return { ok: false, error: { kind: "validation", errors: error.errors } };
  }
  return { ok: false, error: { kind: "http", status: response.status } };
}

export async function login(email: string, password: string): Promise<LoginResult> {
  let result;
  try {
    result = await client.POST("/api/auth/login", { body: { email, password } });
  } catch {
    return { ok: false, error: { kind: "network" } };
  }

  const { data, response } = result;
  if (data) return { ok: true, account: data };
  if (response.status === 401) return { ok: false, error: { kind: "invalid-credentials" } };
  if (response.status === 429) return { ok: false, error: { kind: "rate-limited" } };
  return { ok: false, error: { kind: "http", status: response.status } };
}

export async function logout(): Promise<LogoutResult> {
  try {
    const { response } = await client.POST("/api/auth/logout");
    if (response.ok) return { ok: true };
    return { ok: false, error: { kind: "http", status: response.status } };
  } catch {
    return { ok: false, error: { kind: "network" } };
  }
}
