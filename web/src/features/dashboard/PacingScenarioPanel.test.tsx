import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";
import type { Schemas } from "../../api/client";

describe("scenario pacing", () => {
  it("allinea il tempo dichiarato o personale ai segmenti senza fallback implicito", async () => {
    server.use(
      http.post("*/api/v1/science/pacing", async ({ request }) => {
        const body = (await request.json()) as Schemas["PacingScenarioRequest"];
        expect(body.segments).toEqual([
          { distanceMeters: 1000, relativeTimeCost: 1 },
          { distanceMeters: 1000, relativeTimeCost: 1.5 },
        ]);
        if (body.raceReference) {
          expect(body.targetSeconds).toBeNull();
          expect(body.raceReference.source).toBe("samsung-health");
          return HttpResponse.json({
            status: "insufficient-calibration",
            distanceMeters: 2000,
            targetSeconds: null,
            targetSource: "personal-riegel",
            interval: null,
            splits: [],
          });
        }
        expect(body.targetSeconds).toBe(600);
        return HttpResponse.json({
          status: "conditional-scenario",
          distanceMeters: 2000,
          targetSeconds: 600,
          targetSource: "declared-target",
          interval: null,
          splits: [
            { distanceMeters: 1000, segmentSeconds: 240, cumulativeSeconds: 240 },
            { distanceMeters: 1000, segmentSeconds: 360, cumulativeSeconds: 600 },
          ],
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Scenario pacing" });
    await userEvent.type(
      within(panel).getByLabelText("Segmenti: metri;costo relativo (uno per riga)"),
      "1000;1\n1000;1.5",
    );
    await userEvent.type(within(panel).getByLabelText("Tempo obiettivo (s)"), "600");
    await userEvent.click(within(panel).getByRole("button", { name: "Simula pacing" }));
    expect(await within(panel).findByText("Stato: conditional-scenario")).toBeInTheDocument();
    expect(within(panel).getByRole("row", { name: /2 1000 360 600 360/ })).toBeInTheDocument();
    await userEvent.click(within(panel).getByLabelText("Usa previsione personale"));
    await userEvent.type(within(panel).getByLabelText("Sorgente prove pacing"), "samsung-health");
    await userEvent.type(within(panel).getByLabelText("Inizio fit pacing UTC"), "2026-01-01");
    await userEvent.type(within(panel).getByLabelText("Cutoff fit pacing UTC"), "2026-02-01");
    await userEvent.type(within(panel).getByLabelText("Fine calibrazione pacing UTC"), "2026-03-01");
    await userEvent.click(within(panel).getByRole("button", { name: "Simula pacing" }));
    expect(await within(panel).findByText("Stato: insufficient-calibration")).toBeInTheDocument();
  });
});
