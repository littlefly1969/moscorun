import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import type { Schemas } from "../../api/client";
import { renderApp } from "../../test/render";
import { missingNutrition, server } from "../../test/server";

describe("feedback scientifico", () => {
  it("salva lo zero nel diario come misura e non come dato mancante", async () => {
    let sent: Schemas["ScienceFeedbackRequest"] | undefined;
    server.use(
      http.put("*/api/v1/science/feedback", async ({ request }) => {
        sent = (await request.json()) as Schemas["ScienceFeedbackRequest"];
        return HttpResponse.json({
          ...sent,
          availableAt: "2026-11-26T10:00:00Z",
          values: sent.values.map((value) => ({ ...value, unit: "0-10" })),
          status: "recorded",
          activeDurationSeconds: null,
          sessionRpeLoad: null,
          sessionLoadMethodVersion: "session-rpe-active-minutes-v1",
          nutrition: missingNutrition,
        });
      }),
    );
    renderApp("/forma");
    const panel = await screen.findByRole("region", { name: "Diario giornaliero" });
    await userEvent.type(await within(panel).findByLabelText("Fatica (0-10)"), "0");
    await userEvent.type(within(panel).getByLabelText("Note"), "Giornata tranquilla");
    await userEvent.click(within(panel).getByRole("button", { name: "Salva feedback" }));
    expect(await within(panel).findByText("Revisione salvata: 1.")).toBeInTheDocument();
    expect(sent?.values).toEqual([{ metric: "fatigue", value: 0 }]);
    expect(sent?.note).toBe("Giornata tranquilla");
    expect(sent?.workoutId).toBeNull();
    expect(within(panel).getByText("Diario non confermato completo")).toBeInTheDocument();
  });

  it("salva RPE per la revisione della seduta e mostra il carico restituito dal server", async () => {
    let conflict = false;
    server.use(
      http.put("*/api/v1/science/feedback", async ({ request }) => {
        const body = (await request.json()) as Schemas["ScienceFeedbackRequest"];
        expect(body.workoutId).toBe("wk1");
        expect(body.workoutRevision).toBe(1);
        if (conflict) return new HttpResponse(null, { status: 409 });
        expect(body.values).toEqual([{ metric: "rpe", value: 7 }]);
        return HttpResponse.json({
          ...body,
          availableAt: "2026-11-26T10:00:00Z",
          values: body.values.map((value) => ({ ...value, unit: "0-10" })),
          status: "recorded",
          activeDurationSeconds: 3000,
          sessionRpeLoad: 350,
          sessionLoadMethodVersion: "session-rpe-active-minutes-v1",
          nutrition: missingNutrition,
        });
      }),
    );
    renderApp("/allenamenti/wk1");
    const panel = await screen.findByRole("region", { name: "Feedback della seduta" });
    await userEvent.type(await within(panel).findByLabelText("Sforzo percepito RPE (0-10)"), "7");
    await userEvent.click(within(panel).getByRole("button", { name: "Salva feedback" }));
    expect(await within(panel).findByText("350")).toBeInTheDocument();
    conflict = true;
    await userEvent.click(within(panel).getByRole("button", { name: "Salva feedback" }));
    expect(await within(panel).findByRole("alert")).toHaveTextContent("Revisione cambiata");
  });
});
