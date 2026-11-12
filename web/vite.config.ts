/// <reference types="vitest/config" />
import react from "@vitejs/plugin-react";
import { defineConfig } from "vite";

const apiTarget = process.env.MOSCORUN_API_URL ?? "http://localhost:5080";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // In sviluppo il backend è dietro lo stesso origin, come in produzione: cookie SameSite=Strict funzionano.
    proxy: { "/api": apiTarget, "/health": apiTarget },
  },
  build: {
    outDir: "dist",
    // Nessuna source map pubblica: i file statici sono serviti anche senza autenticazione.
    sourcemap: false,
    target: "es2023",
  },
  test: {
    environment: "jsdom",
    globals: true,
    setupFiles: ["./src/test/setup.ts"],
    include: ["src/**/*.test.{ts,tsx}"],
    restoreMocks: true,
    coverage: {
      provider: "v8",
      include: ["src/**/*.{ts,tsx}"],
      exclude: ["src/**/*.test.{ts,tsx}", "src/test/**", "src/api/schema.d.ts", "src/main.tsx"],
      thresholds: { lines: 80, functions: 80, branches: 75, statements: 80 },
    },
  },
});
