import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("dinamiche estese", () => {
  it("seleziona revisioni e mostra copertura effettiva", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id/science/biomechanics", ({ request }) => {
        const query = new URL(request.url).searchParams;
        const chosen = query.has("chunkIds");
        if (chosen) {
          expect(query.get("chunkIds")).toBe("chunk-cad");
          expect(query.get("cadenceIsStepsPerMinute")).toBe("true");
        }
        const chunk = {
          id: "chunk-cad",
          metric: "cadence",
          externalId: "cad",
          revision: "r1",
          firstSample: "2026-11-25T12:00:00Z",
        };
        return HttpResponse.json({
          status: chosen ? "observed" : "select-chunks",
          selected: chosen ? [chunk] : [],
          chunks: [chunk],
          truncated: false,
          metrics: chosen
            ? [{ metric: "dutyFactor", unit: "%", coverage: 0.5, weightedMean: 30, weightedCvPercent: null }]
            : [],
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Dinamiche di corsa estese" });
    await userEvent.type(within(panel).getByLabelText("Sorgente dinamiche"), "garmin-test");
    const run = within(panel).getByRole("button", { name: "Leggi o analizza dinamiche" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: select-chunks")).toBeInTheDocument();
    await userEvent.selectOptions(within(panel).getByLabelText("cadence", { exact: true }), "chunk-cad");
    await userEvent.click(within(panel).getByLabelText("Cadenza in passi totali al minuto"));
    await userEvent.click(run);
    expect(await within(panel).findByText("dutyFactor")).toBeInTheDocument();
    expect(within(panel).getByText("50%")).toBeInTheDocument();
  });
});
