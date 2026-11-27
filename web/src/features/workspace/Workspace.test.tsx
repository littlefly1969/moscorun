import { fireEvent, screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";
import { metricCatalog } from "./metricCatalog";

describe("ambiente scientifico", () => {
  it("apre l’atlante e permette di cercare e filtrare il catalogo", async () => {
    renderApp("/");
    expect(await screen.findByRole("heading", { name: "Atlante delle metriche" })).toBeInTheDocument();
    const catalog = screen.getByRole("region", { name: "Catalogo metriche" });
    expect(within(catalog).getAllByRole("row")).toHaveLength(metricCatalog.length + 1);
    expect(metricCatalog).toHaveLength(120);
    await userEvent.type(screen.getByLabelText("Cerca metrica"), "groundContactTime");
    expect(within(catalog).getAllByRole("row")).toHaveLength(2);
    await userEvent.click(within(catalog).getByRole("button", { name: "Tempo di contatto a terra" }));
    expect(screen.getByRole("complementary", { name: "Dettaglio metrica" })).toHaveTextContent(
      "Lettura non collegata",
    );
    await userEvent.click(
      within(catalog).getByRole("button", { name: "Preferita: Tempo di contatto a terra" }),
    );
    await userEvent.clear(screen.getByLabelText("Cerca metrica"));
    await userEvent.click(screen.getByLabelText("Solo preferite (sessione)"));
    expect(within(catalog).getAllByRole("row")).toHaveLength(2);
  });

  it("interroga metriche giornaliere senza imputare i dati mancanti", async () => {
    server.use(
      http.get("*/api/v1/science/daily", ({ request }) => {
        const query = new URL(request.url).searchParams;
        const metric = query.get("metric");
        const observed = metric === "fatigue";
        expect(query.get("source")).toBe("health-connect");
        return HttpResponse.json({
          metric,
          unit: "bpm",
          source: "samsung-health",
          methodVersion: "daily-observation-median-mad-v1",
          asOf: "2026-11-26T12:00:00Z",
          status: observed ? "observed" : "missing",
          current: {
            day: query.get("day"),
            value: observed ? 0 : null,
            status: observed ? "observed" : "missing",
          },
          history: [],
          baseline: null,
        });
      }),
    );
    renderApp("/");
    await userEvent.click(await screen.findByRole("button", { name: "Interroga metriche giornaliere" }));
    const catalog = screen.getByRole("region", { name: "Catalogo metriche" });
    const heartRate = within(catalog).getByRole("row", { name: /Fatica/ });
    expect(await within(heartRate).findByText("Osservato")).toBeInTheDocument();
    expect(within(heartRate).getByRole("cell", { name: "0" })).toBeInTheDocument();
    const hrv = within(catalog).getByRole("row", { name: /HRV · RMSSD/ });
    expect(await within(hrv).findByText("Non disponibile")).toBeInTheDocument();
    expect(within(hrv).getByRole("cell", { name: "—" })).toBeInTheDocument();
  });

  it("usa le serie reali della seduta e rende disponibile il cursore", async () => {
    renderApp("/cronoscopio");
    const select = await screen.findByLabelText("Seduta di riferimento");
    await screen.findByRole("option", { name: /Corsa/ });
    await userEvent.selectOptions(select, "wk1");
    expect(
      await screen.findByRole("img", { name: "Frequenza cardiaca nella finestra temporale" }),
    ).toBeInTheDocument();
    const cursor = screen.getByRole("slider");
    expect(cursor).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Frequenza cardiaca" })).toHaveTextContent("120");
    fireEvent.change(cursor, { target: { value: "60" } });
    expect(screen.getByRole("region", { name: "Frequenza cardiaca" })).toHaveTextContent(
      "Nessun campione entro 5 s",
    );
    await userEvent.selectOptions(screen.getByLabelText("Finestra temporale"), "5");
    await userEvent.click(screen.getByRole("checkbox", { name: "Frequenza cardiaca" }));
    expect(screen.queryByRole("region", { name: "Frequenza cardiaca" })).toBeNull();
  });

  it("non disegna relazioni personali prima di un calcolo", async () => {
    renderApp("/evidenze");
    expect(await screen.findByText("Nessuna relazione calcolata in questa sessione.")).toBeInTheDocument();
    expect(
      screen.queryByRole("img", { name: "Mappa delle associazioni statistiche disponibili" }),
    ).toBeNull();
  });
  it("distingue un valore dichiarato dalla sorgente wearable", async () => {
    server.use(
      http.get("*/api/v1/science/daily", () =>
        HttpResponse.json({ title: "Test unavailable" }, { status: 400 }),
      ),
      http.get("*/api/v1/science/feedback", ({ request }) =>
        HttpResponse.json({
          day: new URL(request.url).searchParams.get("day"),
          status: "recorded",
          revision: 2,
          availableAt: "2026-11-26T12:00:00Z",
          values: [{ metric: "hunger", unit: "0-10", value: 0 }],
        }),
      ),
    );
    renderApp("/");
    await userEvent.type(await screen.findByLabelText("Cerca metrica"), "hunger");
    await userEvent.click(screen.getByRole("button", { name: "Interroga metriche giornaliere" }));
    const inspector = screen.getByRole("complementary", { name: "Dettaglio metrica" });
    expect(await within(inspector).findByText("Diario dell’atleta")).toBeInTheDocument();
    expect(within(inspector).getByText("Dichiarato")).toBeInTheDocument();
    expect(within(inspector).getByText("2")).toBeInTheDocument();
  });

  it("permette di passare tra protocolli e scenari", async () => {
    renderApp("/laboratorio");
    expect(await screen.findByRole("heading", { name: "Research Lab N-of-1" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Carico longitudinale/ }));
    expect(screen.getByRole("button", { name: /Carico longitudinale/ })).toHaveAttribute(
      "aria-pressed",
      "true",
    );
    await userEvent.click(screen.getByRole("button", { name: /HRV e intervalli RR/ }));
    await userEvent.click(screen.getByRole("button", { name: /Regolarità del sonno/ }));
    await userEvent.click(screen.getByRole("button", { name: /Feedback e nutrizione/ }));
    expect(await screen.findByRole("heading", { name: "Diario giornaliero" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Analisi della seduta/ }));
    await screen.findByRole("option", { name: /Corsa/ });
    await userEvent.selectOptions(screen.getByLabelText("Seduta di riferimento"), "wk1");
    expect(await screen.findByRole("link", { name: "Apri seduta completa" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("link", { name: /Simulatore/ }));
    expect(await screen.findByRole("heading", { name: "Scenario pacing" })).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: /Previsione gara/ }));
    await userEvent.click(screen.getByRole("button", { name: /Fitness-fatigue/ }));
    await userEvent.click(screen.getByRole("button", { name: /Digital Twin/ }));
    await screen.findByRole("option", { name: /Corsa/ });
    await userEvent.selectOptions(screen.getByLabelText("Seduta di riferimento"), "wk1");
    expect(screen.getByRole("button", { name: /Digital Twin/ })).toHaveAttribute("aria-pressed", "true");
  });

  it("mostra collegamenti solo quando il server restituisce un’associazione utilizzabile", async () => {
    let valid = false;
    server.use(
      http.get("*/api/v1/science/sleep-performance", () =>
        HttpResponse.json({
          status: valid ? "descriptive-association" : "insufficient-pairs",
          pairedTests: valid ? 7 : 2,
          sleepHoursCorrelation: valid ? 0.3 : null,
          sleepEfficiencyCorrelation: null,
          evidence: [],
          methodVersion: "lagged-sleep-attested-performance-pearson-v1",
          source: "samsung-health",
          asOf: "2026-11-26T12:00:00Z",
          windowDays: 28,
          lagDays: 1,
          distanceMeters: 5000,
        }),
      ),
    );
    renderApp("/evidenze");
    await userEvent.type(await screen.findByLabelText("Sorgente prove sonno"), "samsung-health");
    await userEvent.type(screen.getByLabelText("Ultimo giorno prove UTC"), "2026-11-25");
    await userEvent.type(screen.getByLabelText("Distanza prove sonno (m)"), "5000");
    const run = screen.getByRole("button", { name: "Confronta sonno e prestazione" });
    await userEvent.click(run);
    expect(
      await screen.findByText("Nessun collegamento calcolabile per il protocollo selezionato."),
    ).toBeInTheDocument();
    valid = true;
    await userEvent.click(run);
    expect(
      await screen.findByText(
        "Associazioni descrittive restituite dal server; linee senza direzione causale.",
      ),
    ).toBeInTheDocument();
    const map = screen.getByRole("img", { name: "Mappa delle associazioni statistiche disponibili" });
    expect(within(map).getByText("r = 0,3")).toBeInTheDocument();
  });
  it("sincronizza i giorni UTC e non interpola i giorni mancanti", async () => {
    server.use(
      http.get("*/api/v1/science/daily", ({ request }) => {
        const params = new URL(request.url).searchParams;
        return HttpResponse.json({
          metric: params.get("metric"),
          source: params.get("source"),
          unit: "bpm",
          asOf: "2026-11-26T12:00:00Z",
          methodVersion: "daily-observation-median-mad-v1",
          current: { day: params.get("day"), status: "observed", value: 60, method: "Measured" },
          history: [],
        });
      }),
    );
    renderApp("/cronoscopio");
    await userEvent.click(await screen.findByRole("button", { name: "Storico giornaliero" }));
    await userEvent.click(screen.getByRole("button", { name: "Carica tracce giornaliere" }));
    const trace = await screen.findByRole("region", { name: "Frequenza cardiaca a riposo" });
    expect(trace).toHaveTextContent("60");
    fireEvent.change(screen.getByRole("slider"), { target: { value: "0" } });
    expect(trace).toHaveTextContent("Non osservato nel giorno");
    await userEvent.click(screen.getByRole("button", { name: /^Seduta$/ }));
    expect(await screen.findByLabelText("Seduta di riferimento")).toBeInTheDocument();
  });
});

describe("sorgente scientifica", () => {
  it("propone le sorgenti dell'acquisizione con Health Connect predefinita", async () => {
    renderApp("/");
    const field = await screen.findByLabelText("Sorgente scientifica");
    expect(field).toHaveValue("health-connect");
    expect(
      within(field)
        .getAllByRole("option")
        .map((option) => option.getAttribute("value")),
    ).toEqual(["health-connect", "samsung-health", "garmin-connect"]);
  });
});
