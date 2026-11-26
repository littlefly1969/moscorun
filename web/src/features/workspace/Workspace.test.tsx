import { screen, within } from "@testing-library/react";
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
    expect(metricCatalog).toHaveLength(100);
    await userEvent.type(screen.getByLabelText("Cerca metrica"), "groundContactTime");
    expect(within(catalog).getAllByRole("row")).toHaveLength(2);
    await userEvent.click(within(catalog).getByRole("button", { name: "Tempo di contatto a terra" }));
    expect(screen.getByRole("complementary", { name: "Dettaglio metrica" })).toHaveTextContent(
      "Lettura non collegata",
    );
    await userEvent.click(within(catalog).getByRole("button", { name: "Preferita: Tempo di contatto a terra" }));
    await userEvent.clear(screen.getByLabelText("Cerca metrica"));
    await userEvent.click(screen.getByLabelText("Solo preferite (sessione)"));
    expect(within(catalog).getAllByRole("row")).toHaveLength(2);
  });

  it("interroga metriche giornaliere senza imputare i dati mancanti", async () => {
    server.use(
      http.get("*/api/v1/science/daily", ({ request }) => {
        const query = new URL(request.url).searchParams;
        const metric = query.get("metric");
        const observed = metric === "restingHeartRate";
        expect(query.get("source")).toBe("samsung-health");
        return HttpResponse.json({
          metric,
          unit: "bpm",
          source: "samsung-health",
          methodVersion: "daily-observation-median-mad-v1",
          asOf: "2026-11-26T12:00:00Z",
          status: observed ? "observed" : "missing",
          current: { day: query.get("day"), value: observed ? 0 : null, status: observed ? "observed" : "missing" },
          history: [],
          baseline: null,
        });
      }),
    );
    renderApp("/");
    await userEvent.click(await screen.findByRole("button", { name: "Interroga metriche giornaliere" }));
    const catalog = screen.getByRole("region", { name: "Catalogo metriche" });
    const heartRate = within(catalog).getByRole("row", { name: /Frequenza cardiaca a riposo/ });
    expect(await within(heartRate).findByText("Osservato")).toBeInTheDocument();
    expect(within(heartRate).getByRole("cell", { name: "0", exact: true })).toBeInTheDocument();
    const hrv = within(catalog).getByRole("row", { name: /HRV · RMSSD/ });
    expect(await within(hrv).findByText("Non disponibile")).toBeInTheDocument();
    expect(within(hrv).getByRole("cell", { name: "—" })).toBeInTheDocument();
  });

  it("usa le serie reali della seduta e rende disponibile il cursore", async () => {
    renderApp("/cronoscopio");
    const select = await screen.findByLabelText("Seduta di riferimento");
    await screen.findByRole("option", { name: /Corsa/ });
    await userEvent.selectOptions(select, "wk1");
    expect(await screen.findByRole("img", { name: "Frequenza cardiaca nella finestra temporale" })).toBeInTheDocument();
    expect(screen.getByRole("slider")).toBeInTheDocument();
    expect(screen.getByRole("region", { name: "Frequenza cardiaca" })).toHaveTextContent("120");
  });

  it("non disegna relazioni personali prima di un calcolo", async () => {
    renderApp("/evidenze");
    expect(await screen.findByText("Nessuna relazione calcolata in questa sessione.")).toBeInTheDocument();
    expect(screen.queryByRole("img", { name: "Mappa delle associazioni statistiche disponibili" })).toBeNull();
  });
});
