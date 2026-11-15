import { loadGoogleMaps, resetGoogleMapsLoader } from "./googleMaps";

describe("caricamento di Google Maps", () => {
  afterEach(() => {
    resetGoogleMapsLoader();
    document.head.querySelectorAll("script").forEach((s) => {
      s.remove();
    });
    vi.unstubAllGlobals();
  });

  it("inserisce lo script con la chiave e si risolve alla callback", async () => {
    const maps = { Map: vi.fn() };
    vi.stubGlobal("google", { maps });
    const promise = loadGoogleMaps("AIza-test");

    const script = document.head.querySelector("script");
    expect(script?.src).toContain("https://maps.googleapis.com/maps/api/js?key=AIza-test");
    expect(script?.src).toContain("callback=__moscorunMapsReady");
    window.__moscorunMapsReady?.();
    await expect(promise).resolves.toBe(maps);
    expect(loadGoogleMaps("AIza-test")).toBe(promise);
  });

  it("una chiave rifiutata diventa un errore esplicito e si può riprovare", async () => {
    const promise = loadGoogleMaps("AIza-bad");
    window.gm_authFailure?.();
    await expect(promise).rejects.toThrow(/Chiave Google Maps rifiutata/);
    expect(loadGoogleMaps("AIza-bad")).not.toBe(promise);
  });
});
