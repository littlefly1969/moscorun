import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("associazione sonno e prestazione", () => {
  it("mostra coppie valide e mancanti senza attribuzione causale", async () => {
    server.use(
      http.get("*/api/v1/science/sleep-performance", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("lag")).toBe("1");
        expect(query.get("distanceMeters")).toBe("5000");
        return HttpResponse.json({
          status: "descriptive-association",
          pairedTests: 7,
          sleepHoursCorrelation: 0.3,
          sleepEfficiencyCorrelation: null,
          evidence: [
            {
              workoutId: "wk1",
              testStartedAt: "2026-11-20T12:00:00Z",
              sleepDay: "2026-11-19",
              status: "paired",
              sleepHours: 7.5,
              speedMetersPerSecond: 4,
            },
            {
              workoutId: "wk2",
              testStartedAt: "2026-11-21T12:00:00Z",
              sleepDay: "2026-11-20",
              status: "late-night-revision",
              sleepHours: null,
              speedMetersPerSecond: 4.1,
            },
          ],
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Sonno e prestazione" });
    await userEvent.type(within(panel).getByLabelText("Sorgente prove sonno"), "samsung-health");
    await userEvent.type(within(panel).getByLabelText("Ultimo giorno prove UTC"), "2026-11-25");
    await userEvent.type(within(panel).getByLabelText("Distanza prove sonno (m)"), "5000");
    await userEvent.click(within(panel).getByRole("button", { name: "Confronta sonno e prestazione" }));
    expect(await within(panel).findByText("0,3")).toBeInTheDocument();
    expect(within(panel).getByText("late-night-revision")).toBeInTheDocument();
  });
});
