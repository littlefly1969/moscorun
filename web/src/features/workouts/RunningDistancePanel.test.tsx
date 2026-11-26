import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("variazione distanza", () => {
  it("distingue confronto valido da storia non completa", async () => {
    let complete = false;
    server.use(
      http.get("*/api/v1/workouts/:id/science/distance-load", () =>
        HttpResponse.json({
          status: complete ? "observed" : "incomplete-prior-history",
          completeDays: complete ? 30 : 29,
          current: { distanceMeters: 12000 },
          priorLongestMeters: complete ? 8000 : null,
          increasePercent: complete ? 50 : null,
          days: [{ day: "2026-11-24", status: "observed-rest", totalMeters: 0, longestMeters: null }],
        }),
      ),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Variazione della distanza di corsa" });
    const run = within(panel).getByRole("button", { name: "Confronta distanza" });
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: incomplete-prior-history")).toBeInTheDocument();
    complete = true;
    await userEvent.click(run);
    expect(await within(panel).findByText("Stato: observed")).toBeInTheDocument();
    expect(within(panel).getByText("50")).toBeInTheDocument();
  });
});
