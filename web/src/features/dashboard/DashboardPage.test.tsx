import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server, summary, weighing } from "../../test/server";
import { renderApp } from "../../test/render";
import { toChartPoints } from "./chartPoints";

describe("cruscotto", () => {
  it("mostra indicatori, sorgenti e pesate", async () => {
    renderApp("/");

    const latest = await screen.findByRole("region", { name: "Ultimo peso" });
    expect(latest).toHaveTextContent("78,4 kg");
    expect(screen.getByRole("region", { name: "Tendenza 7 giorni" })).toHaveTextContent("-0,6 kg");
    expect(screen.getByRole("region", { name: "Pesate ultimi 30 giorni" })).toHaveTextContent("21");
    expect(screen.getByText(/Bilancia bagno: ultimo contatto/)).toBeInTheDocument();
    expect(screen.getAllByRole("row")).toHaveLength(3);
    expect(await screen.findByRole("figure", { name: "Andamento del peso" })).toBeInTheDocument();
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
    renderApp("/");

    expect(await screen.findByRole("region", { name: "Ultimo peso" })).toHaveTextContent("—");
    expect(screen.getByText("nessuna pesata")).toBeInTheDocument();
    expect(screen.getByText(/Nessun dispositivo collegato/)).toBeInTheDocument();
    expect(await screen.findByText(/Servono almeno due pesate/)).toBeInTheDocument();
    expect(screen.getByText(/Nessuna pesata negli ultimi 90 giorni/)).toBeInTheDocument();
  });

  it("permette di escludere una pesata non propria", async () => {
    let sent: unknown;
    server.use(
      http.put("*/api/v1/weighings/w2/attribution", async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(weighing({ id: "w2", attribution: "Rejected" }));
      }),
    );
    renderApp("/");

    const row = (await screen.findAllByRole("row"))[1];
    if (!row) throw new Error("riga mancante");
    await userEvent.click(within(row).getByRole("button", { name: "Non è mia" }));

    await waitFor(() => {
      expect(sent).toEqual({ attribution: "Rejected" });
    });
  });

  it("mostra un errore se il server non risponde", async () => {
    server.use(http.get("*/api/v1/dashboard/summary", () => new HttpResponse(null, { status: 500 })));
    renderApp("/");
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
