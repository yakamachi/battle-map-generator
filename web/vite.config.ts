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
});
