import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server, summary, weighing } from "../../test/server";
import { renderApp } from "../../test/render";
import { toChartPoints } from "./chartPoints";

describe("cruscotto", () => {
  it("mostra indicatori, sorgenti e pesate", async () => {
    renderApp("/forma");

    const latest = await screen.findByRole("region", { name: "Ultimo peso" });
    expect(latest).toHaveTextContent("78,4 kg");
    expect(screen.getByRole("region", { name: "Tendenza 7 giorni" })).toHaveTextContent("-0,6 kg");
    expect(screen.getByRole("region", { name: "Pesate ultimi 30 giorni" })).toHaveTextContent("21");
    expect(screen.getByText(/Bilancia bagno: ultima pesata ricevuta/)).toHaveTextContent(/ultimo contatto/);
    expect(screen.getAllByRole("row")).toHaveLength(3);
    expect(await screen.findByRole("figure", { name: "Andamento del peso" })).toBeInTheDocument();
  });

  it("analizza recupero e forma senza imputare giorni mancanti", async () => {
    let missing = false;
    server.use(
      http.get("*/api/v1/science/daily", ({ request }) => {
        const params = new URL(request.url).searchParams;
        expect(params.get("metric")).toBe("weight");
        expect(params.get("source")).toBe("health-connect");
        expect(params.get("baselineDays")).toBe("28");
        return HttpResponse.json({
          metric: "weight",
          unit: "kg",
          source: "samsung-health",
          asOf: "2026-11-26T12:00:00Z",
          methodVersion: "daily-observation-median-mad-v1",
          status: missing ? "missing" : "observed",
          current: {
            day: params.get("day"),
            status: missing ? "missing" : "observed",
            value: missing ? null : 79.3,
            method: missing ? null : "Measured",
          },
          history: [
            { day: "2026-11-24", status: "observed", value: 80 },
            { day: "2026-11-23", status: "missing", value: null },
          ],
          baseline: missing ? null : { baselineMedian: 80, baselineCount: 7, robustZScore: null },
          currentVersusMedianPercent: missing ? null : -0.875,
          weightTrendKgPerWeek: missing ? null : -0.7,
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Recupero e composizione corporea" });
    await userEvent.selectOptions(within(panel).getByLabelText("Metrica giornaliera"), "weight");
    const run = within(panel).getByRole("button", { name: "Analizza recupero e forma" });
    await userEvent.click(run);
    expect(await within(panel).findByText("79,3 kg")).toBeInTheDocument();
    expect(within(panel).getByText("-0,7 kg/settimana")).toBeInTheDocument();
    expect(within(panel).getByText("Non disponibile")).toBeInTheDocument();
    missing = true;
    await userEvent.click(run);
    expect(await within(panel).findByText(/Non disponibile · samsung-health/)).toBeInTheDocument();
    expect(within(panel).queryByText("79,3 kg")).not.toBeInTheDocument();
  });

  it("gestisce l'archivio vuoto senza inventare valori", async () => {
    server.use(
      http.get("*/api/v1/dashboard/summary", () =>
        HttpResponse.json({
          ...summary,
          latestWeightKg: null,
          latestMeasuredAt: null,
          trend7Days: null,
          trend30Days: null,
          weighingsLast30Days: 0,
          sources: [],
        }),
      ),
      http.get("*/api/v1/weighings", () => HttpResponse.json({ items: [], from: "x", to: "y" })),
    );
    renderApp("/forma");

    expect(await screen.findByRole("region", { name: "Ultimo peso" })).toHaveTextContent("—");
    expect(screen.getByText("nessuna pesata")).toBeInTheDocument();
    expect(screen.getByText(/Nessun dispositivo collegato/)).toBeInTheDocument();
    expect(await screen.findByText(/Servono almeno due pesate/)).toBeInTheDocument();
    expect(screen.getByText(/Nessuna pesata negli ultimi 90 giorni/)).toBeInTheDocument();
  });

  it("una pesata da confermare offre entrambe le scelte e si conferma direttamente", async () => {
    let sent: unknown;
    server.use(
      http.put("*/api/v1/weighings/w2/attribution", async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(weighing({ id: "w2", attribution: "Confirmed" }));
      }),
    );
    renderApp("/forma");

    const row = (await screen.findAllByRole("row"))[1];
    if (!row) throw new Error("riga mancante");
    expect(within(row).getByText("Da confermare")).toBeInTheDocument();
    expect(
      within(row)
        .getAllByRole("button")
        .map((b) => b.textContent),
    ).toEqual(["È mia", "Non è mia"]);
    await userEvent.click(within(row).getByRole("button", { name: "È mia" }));

    await waitFor(() => {
      expect(sent).toEqual({ attribution: "Confirmed" });
    });
  });

  it("permette di escludere una pesata non propria", async () => {
    let sent: unknown;
    server.use(
      http.put("*/api/v1/weighings/w2/attribution", async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(weighing({ id: "w2", attribution: "Rejected" }));
      }),
    );
    renderApp("/forma");

    const row = (await screen.findAllByRole("row"))[1];
    if (!row) throw new Error("riga mancante");
    await userEvent.click(within(row).getByRole("button", { name: "Non è mia" }));

    await waitFor(() => {
      expect(sent).toEqual({ attribution: "Rejected" });
    });
  });

  it("per le pesate già decise offre solo il cambio opposto", async () => {
    server.use(
      http.get("*/api/v1/weighings", () =>
        HttpResponse.json({
          items: [
            weighing({ id: "c", attribution: "Confirmed" }),
            weighing({ id: "r", attribution: "Rejected", measuredAt: "2026-11-11T06:30:00Z" }),
          ],
          from: "x",
          to: "y",
        }),
      ),
    );
    renderApp("/forma");

    const [, confirmed, rejected] = await screen.findAllByRole("row");
    if (!confirmed || !rejected) throw new Error("righe mancanti");
    expect(within(confirmed).getByText("Mia")).toBeInTheDocument();
    expect(
      within(confirmed)
        .getAllByRole("button")
        .map((b) => b.textContent),
    ).toEqual(["Non è mia"]);
    expect(within(rejected).getByText("Di un'altra persona")).toBeInTheDocument();
    expect(
      within(rejected)
        .getAllByRole("button")
        .map((b) => b.textContent),
    ).toEqual(["È mia"]);
  });

  it("la freschezza di una sorgente dipende dai dati ricevuti, non dal solo contatto", async () => {
    server.use(
      http.get("*/api/v1/dashboard/summary", () =>
        HttpResponse.json({
          ...summary,
          sources: [
            {
              deviceId: "d1",
              name: "Bilancia bagno",
              lastSeenAt: new Date().toISOString(),
              lastIngestedAt: null,
            },
          ],
        }),
      ),
    );
    renderApp("/forma");

    const source = await screen.findByText(/Bilancia bagno: ultima pesata ricevuta mai/);
    expect(source.closest("li")).toHaveAttribute("data-freshness", "missing");
  });

  it("mostra un errore se il server non risponde", async () => {
    server.use(http.get("*/api/v1/dashboard/summary", () => new HttpResponse(null, { status: 500 })));
    renderApp("/forma");
    expect(await screen.findByRole("alert")).toHaveTextContent("Impossibile caricare i dati.");
  });

  it("ordina i punti del grafico ed esclude le pesate rifiutate", () => {
    const points = toChartPoints([
      weighing({ measuredAt: "2026-11-12T06:00:00Z", weightKg: 78 }),
      weighing({ measuredAt: "2026-11-10T06:00:00Z", weightKg: 79 }),
      weighing({ measuredAt: "2026-11-11T06:00:00Z", weightKg: 55, attribution: "Rejected" }),
    ]);
    expect(points.map((p) => p.kg)).toEqual([79, 78]);
  });
});
