import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import { renderApp } from "../../test/render";
import type { Schemas } from "../../api/client";

const at = "2026-11-26T12:00:00Z";
const inventory: Schemas["ScientificInventory"] = {
  asOf: at,
  observations: [
    {
      metric: "spo2",
      unit: "%",
      source: "health-connect",
      method: "Measured",
      deviceId: "d1",
      session: false,
      revisionRows: 3,
      nonNullRows: 2,
      firstTime: at,
      lastTime: at,
      lastAvailableAt: at,
    },
  ],
  signals: [
    {
      metric: "rrInterval",
      unit: "ms",
      source: "garmin-connect",
      method: "Measured",
      deviceId: "d1",
      session: true,
      chunkRevisions: 1,
      storedSamples: 120,
      firstTime: at,
      lastTime: at,
      lastAvailableAt: at,
    },
  ],
  scales: [],
  devices: [{ id: "d1", name: "Telefono", lastSeenAt: at, revokedAt: null }],
  diagnostics: [
    {
      deviceId: "d1",
      receivedAt: at,
      report: {
        source: "health-connect",
        runId: "run1",
        startedAt: at,
        finishedAt: at,
        state: "COMPLETED",
        windowsRead: 1,
        firstReadFrom: "2026-11-20T12:00:00Z",
        readThrough: at,
        metrics: [
          {
            metric: "spo2",
            permission: "granted",
            read: 4,
            attempted: 3,
            confirmed: 1,
            rejected: 1,
            conflicts: 0,
          },
          {
            metric: "vo2max",
            permission: "denied",
            read: 0,
            attempted: 0,
            confirmed: 0,
            rejected: 0,
            conflicts: 0,
          },
        ],
      },
    },
  ],
};

describe("centro acquisizione", () => {
  it("distingue archivio, permessi e contatori e permette di aprire lo storico non esposto dal daily", async () => {
    let page = 0;
    server.use(
      http.get("*/api/v1/science/inventory", () => HttpResponse.json(inventory)),
      http.get("*/api/v1/science/inventory/observations", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("metric")).toBe("spo2");
        expect(query.get("source")).toBe("health-connect");
        page++;
        if (page > 1) expect(query.get("beforeId")).toBe("row1");
        return HttpResponse.json({
          asOf: at,
          hasMore: page === 1,
          items: [
            {
              id: "row" + String(page),
              metric: "spo2",
              unit: "%",
              value: page === 1 ? 97 : null,
              source: "health-connect",
              method: "Measured",
              providerAlgorithm: "com.sec.android.app.shealth",
              deviceId: "d1",
              workoutId: null,
              time: at,
              availableAt: at,
              externalRevision: "1",
              revisionOrder: 1,
            },
          ],
        });
      }),
    );
    renderApp("/acquisizione");
    const catalog = await screen.findByRole("region", { name: "Inventario delle metriche" });
    const phone = screen.getByRole("region", { name: "Diagnostica trasmessa dal telefono" });
    expect(within(phone).getByText("Non concesso")).toBeInTheDocument();
    expect(within(catalog).getByRole("row", { name: /Saturazione SpO₂/ })).toHaveTextContent("2/3");
    expect(within(catalog).getByRole("row", { name: /Intervallo RR/ })).toHaveTextContent("1/120");
    await userEvent.click(within(catalog).getByRole("button", { name: "Saturazione SpO₂" }));
    expect(screen.getByRole("region", { name: "Provenienza della metrica" })).toHaveTextContent(
      "Non esposta dal motore giornaliero",
    );
    await userEvent.click(screen.getByRole("button", { name: "Vedi osservazioni: health-connect" }));
    expect(await screen.findByText("97 %")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Altre osservazioni" }));
    expect(await screen.findByText("Mancante")).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText("Sorgente"), "garmin-connect");
    expect(screen.queryByRole("region", { name: "Storico delle osservazioni" })).toBeNull();
    await userEvent.click(screen.getByLabelText("Solo record presenti"));
    expect(within(catalog).getAllByRole("row")).toHaveLength(2);
    await userEvent.type(screen.getByLabelText("Cerca metrica"), "spo2");
    expect(screen.getByText("Nessun risultato per i filtri selezionati.")).toBeInTheDocument();
  });

  it("non trasforma un errore di rete in un archivio vuoto", async () => {
    server.use(http.get("*/api/v1/science/inventory", () => new HttpResponse(null, { status: 503 })));
    renderApp("/acquisizione");
    expect(await screen.findByRole("alert")).toHaveTextContent("non è possibile stabilire");
    expect(screen.queryByRole("region", { name: "Inventario delle metriche" })).toBeNull();
  });

  it("esplicita che senza rapporto remoto i permessi non sono noti", async () => {
    server.use(
      http.get("*/api/v1/science/inventory", () =>
        HttpResponse.json({
          ...inventory,
          observations: [],
          signals: [],
          diagnostics: [],
        }),
      ),
    );
    renderApp("/acquisizione");
    expect(await screen.findByText(/Nessun rapporto diagnostico ricevuto/)).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Inventario delle metriche" })).toHaveTextContent(
      "Nessun record",
    );
  });
  it("mostra pesate, valori nulli, dispositivi revocati e permessi ignoti senza mescolare le fonti", async () => {
    server.use(
      http.get("*/api/v1/science/inventory", () =>
        HttpResponse.json({
          ...inventory,
          observations: [{ ...inventory.observations[0], metric: "fatigue", nonNullRows: 0, deviceId: null }],
          scales: [
            {
              deviceId: "scale1",
              attribution: "Rejected",
              weighings: 3,
              withImpedance: 1,
              firstTime: at,
              lastTime: at,
              lastReceivedAt: at,
            },
          ],
          devices: [{ id: "scale1", name: "Bilancia", lastSeenAt: null, revokedAt: at }],
          diagnostics: [
            {
              ...inventory.diagnostics[0],
              report: {
                ...inventory.diagnostics[0]?.report,
                windowsRead: 0,
                firstReadFrom: null,
                readThrough: null,
                metrics: [
                  {
                    metric: "spo2",
                    permission: "unknown",
                    read: 0,
                    attempted: 0,
                    confirmed: 0,
                    rejected: 0,
                    conflicts: 0,
                  },
                ],
              },
            },
          ],
        }),
      ),
    );
    renderApp("/acquisizione?metrica=weight");
    const details = await screen.findByRole("region", { name: "Provenienza della metrica" });
    expect(within(details).getByText(/3 record bilancia/)).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Pesate per dispositivo" })).toHaveTextContent("Rejected");
    expect(screen.getByRole("region", { name: "Stato dei dispositivi" })).toHaveTextContent("Revocato");
    expect(screen.getByRole("region", { name: "Diagnostica trasmessa dal telefono" })).toHaveTextContent(
      "Capacità non verificata",
    );
    await userEvent.click(within(details).getByRole("button", { name: "Mostra tutte le metriche" }));
    expect(screen.getByRole("region", { name: "Diagnostica trasmessa dal telefono" })).toHaveTextContent(
      "Sconosciuto",
    );
    const catalog = screen.getByRole("region", { name: "Inventario delle metriche" });
    expect(within(catalog).getByRole("row", { name: /Fatica/ })).toHaveTextContent("Solo valori mancanti");
    await userEvent.click(within(catalog).getByRole("button", { name: "Intervallo RR" }));
    expect(screen.getByRole("region", { name: "Provenienza della metrica" })).toHaveTextContent(
      "120 campioni memorizzati",
    );
    await userEvent.selectOptions(screen.getByLabelText("Sorgente"), "bilancia");
    expect(screen.getByRole("region", { name: "Provenienza della metrica" })).toHaveTextContent(
      "Nessun record scientifico",
    );
    await userEvent.click(screen.getByRole("button", { name: "Aggiorna inventario" }));
  });

  it("dall'atlante distingue una metrica remota non esposta dal motore daily", async () => {
    server.use(http.get("*/api/v1/science/inventory", () => HttpResponse.json(inventory)));
    renderApp("/");
    await userEvent.type(await screen.findByLabelText("Cerca metrica"), "spo2");
    const catalog = screen.getByRole("region", { name: "Catalogo metriche" });
    const link = await within(catalog).findByRole("link", { name: "Presente · 2 valori" });
    expect(link).toHaveAttribute("href", "/acquisizione?metrica=spo2");
    await userEvent.click(link);
    expect(await screen.findByRole("region", { name: "Provenienza della metrica" })).toHaveTextContent(
      "Non esposta dal motore giornaliero",
    );
  });
});
