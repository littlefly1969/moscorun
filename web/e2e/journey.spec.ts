import AxeBuilder from "@axe-core/playwright";
import { expect, test, type Page } from "@playwright/test";

const adminEmail = process.env.E2E_ADMIN_EMAIL ?? "admin@moscorun.local";
const adminPassword = process.env.E2E_ADMIN_PASSWORD ?? "";

async function login(page: Page) {
  await page.goto("/");
  await expect(page).toHaveURL(/\/accesso$/);
  await page.getByLabel("Email").fill(adminEmail);
  await page.getByLabel("Password").fill(adminPassword);
  await page.getByRole("button", { name: "Accedi" }).click();
  await expect(page.getByRole("heading", { name: "Forma" })).toBeVisible();
}

async function expectNoA11yViolations(page: Page) {
  const results = await new AxeBuilder({ page }).withTags(["wcag2a", "wcag2aa", "wcag21aa"]).analyze();
  expect(results.violations).toEqual([]);
}

test.beforeAll(() => {
  if (!adminPassword) throw new Error("Impostare E2E_ADMIN_PASSWORD");
});

test("la pagina di accesso è accessibile e rifiuta credenziali errate", async ({ page }) => {
  await page.goto("/accesso");
  await expectNoA11yViolations(page);
  await page.getByLabel("Email").fill(adminEmail);
  await page.getByLabel("Password").fill("password-sbagliata");
  await page.getByRole("button", { name: "Accedi" }).click();
  await expect(page.getByRole("alert")).toHaveText("Email o password non corrette.");
});

test("percorso completo: dispositivo, pesata dall'agente, cruscotto", async ({ page, request }, testInfo) => {
  await login(page);

  // 1. Registrazione di un agente bilancia dal cruscotto: il token appare una sola volta.
  await page.getByRole("link", { name: "Dispositivi" }).click();
  const deviceName = `Bilancia E2E ${testInfo.project.name} ${Date.now()}`;
  await page.getByLabel("Nome").fill(deviceName);
  await page.getByRole("button", { name: "Crea e genera token" }).click();
  const token = (await page.locator("code.token").textContent())?.trim() ?? "";
  expect(token).toMatch(/^mr1_[0-9a-f]{32}_[A-Za-z0-9_-]{43}$/);
  await expectNoA11yViolations(page);
  await page.getByRole("button", { name: "Ho salvato il token" }).click();
  await expect(page.locator("code.token")).toHaveCount(0);

  // 2. L'agente invia due pesate (una ripetuta: deve restare una sola).
  const weight = 70 + Math.round(Math.random() * 1000) / 100;
  const event = `e2e-${testInfo.project.name}-${Date.now()}`;
  const headers = { Authorization: `Bearer ${token}` };
  const body = {
    sourceEventId: event,
    measuredAt: new Date().toISOString(),
    weightKg: weight,
    impedanceOhm: 500,
  };
  expect((await request.post("/api/v1/ingest/weighings", { headers, data: body })).status()).toBe(201);
  expect((await request.post("/api/v1/ingest/weighings", { headers, data: body })).status()).toBe(200);

  // 3. Il cruscotto mostra la pesata e la sorgente attiva.
  await page.getByRole("link", { name: "Forma" }).click();
  const formatted = new Intl.NumberFormat("it-IT", {
    minimumFractionDigits: 1,
    maximumFractionDigits: 2,
  }).format(weight);
  await expect(page.getByRole("region", { name: "Ultimo peso" })).toContainText(`${formatted} kg`);
  await expect(page.getByText(`${deviceName}: ultima pesata ricevuta`)).toBeVisible();
  await expectNoA11yViolations(page);

  // 4. La pesata arriva "da confermare" e si conferma con un solo clic.
  const weighingRow = page
    .getByRole("region", { name: "Pesate recenti" })
    .getByRole("row")
    .filter({ hasText: `${formatted} kg` });
  await expect(weighingRow.getByRole("cell").nth(2)).toHaveText("Da confermare");
  await weighingRow.getByRole("button", { name: "È mia", exact: true }).click();
  await expect(weighingRow.getByRole("cell").nth(2)).toHaveText("Mia");
  await expect(weighingRow.getByRole("button")).toHaveText(["Non è mia"]);

  // 5. Revoca: il token smette di funzionare subito. La pagina non deve scorrere in orizzontale
  //    (su mobile il browser ridurrebbe lo zoom e i pulsanti diventerebbero difficili da toccare).
  await page.getByRole("link", { name: "Dispositivi" }).click();
  await expect(page.getByRole("table")).toBeVisible();
  const overflow = await page.evaluate<number>(
    "document.documentElement.scrollWidth - document.documentElement.clientWidth",
  );
  expect(overflow).toBeLessThanOrEqual(0);
  const row = page.getByRole("row", { name: deviceName });
  await row.getByRole("button", { name: "Revoca" }).click();
  await row.getByRole("button", { name: "Conferma revoca" }).click();
  await expect(row).toContainText("revocato");
  const afterRevoke = await request.post("/api/v1/ingest/weighings", {
    headers,
    data: { ...body, sourceEventId: `${event}-2` },
  });
  expect(afterRevoke.status()).toBe(401);

  // 6. Uscita.
  await page.getByRole("button", { name: "Esci" }).click();
  await expect(page).toHaveURL(/\/accesso$/);
});
