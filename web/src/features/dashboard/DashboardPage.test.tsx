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
    expect(screen.getByText(/Bilancia bagno: ultima pesata ricevuta/)).toHaveTextContent(/ultimo contatto/);
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

  it("una pesata da confermare offre entrambe le scelte e si conferma direttamente", async () => {
    let sent: unknown;
    server.use(
      http.put("*/api/v1/weighings/w2/attribution", async ({ request }) => {
        sent = await request.json();
        return HttpResponse.json(weighing({ id: "w2", attribution: "Confirmed" }));
      }),
    );
    renderApp("/");

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
    renderApp("/");

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
    renderApp("/");

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
    renderApp("/");

    const source = await screen.findByText(/Bilancia bagno: ultima pesata ricevuta mai/);
    expect(source.closest("li")).toHaveAttribute("data-freshness", "missing");
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
