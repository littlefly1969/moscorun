import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("biomeccanica accoppiata", () => {
  it("richiede conferma dell'unità e mostra solo risultati supportati", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id/science/steps", ({ request }) => {
        const confirmed = new URL(request.url).searchParams.get("cadenceIsStepsPerMinute") === "true";
        return HttpResponse.json({
          revision: 1,
          methodVersion: "running-step-native-paired-zoh-v1",
          maxGapSeconds: 5,
          status: confirmed ? "observed" : "unconfirmed-cadence-unit",
          coverage: confirmed ? 0.4 : 0,
          meanSpeedMetersPerSecond: confirmed ? 3.625 : null,
          meanCadenceStepsPerMinute: confirmed ? 161.25 : null,
          meanStepLengthMeters: confirmed ? 1.35 : null,
          stepLengthCvPercent: null,
          cadenceCvPercent: null,
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Biomeccanica: passo e cadenza" });
    const run = within(panel).getByRole("button", { name: "Analizza biomeccanica" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Unità della cadenza non confermata")).toBeInTheDocument();
    await userEvent.click(within(panel).getByRole("checkbox"));
    await userEvent.click(run);
    expect(await within(panel).findByText("1,35")).toBeInTheDocument();
    expect(within(panel).getByText("40%")).toBeInTheDocument();
  });
});
