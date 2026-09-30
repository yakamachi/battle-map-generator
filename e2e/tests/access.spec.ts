import { expect, request, test } from "@playwright/test";

// A caller without a session cannot generate, also when it skips the UI. A fresh request context
// with an empty storage state carries no cookie: no auth call is made and no generate permit is spent.
test("generating without a session returns 401", async ({ baseURL }) => {
  const anonymous = await request.newContext({ baseURL, storageState: { cookies: [], origins: [] } });
  try {
    const response = await anonymous.post("/api/maps/generate", { data: {} });
    expect(response.status()).toBe(401);
  } finally {
    await anonymous.dispose();
  }
});
