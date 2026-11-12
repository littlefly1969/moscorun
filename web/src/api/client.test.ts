import { ApiError, unwrap } from "./client";

const response = (status: number) => new Response(null, { status });

describe("unwrap", () => {
  it("restituisce i dati quando la risposta è ok", async () => {
    await expect(unwrap(Promise.resolve({ data: 42, response: response(200) }))).resolves.toBe(42);
  });

  it("converte un ProblemDetails in ApiError", async () => {
    const call = unwrap(
      Promise.resolve({ error: { title: "Conflitto", detail: "Già registrato" }, response: response(409) }),
    );
    await expect(call).rejects.toMatchObject({ status: 409, message: "Già registrato" });
  });

  it("usa un messaggio generico senza corpo", async () => {
    const error = await unwrap(Promise.resolve({ response: response(500) })).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).message).toBe("Errore HTTP 500");
  });
});
