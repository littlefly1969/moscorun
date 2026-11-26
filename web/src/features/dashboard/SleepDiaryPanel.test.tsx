import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("regolarità sonno", () => {
  it("rende orari circolari e giorni mancanti senza imputazione", async () => {
    server.use(
      http.get("*/api/v1/science/sleep", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("last")).toBe("2026-11-25");
        expect(query.get("days")).toBe("28");
        return HttpResponse.json({
          status: "observed",
          completeNights: 7,
          windowDays: 28,
          bedtime: { meanMinuteOfDay: 1439.8, circularStandardDeviationMinutes: 10 },
          wake: { meanMinuteOfDay: 480, circularStandardDeviationMinutes: 20 },
          meanEfficiencyPercent: 87.5,
          nights: [
            {
              day: "2026-11-25",
              status: "unconfirmed",
              bedtimeMinute: null,
              wakeMinute: null,
              efficiencyPercent: null,
            },
          ],
        });
      }),
    );
    renderApp("/");
    const panel = await screen.findByRole("region", { name: "Regolarità del sonno" });
    await userEvent.type(within(panel).getByLabelText("Ultimo giorno sonno UTC"), "2026-11-25");
    await userEvent.click(within(panel).getByRole("button", { name: "Analizza sonno" }));
    expect(await within(panel).findByText("00:00")).toBeInTheDocument();
    expect(within(panel).getByText("08:00")).toBeInTheDocument();
    expect(within(panel).getByText("87,5")).toBeInTheDocument();
    expect(within(panel).getByText("unconfirmed")).toBeInTheDocument();
  });
});
