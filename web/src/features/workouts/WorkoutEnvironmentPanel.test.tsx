import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";
import type { Schemas } from "../../api/client";

describe("contesto ambientale", () => {
  it("richiede fonti esplicite e mostra solo supporto restituito", async () => {
    server.use(
      http.post("*/api/v1/workouts/:id/science/environment", async ({ request }) => {
        const body = (await request.json()) as Schemas["WorkoutEnvironmentRequest"];
        expect(body.source).toBe("weather-test");
        expect(body.protocol.heatThresholdCelsius).toBe(25);
        const selected = body.chunkIds.length > 0;
        const chunk = {
          id: "temp",
          metric: "airTemperature",
          externalId: "temp",
          revision: "r1",
          firstSample: "2026-11-25T12:00:00Z",
        };
        if (selected) {
          expect(body.chunkIds).toEqual(["temp"]);
          expect(body.protocol.weatherRepresentative).toBe(true);
        }
        return HttpResponse.json({
          weatherStatus: selected ? "observed-support" : "select-chunks",
          truncated: false,
          chunks: [chunk],
          analysis: {
            terrain: {
              supportedSeconds: 10,
              horizontalDistanceMeters: 30,
              netGradePercent: 10,
              rawAscentMeters: 3,
              rawDescentMeters: 0,
            },
            heatExposure: selected ? { aboveThresholdMinutes: 10, degreeMinutes: 50 } : null,
            metrics: [
              {
                metric: "airTemperature",
                unit: "Cel",
                coverage: selected ? 1 : 0,
                weightedMean: selected ? 30 : null,
              },
            ],
          },
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Contesto ambientale della corsa" });
    await userEvent.type(within(panel).getByLabelText("Fonte meteo"), "weather-test");
    const run = within(panel).getByRole("button", { name: "Leggi o analizza ambiente" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato meteo: select-chunks")).toBeInTheDocument();
    await userEvent.selectOptions(within(panel).getByLabelText("airTemperature", { exact: true }), "temp");
    await userEvent.click(within(panel).getByLabelText("Confermo meteo rappresentativo del percorso"));
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato meteo: observed-support")).toBeInTheDocument();
    expect(within(panel).getByText("100%")).toBeInTheDocument();
  });
});
