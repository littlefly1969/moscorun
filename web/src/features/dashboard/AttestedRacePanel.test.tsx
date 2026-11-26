import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("calibrazione personale", () => {
  it("invia una finestra esplicita e non mostra previsioni senza prove", async () => {
    server.use(
      http.get("*/api/v1/science/race-calibration", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("from")).toBe("2026-01-01");
        expect(query.get("split")).toBe("2026-02-01");
        expect(query.get("until")).toBe("2026-03-01");
        expect(query.get("targetMeters")).toBe("5000");
        return HttpResponse.json({
          protocol: "flat-course-maximal-elapsed-v1",
          methodVersion: "personal-riegel-theil-sen-v1",
          status: "insufficient-training",
          model: null,
          prediction: null,
          interval: null,
          evidence: [],
          performance: {
            speedMethodVersion: "distance-time-ols-v2",
            robustSpeedMethodVersion: "distance-time-theil-sen-v1",
            cooperMethodVersion: "cooper-12min-field-estimate-v1",
            ordinaryLeastSquares: null,
            robustCriticalSpeed: null,
            cooperTests: [],
            criticalSpeedCandidateIds: [],
            powerMethodVersion: "work-time-ols-v1",
            criticalPower: null,
            criticalPowerCandidateIds: [],
          },
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Calibrazione prestazionale personale" });
    await userEvent.type(within(panel).getByLabelText("Inizio fit UTC"), "2026-01-01");
    await userEvent.type(within(panel).getByLabelText("Cutoff fit UTC"), "2026-02-01");
    await userEvent.type(within(panel).getByLabelText("Fine calibrazione esclusa UTC"), "2026-03-01");
    await userEvent.type(within(panel).getByLabelText("Distanza obiettivo (m)"), "5000");
    await userEvent.click(within(panel).getByRole("button", { name: "Calibra prestazione" }));
    expect(
      await within(panel).findByText("Prove massimali disponibili al cutoff insufficienti."),
    ).toBeInTheDocument();
  });
});
