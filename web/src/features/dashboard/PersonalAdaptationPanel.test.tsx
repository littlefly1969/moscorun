import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("adattamento personale", () => {
  it("mostra la calibrazione, la baseline e le prove, senza inventare i mancanti", async () => {
    let calibrated = false;
    server.use(
      http.get("*/api/v1/science/adaptation", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("distanceMeters")).toBe("5000");
        expect(query.get("from")).toBe("2026-01-01");
        expect(query.get("split")).toBe("2026-02-01");
        expect(query.get("until")).toBe("2026-03-01");
        return HttpResponse.json({
          status: calibrated ? "retrospective-holdout" : "insufficient-causal-training",
          loads: [{ load: 0 }, { load: null }],
          model: calibrated
            ? { trainingCount: 6, parameters: { fitnessTimeConstantDays: 28, fatigueTimeConstantDays: 7 } }
            : null,
          holdout: calibrated ? { meanAbsoluteError: 0.1 } : null,
          baselineHoldout: calibrated ? { meanAbsoluteError: 0.2 } : null,
          currentPredictedSpeedMetersPerSecond: calibrated ? 4.2 : null,
          tests: [
            {
              workoutId: "wk1",
              startedAt: "2026-02-10T12:00:00Z",
              role: "calibration",
              observedSpeedMetersPerSecond: 4.1,
              predictedSpeedMetersPerSecond: calibrated ? 4.2 : null,
            },
          ],
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Adattamento fitness-fatigue" });
    await userEvent.type(within(panel).getByLabelText("Sorgente prove"), "samsung-health");
    await userEvent.type(within(panel).getByLabelText("Inizio storico UTC"), "2026-01-01");
    await userEvent.type(within(panel).getByLabelText("Cutoff addestramento UTC"), "2026-02-01");
    await userEvent.type(within(panel).getByLabelText("Fine holdout esclusa UTC"), "2026-03-01");
    await userEvent.type(within(panel).getByLabelText("Distanza delle prove (m)"), "5000");
    const run = within(panel).getByRole("button", { name: "Calibra adattamento" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: insufficient-causal-training")).toBeInTheDocument();
    expect(within(panel).getByText("Giorni con carico attestato: 1/2")).toBeInTheDocument();
    calibrated = true;
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: retrospective-holdout")).toBeInTheDocument();
    expect(within(panel).getByText("0,1")).toBeInTheDocument();
    expect(within(panel).getByText("0,2")).toBeInTheDocument();
    expect(within(panel).getByText("28")).toBeInTheDocument();
  });
});
