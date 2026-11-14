import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { device, server } from "../../test/server";
import { renderApp } from "../../test/render";

describe("dispositivi", () => {
  it("elenca i dispositivi", async () => {
    server.use(
      http.get("*/api/v1/devices", () =>
        HttpResponse.json([
          device({ name: "Bilancia" }),
          device({ name: "Vecchio", revokedAt: "2026-11-02T00:00:00Z" }),
        ]),
      ),
    );
    renderApp("/dispositivi");
    expect(await screen.findByText("Bilancia")).toBeInTheDocument();
    expect(screen.getByText("revocato")).toBeInTheDocument();
  });

  it("registra un dispositivo e mostra il token una sola volta", async () => {
    let body: unknown;
    server.use(
      http.post("*/api/v1/devices", async ({ request }) => {
        body = await request.json();
        return HttpResponse.json(
          { device: device({ name: "Telefono" }), token: "mr1_abc_secret" },
          { status: 201 },
        );
      }),
    );
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, "clipboard", { value: { writeText }, configurable: true });
    renderApp("/dispositivi");

    await userEvent.type(await screen.findByLabelText("Nome"), "Telefono");
    await userEvent.selectOptions(screen.getByLabelText("Tipo"), "PhoneBridge");
    await userEvent.click(screen.getByRole("button", { name: "Crea e genera token" }));

    expect(await screen.findByText("mr1_abc_secret")).toBeInTheDocument();
    expect(body).toEqual({ name: "Telefono", kind: "PhoneBridge" });

    await userEvent.click(screen.getByRole("button", { name: "Copia" }));
    expect(writeText).toHaveBeenCalledWith("mr1_abc_secret");
    expect(await screen.findByRole("button", { name: "Copiato" })).toBeInTheDocument();

    await userEvent.click(screen.getByRole("button", { name: "Ho salvato il token" }));
    expect(screen.queryByText("mr1_abc_secret")).not.toBeInTheDocument();
  });

  it("mostra l'errore di registrazione", async () => {
    server.use(
      http.post("*/api/v1/devices", () =>
        HttpResponse.json({ title: "Errore", detail: "Nome non valido" }, { status: 400 }),
      ),
    );
    renderApp("/dispositivi");
    await userEvent.type(await screen.findByLabelText("Nome"), "x");
    await userEvent.click(screen.getByRole("button", { name: "Crea e genera token" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Nome non valido");
  });

  it("chiede conferma prima di revocare", async () => {
    let revoked = false;
    server.use(
      http.get("*/api/v1/devices", () => HttpResponse.json([device({ id: "d9", name: "Bilancia" })])),
      http.delete("*/api/v1/devices/d9", () => {
        revoked = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    renderApp("/dispositivi");

    await userEvent.click(await screen.findByRole("button", { name: "Revoca" }));
    expect(revoked).toBe(false);
    await userEvent.click(screen.getByRole("button", { name: "Conferma revoca" }));
    await waitFor(() => {
      expect(revoked).toBe(true);
    });
  });

  it("segnala elenco vuoto ed errori", async () => {
    server.use(http.get("*/api/v1/devices", () => HttpResponse.json([])));
    renderApp("/dispositivi");
    expect(await screen.findByText("Nessun dispositivo.")).toBeInTheDocument();
  });
});
