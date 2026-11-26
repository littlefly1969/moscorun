import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";
import type { Schemas } from "../../api/client";

describe("digital twin cardiaco", () => {
  it("trasmette protocollo e ipotesi e visualizza soltanto la simulazione restituita", async () => {
    server.use(
      http.post("*/api/v1/workouts/:id/science/twin", async ({ request }) => {
        const body = (await request.json()) as Schemas["CalibratedTwinRequest"];
        expect(body.recoveryProtocol.endOffsetSeconds).toBe(30);
        expect(body.segments).toEqual([
          { durationSeconds: 30, externalSpeedMetersPerSecond: 4, equilibriumHeartRateBpm: 150 },
        ]);
        const confirmed = body.transferAssumptionConfirmed;
        return HttpResponse.json({
          status: confirmed ? "conditional-simulation" : "unconfirmed-transfer",
          parameters: confirmed ? { heartRateTimeConstantSeconds: 30 } : null,
          calibration: { fit: { rootMeanSquaredErrorBpm: 1 } },
          segments: confirmed
            ? [{ cumulativeSeconds: 30, cumulativeMeters: 120, endHeartRateBpm: 132, meanHeartRateBpm: 118 }]
            : [],
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Digital Twin cardiaco condizionale" });
    await userEvent.type(within(panel).getByLabelText("Fine sforzo per calibrazione (s)"), "30");
    await userEvent.type(within(panel).getByLabelText("Equilibrio recupero dichiarato (bpm)"), "70");
    await userEvent.type(within(panel).getByLabelText("FC iniziale scenario (bpm)"), "100");
    await userEvent.type(within(panel).getByLabelText("FC riposo (bpm)"), "50");
    await userEvent.type(within(panel).getByLabelText("FC massima (bpm)"), "190");
    await userEvent.type(within(panel).getByLabelText("RMSE massimo accettato (bpm)"), "3");
    await userEvent.click(within(panel).getByLabelText("Confermo la modalità costante di calibrazione"));
    await userEvent.type(
      within(panel).getByLabelText("Segmenti: secondi;m/s;FC equilibrio (uno per riga)"),
      "30;4;150",
    );
    const run = within(panel).getByRole("button", { name: "Simula risposta cardiaca" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: unconfirmed-transfer")).toBeInTheDocument();
    await userEvent.click(within(panel).getByLabelText("Confermo ipotesi di trasferimento della tau"));
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: conditional-simulation")).toBeInTheDocument();
    expect(within(panel).getByRole("row", { name: /30 120 132 118/ })).toBeInTheDocument();
  });
});
