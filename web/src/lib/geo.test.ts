import { projectRoute } from "./geo";

describe("proiezione del percorso", () => {
  it("mantiene le proporzioni reali e centra il tracciato", () => {
    // 0,01° di longitudine a 60° N valgono metà di 0,01° di latitudine.
    const p = projectRoute(
      [
        [10, 60],
        [10.01, 60.01],
      ],
      200,
      200,
      0,
    );
    expect(p).not.toBeNull();
    expect(p?.start).toEqual({ x: 50, y: 200 });
    expect(p?.end).toEqual({ x: 150, y: 0 });
    expect(p?.path).toBe("M50 200 L150 0");
  });

  it("restituisce null con meno di due punti e regge i punti coincidenti", () => {
    expect(projectRoute([[12, 41]], 100, 100)).toBeNull();
    expect(
      projectRoute(
        [
          [12, 41],
          [12, 41],
        ],
        100,
        100,
      )?.path,
    ).toMatch(/^M[\d.]+ [\d.]+ L/);
  });
});
