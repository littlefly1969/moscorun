import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { http, HttpResponse } from "msw";
import { server } from "../../test/server";
import { renderApp } from "../../test/render";

const anonymous = http.get("*/api/auth/me", () => new HttpResponse(null, { status: 401 }));

describe("accesso", () => {
  it("reindirizza al login chi non è autenticato", async () => {
    server.use(anonymous);
    const { router } = renderApp("/dispositivi");
    expect(await screen.findByRole("heading", { name: "MoscoRun" })).toBeInTheDocument();
    expect(router.state.location.pathname).toBe("/accesso");
  });

  it("accede e torna alla pagina richiesta", async () => {
    let loggedIn = false;
    let body: unknown;
    server.use(
      http.get("*/api/auth/me", () =>
        loggedIn
          ? HttpResponse.json({ id: "u1", email: "stefano@example.it", displayName: "Stefano" })
          : new HttpResponse(null, { status: 401 }),
      ),
      http.post("*/api/auth/login", async ({ request }) => {
        body = await request.json();
        loggedIn = true;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { router } = renderApp("/dispositivi");

    await userEvent.type(await screen.findByLabelText("Email"), "stefano@example.it");
    await userEvent.type(screen.getByLabelText("Password"), "una-password-lunga");
    await userEvent.click(screen.getByRole("button", { name: "Accedi" }));

    await waitFor(() => {
      expect(router.state.location.pathname).toBe("/dispositivi");
    });
    expect(body).toEqual({ email: "stefano@example.it", password: "una-password-lunga" });
    expect(await screen.findByText("Stefano")).toBeInTheDocument();
  });

  it.each([
    [401, "Email o password non corrette."],
    [423, "Troppi tentativi: account bloccato per 15 minuti."],
    [429, "Troppi tentativi ravvicinati: attendi un minuto."],
    [500, "Accesso non riuscito. Riprova."],
  ])("mostra un messaggio chiaro per l'errore %i", async (status, message) => {
    server.use(
      anonymous,
      http.post("*/api/auth/login", () => HttpResponse.json({ title: "x" }, { status })),
    );
    renderApp("/accesso");

    await userEvent.type(await screen.findByLabelText("Email"), "a@b.it");
    await userEvent.type(screen.getByLabelText("Password"), "x");
    await userEvent.click(screen.getByRole("button", { name: "Accedi" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(message);
  });

  it("esce e torna al login", async () => {
    let loggedIn = true;
    server.use(
      http.get("*/api/auth/me", () =>
        loggedIn
          ? HttpResponse.json({ id: "u1", email: "stefano@example.it", displayName: null })
          : new HttpResponse(null, { status: 401 }),
      ),
      http.post("*/api/auth/logout", () => {
        loggedIn = false;
        return new HttpResponse(null, { status: 204 });
      }),
    );
    const { router } = renderApp("/dispositivi");
    await userEvent.click(await screen.findByRole("button", { name: "Esci" }));
    await waitFor(() => {
      expect(router.state.location.pathname).toBe("/accesso");
    });
  });

  it("segnala un server non raggiungibile", async () => {
    server.use(http.get("*/api/auth/me", () => new HttpResponse(null, { status: 503 })));
    renderApp("/");
    expect(await screen.findByRole("alert")).toHaveTextContent("Server non raggiungibile");
  });
});
