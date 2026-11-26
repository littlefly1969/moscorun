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
  await expect(page.getByRole("heading", { name: "Atlante delle metriche" })).toBeVisible();
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

test("allenamento dal telefono: elenco, dettaglio e percorso senza cartografia", async ({
  page,
  request,
}, testInfo) => {
  await login(page);

  // 1. Registrazione del ponte Android, che invia gli allenamenti letti da Samsung Health.
  await page.getByRole("link", { name: "Dispositivi" }).click();
  await page.getByLabel("Nome").fill(`Telefono E2E ${testInfo.project.name} ${Date.now()}`);
  await page.getByLabel("Tipo").selectOption({ label: "Telefono Android" });
  await page.getByRole("button", { name: "Crea e genera token" }).click();
  const token = (await page.locator("code.token").textContent())?.trim() ?? "";
  await page.getByRole("button", { name: "Ho salvato il token" }).click();

  // 2. Il telefono invia una corsa con tracciato GPS. La distanza (sotto il km, mostrata in metri)
  //    rende riconoscibile la riga anche con i progetti desktop e mobile sullo stesso database.
  const distance = 200 + Math.floor(Math.random() * 790);
  const start = new Date(Date.now() - 3_600_000);
  const at = (s: number) => new Date(start.getTime() + s * 1000).toISOString();
  const body = {
    source: "samsung-health",
    sourceId: `e2e-${testInfo.project.name}-${Date.now()}`,
    revision: 1,
    activityType: "RUNNING",
    startedAt: at(0),
    endedAt: at(300),
    distanceM: distance,
    averageHeartRate: 142,
    maxHeartRate: 168,
    track: Array.from({ length: 150 }, (_, i) => ({
      time: at(i * 2),
      lat: 45.07 + i * 2e-5,
      lon: 7.68 + Math.sin(i / 20) * 5e-4,
    })),
    series: [
      {
        metric: "HeartRate",
        samples: Array.from({ length: 30 }, (_, i) => ({ time: at(i * 10), value: 130 + (i % 7) * 4 })),
      },
    ],
  };
  const headers = { Authorization: `Bearer ${token}` };
  expect((await request.post("/api/v1/ingest/workouts", { headers, data: body })).status()).toBe(201);
  expect((await request.get("/api/v1/workouts", { headers })).status()).toBe(200);

  // 3. Elenco: la riga con la distanza inviata porta al dettaglio.
  await page.getByRole("link", { name: "Allenamenti" }).click();
  await expect(page.getByRole("heading", { name: "Allenamenti", level: 1 })).toBeVisible();
  const row = page.getByRole("row").filter({ hasText: `${distance} m` });
  await expect(row).toContainText("Corsa");
  await expectNoA11yViolations(page);
  await row.getByRole("link").click();

  // 4. Dettaglio: senza chiave Google il percorso è disegnato senza mappa di sfondo.
  await expect(page.getByRole("heading", { level: 1 })).toContainText("Corsa");
  await expect(
    page.getByRole("img", { name: "Percorso dell'allenamento (senza mappa di sfondo)" }),
  ).toBeVisible();
  await expect(page.getByRole("heading", { name: "Frequenza cardiaca" })).toBeVisible();
  await expectNoA11yViolations(page);
  const overflow = await page.evaluate<number>(
    "document.documentElement.scrollWidth - document.documentElement.clientWidth",
  );
  expect(overflow).toBeLessThanOrEqual(0);
});
