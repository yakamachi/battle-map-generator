import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { test as base, expect } from "@playwright/test";

// Shared across every visual spec: hermetic fonts, so the gate never waits on the network and a
// change in the files Google serves cannot move the baselines, and a mocked /api/auth/me, so
// routes behind routes/protected.tsx render without a real backend.

// The app loads Inter from Google Fonts (app/root.tsx). Every test answers those requests with
// the vendored file in ./fonts, so the gate never waits on the network and a change in the files
// Google serves cannot move the baselines. Any other Google Fonts request is aborted and fails
// the test. The routes are on the context, so a test's page.unrouteAll() leaves them in place.
const INTER_WOFF2 = readFileSync(resolve(import.meta.dirname, "fonts/inter-latin-wght-normal.woff2"));
const VENDORED_FONT_URL = "https://fonts.gstatic.com/__vendored/inter.woff2";
const INTER_CSS = `@font-face {
  font-family: "Inter";
  font-style: normal;
  font-weight: 100 900;
  font-display: block;
  src: url(${VENDORED_FONT_URL}) format("woff2");
}
`;

// The fixed account /api/auth/me answers with by default; a spec that needs a logged-out visitor
// overrides it with test.use({ account: null }), which answers 401 instead.
export const ACCOUNT = { email: "dm@example.com" };

type Fixtures = {
  hermeticFonts: void;
  account: { email: string } | null;
  mockAuthMe: void;
};

export const test = base.extend<Fixtures>({
  account: [ACCOUNT, { option: true }],

  hermeticFonts: [
    async ({ context }, use) => {
      const stray: string[] = [];
      await context.route("https://fonts.googleapis.com/**", (route) => {
        const url = new URL(route.request().url());
        if (url.pathname === "/css2" && url.searchParams.get("family")?.startsWith("Inter:")) {
          return route.fulfill({ status: 200, contentType: "text/css; charset=utf-8", body: INTER_CSS });
        }
        stray.push(url.href);
        return route.abort();
      });
      await context.route("https://fonts.gstatic.com/**", (route) => {
        if (route.request().url() === VENDORED_FONT_URL) {
          // Fonts are fetched in CORS mode, so the cross-origin response must allow it.
          return route.fulfill({
            status: 200,
            contentType: "font/woff2",
            headers: { "Access-Control-Allow-Origin": "*" },
            body: INTER_WOFF2,
          });
        }
        stray.push(route.request().url());
        return route.abort();
      });
      await use();
      expect(stray, "Google Fonts requests the visual gate does not vendor").toEqual([]);
    },
    { auto: true },
  ],

  mockAuthMe: [
    async ({ context, account }, use) => {
      await context.route("**/api/auth/me", (route) => {
        if (account) {
          return route.fulfill({
            status: 200,
            contentType: "application/json",
            body: JSON.stringify(account),
          });
        }
        return route.fulfill({ status: 401 });
      });
      await use();
    },
    { auto: true },
  ],
});

export { expect };
