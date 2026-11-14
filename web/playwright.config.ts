import { defineConfig, devices } from "@playwright/test";

/**
 * Test end-to-end sull'intero stack (immagine container reale + PostgreSQL), avviato da
 * deploy/compose.yaml in CI. In locale: E2E_BASE_URL=http://127.0.0.1:8085 npm run e2e
 */
export default defineConfig({
  testDir: "./e2e",
  // Seriale: i test condividono l'utente amministratore e i tentativi falliti aggiornano la stessa riga.
  fullyParallel: false,
  workers: 1,
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0,
  reporter: process.env.CI ? [["github"], ["list"], ["html", { open: "never" }]] : "list",
  // Un'azione bloccata fallisce con il selettore nel messaggio invece di esaurire il tempo del test.
  expect: { timeout: 10_000 },
  use: {
    actionTimeout: 10_000,
    navigationTimeout: 15_000,
    baseURL: process.env.E2E_BASE_URL ?? "http://127.0.0.1:8085",
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    locale: "it-IT",
    timezoneId: "Europe/Rome",
    ...(process.env.PLAYWRIGHT_CHROMIUM_PATH
      ? { launchOptions: { executablePath: process.env.PLAYWRIGHT_CHROMIUM_PATH } }
      : {}),
  },
  projects: [
    { name: "desktop", use: { ...devices["Desktop Chrome"] } },
    { name: "mobile", use: { ...devices["Pixel 7"] } },
  ],
});
