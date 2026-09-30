import { reactRouter } from "@react-router/dev/vite";
import tailwindcss from "@tailwindcss/vite";
import { defineConfig } from "vite";

export default defineConfig({
  plugins: [tailwindcss(), reactRouter()],
  resolve: {
    tsconfigPaths: true,
  },
  server: {
    // In dev the API runs separately (`dotnet run` in api/); in production it serves this SPA itself.
    proxy: {
      "/api": "http://localhost:5108",
    },
  },
  preview: {
    // The SPA build prerenders index.html through `vite preview`. Pin IPv4: in a container with an
    // IPv6 loopback (the visual-test Playwright image) "localhost" made the preview server and the
    // prerender request disagree, and the build failed with ECONNREFUSED.
    host: "127.0.0.1",
  },
});
