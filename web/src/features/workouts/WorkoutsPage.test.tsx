import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server, workout } from "../../test/server";

const googleMock = vi.hoisted(() => ({ load: vi.fn() }));
vi.mock("../../lib/googleMaps", () => ({ loadGoogleMaps: googleMock.load }));

describe("allenamenti", () => {
  beforeEach(() => {
    // Con le graffe: una funzione restituita da beforeEach verrebbe eseguita come pulizia.
    googleMock.load.mockReset();
  });

  it("elenca gli allenamenti con i valori principali e apre il dettaglio", async () => {
    const { router } = renderApp("/allenamenti");

    const row = (await screen.findAllByRole("row"))[1];
    if (!row) throw new Error("riga mancante");
    expect(row).toHaveTextContent("Corsa");
    expect(row).toHaveTextContent("50:00");
    expect(row).toHaveTextContent("10,0 km");
    expect(row).toHaveTextContent("5:00 /km");
    expect(row).toHaveTextContent("142 bpm");
    await userEvent.click(within(row).getByRole("link"));
    expect(router.state.location.pathname).toBe("/allenamenti/wk1");
    expect(await screen.findByRole("heading", { level: 1 })).toHaveTextContent("Corsa");
  });

  it("mostra le metriche scientifiche senza trasformare i canali mancanti in zero", async () => {
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Analisi scientifica delle serie" });
    expect(await within(panel).findByText("Non acquisita")).toBeInTheDocument();
    expect(within(panel).getByText("140 bpm")).toBeInTheDocument();
  });

  it("senza allenamenti spiega come inviarli dal telefono", async () => {
    server.use(http.get("*/api/v1/workouts", () => HttpResponse.json({ items: [], from: "x", to: "y" })));
    renderApp("/allenamenti");

    expect(await screen.findByText(/Invia allenamenti al server/)).toBeInTheDocument();
  });

  it("senza chiave Google disegna il percorso in locale senza contattare Google", async () => {
    renderApp("/allenamenti/wk1");

    expect(await screen.findByRole("img", { name: /Percorso dell'allenamento/ })).toBeInTheDocument();
    expect(screen.getByText(/Cartografia non configurata/)).toBeInTheDocument();
    expect(await screen.findByRole("figure", { name: /Frequenza cardiaca/ })).toBeInTheDocument();
    expect(googleMock.load).not.toHaveBeenCalled();
  });

  it("con Google configurato disegna il tracciato sulla mappa", async () => {
    server.use(
      http.get("*/api/v1/maps/config", () =>
        HttpResponse.json({ provider: "google", googleApiKey: "AIza-test" }),
      ),
    );
    const polyline = vi.fn();
    const fitBounds = vi.fn();
    googleMock.load.mockResolvedValue({
      Map: vi.fn(function () {
        return { fitBounds };
      }),
      MapTypeId: { ROADMAP: "roadmap" },
      LatLngBounds: vi.fn(function () {
        return { extend: vi.fn() };
      }),
      Polyline: polyline,
      SymbolPath: { CIRCLE: 0 },
    });
    renderApp("/allenamenti/wk1");

    const map = await screen.findByRole("region", { name: "Mappa del percorso" });
    await vi.waitFor(() => {
      expect(polyline).toHaveBeenCalledTimes(1);
    });
    expect(googleMock.load).toHaveBeenCalledWith("AIza-test");
    expect(map).toHaveAttribute("aria-busy", "false");
    expect(fitBounds).toHaveBeenCalled();
    const options = polyline.mock.calls[0]?.[0] as { path: { lat: number; lng: number }[] };
    expect(options.path[0]).toEqual({ lat: 41.89, lng: 12.49 });
  });

  it("se Google rifiuta la chiave mostra il percorso senza mappa e il motivo", async () => {
    server.use(
      http.get("*/api/v1/maps/config", () =>
        HttpResponse.json({ provider: "google", googleApiKey: "AIza-test" }),
      ),
    );
    googleMock.load.mockRejectedValue(new Error("Chiave Google Maps rifiutata."));
    renderApp("/allenamenti/wk1");

    expect(await screen.findByText(/Chiave Google Maps rifiutata/)).toBeInTheDocument();
    expect(screen.getByRole("img", { name: /Percorso dell'allenamento/ })).toBeInTheDocument();
  });

  it("allenamento senza GPS", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id", () =>
        HttpResponse.json({ summary: workout({ trackPointCount: 0, bounds: null }), series: [] }),
      ),
    );
    renderApp("/allenamenti/wk1");
    expect(await screen.findByText(/non ha un percorso GPS/)).toBeInTheDocument();
  });

  it("allenamento inesistente", async () => {
    renderApp("/allenamenti/altro");
    expect(await screen.findByRole("alert")).toHaveTextContent("Allenamento non trovato.");
  });

  it("segnala partenza e arrivo nascosti dalle zone private", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id/route", () =>
        HttpResponse.json({
          type: "Feature",
          bbox: [12.49, 41.89, 12.5, 41.9],
          geometry: {
            type: "LineString",
            coordinates: [
              [12.49, 41.89],
              [12.5, 41.9],
            ],
          },
          properties: {
            workoutId: "wk1",
            totalPoints: 10,
            returnedPoints: 2,
            hiddenStartPoints: 5,
            hiddenEndPoints: 3,
            times: [1, 2],
          },
        }),
      ),
    );
    renderApp("/allenamenti/wk1");
    expect(await screen.findByText(/Partenza e arrivo nascosti/)).toBeInTheDocument();
  });
});
