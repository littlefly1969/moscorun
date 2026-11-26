import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("carico longitudinale", () => {
  it("distingue riposo confermato da dati mancanti", async () => {
    server.use(
      http.get("*/api/v1/science/load", () =>
        HttpResponse.json({
          lastDay: "2026-01-31",
          asOf: "2026-02-01T10:00:00Z",
          methodVersion: "attested-daily-session-rpe-v1",
          unit: "UA",
          days: [
            { day: "2026-01-30", status: "missing-attestation", sessions: 0, ratedSessions: 0, load: null },
            { day: "2026-01-31", status: "observed-rest", sessions: 0, ratedSessions: 0, load: 0 },
          ],
          recentWeek: null,
          rolling: null,
        }),
      ),
    );
    renderApp("/");
    const panel = await screen.findByRole("region", { name: "Carico di allenamento longitudinale" });
    await userEvent.click(within(panel).getByRole("button", { name: "Analizza carico" }));
    const rest = await within(panel).findByRole("row", { name: /2026-01-31/ });
    expect(within(rest).getByText("0", { exact: true })).toBeInTheDocument();
    expect(within(rest).getByText("Riposo confermato")).toBeInTheDocument();
    const missing = within(panel).getByRole("row", { name: /2026-01-30/ });
    expect(within(missing).getByText("—")).toBeInTheDocument();
    expect(within(panel).getByText(/Servono 35 giornate complete/)).toBeInTheDocument();
  });
});
