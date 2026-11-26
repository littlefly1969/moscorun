import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("research lab personale", () => {
  it("mostra benchmark comparabili e fold senza supporto", async () => {
    server.use(
      http.get("*/api/v1/science/research", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("penalty")).toBe("1");
        expect(query.get("distanceMeters")).toBe("5000");
        return HttpResponse.json({
          status: "partial-fold-support",
          candidateTests: 20,
          examples: [{ workoutId: "wk1" }],
          folds: [
            {
              fold: { cutoff: "2026-02-01T00:00:00Z" },
              status: "paired-retrospective",
              trainingCount: 10,
              testCount: 3,
              full: { model: { meanAbsoluteError: 0.1 }, baseline: { meanAbsoluteError: 0.3 } },
              ablated: { model: { meanAbsoluteError: 0.2 } },
            },
            {
              fold: { cutoff: "2026-02-15T00:00:00Z" },
              status: "insufficient-support",
              trainingCount: 13,
              testCount: 1,
              full: null,
              ablated: null,
            },
          ],
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Research Lab N-of-1" });
    await userEvent.type(within(panel).getByLabelText("Sorgente prove ricerca"), "samsung-health");
    await userEvent.type(within(panel).getByLabelText("Inizio storico ricerca UTC"), "2026-01-01");
    await userEvent.type(within(panel).getByLabelText("Cutoff ricerca UTC"), "2026-02-01");
    await userEvent.type(within(panel).getByLabelText("Fine ricerca esclusa UTC"), "2026-03-01");
    await userEvent.type(within(panel).getByLabelText("Distanza prove ricerca (m)"), "5000");
    await userEvent.type(within(panel).getByLabelText("Penalità L2"), "1");
    await userEvent.click(within(panel).getByRole("button", { name: "Esegui benchmark" }));
    expect(await within(panel).findByText("Stato: partial-fold-support")).toBeInTheDocument();
    expect(within(panel).getByText("0,1")).toBeInTheDocument();
    expect(within(panel).getByText("0,2")).toBeInTheDocument();
    expect(within(panel).getByText("0,3")).toBeInTheDocument();
    expect(within(panel).getByText("insufficient-support")).toBeInTheDocument();
  });
});
