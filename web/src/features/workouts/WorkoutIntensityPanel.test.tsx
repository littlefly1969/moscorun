import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("intensità della seduta", () => {
  it("visualizza copertura e zone senza introdurre parametri cardiaci impliciti", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id/science/intensity", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("metric")).toBe("HeartRate");
        expect(query.get("thresholds")).toBe("130,150");
        expect(query.has("restingBpm")).toBe(false);
        return HttpResponse.json({
          revision: 1,
          methodVersion: "workout-intensity-native-support-v1",
          maxGapSeconds: 5,
          unit: "bpm",
          protocol: { upperBoundaries: [130, 150] },
          distribution: { coverage: 0.75, missingMinutes: 5, zoneMinutes: [5, 10, 0] },
          observedMean: 140,
          supportedHeartRateReserveLoad: null,
          supportedExponentialTrimp: null,
          heartRateStatus: "not-requested",
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Zone d'intensità e carico cardiaco" });
    await userEvent.type(within(panel).getByLabelText("Soglie (virgola tra valori, punto decimale)"), "130,150");
    await userEvent.click(within(panel).getByRole("button", { name: "Analizza intensità" }));
    expect(await within(panel).findByText(/Copertura temporale: 75/)).toBeInTheDocument();
    expect(within(panel).getByText("140")).toBeInTheDocument();
    expect(within(panel).getByRole("row", { name: /2 150 10/ })).toBeInTheDocument();
  });
});
