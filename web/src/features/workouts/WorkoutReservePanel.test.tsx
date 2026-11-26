import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { renderApp } from "../../test/render";
import { server } from "../../test/server";

describe("replay riserva", () => {
  it("usa parametri espliciti e mostra la fine del supporto", async () => {
    server.use(
      http.get("*/api/v1/workouts/:id/science/reserve", ({ request }) => {
        const query = new URL(request.url).searchParams;
        expect(query.get("metric")).toBe("Power");
        expect(query.get("criticalIntensity")).toBe("300");
        expect(query.get("capacity")).toBe("1000");
        expect(query.get("initialRemaining")).toBe("1000");
        expect(query.get("recoveryTimeConstantSeconds")).toBe("60");
        return HttpResponse.json({
          status: "truncated-at-gap",
          supportedSeconds: 2,
          workoutDurationSeconds: 10,
          unit: "J",
          minimumRemaining: 800,
          lastRemaining: 800,
          exhaustionSeconds: null,
          points: [
            { cumulativeSeconds: 0, remaining: 1000 },
            { cumulativeSeconds: 2, remaining: 800 },
          ],
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Replay riserva critica D′ / W′" });
    await userEvent.selectOptions(within(panel).getByLabelText("Canale"), "Power");
    await userEvent.type(within(panel).getByLabelText("Intensità critica calibrata"), "300");
    await userEvent.type(within(panel).getByLabelText("Capacità calibrata"), "1000");
    await userEvent.type(within(panel).getByLabelText("Riserva iniziale ipotizzata"), "1000");
    await userEvent.type(within(panel).getByLabelText("Costante di recupero calibrata (s)"), "60");
    await userEvent.click(within(panel).getByRole("button", { name: "Simula riserva" }));
    expect(await within(panel).findByText("Stato: truncated-at-gap")).toBeInTheDocument();
    expect(within(panel).getByText("Supporto continuo: 2 / 10 s")).toBeInTheDocument();
  });
});
