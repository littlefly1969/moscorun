/**
 * Carica Maps JavaScript API una sola volta. Gli errori di chiave (dominio non autorizzato, API non abilitata)
 * arrivano da Google tramite window.gm_authFailure: diventano un errore esplicito invece di una mappa grigia.
 */
let loading: Promise<typeof google.maps> | undefined;

declare global {
  interface Window {
    gm_authFailure?: () => void;
    __moscorunMapsReady?: () => void;
  }
}

export function loadGoogleMaps(apiKey: string, timeoutMs = 15_000): Promise<typeof google.maps> {
  loading ??= new Promise<typeof google.maps>((resolve, reject) => {
    const fail = (message: string) => {
      loading = undefined;
      reject(new Error(message));
    };
    const timer = window.setTimeout(() => {
      fail("Google Maps non risponde.");
    }, timeoutMs);
    window.gm_authFailure = () => {
      fail("Chiave Google Maps rifiutata: controllare restrizioni e API abilitate.");
    };
    window.__moscorunMapsReady = () => {
      window.clearTimeout(timer);
      resolve(google.maps);
    };
    const script = document.createElement("script");
    const params = new URLSearchParams({
      key: apiKey,
      v: "weekly",
      loading: "async",
      callback: "__moscorunMapsReady",
      language: "it",
    });
    script.src = `https://maps.googleapis.com/maps/api/js?${params.toString()}`;
    script.async = true;
    script.onerror = () => {
      window.clearTimeout(timer);
      fail("Impossibile caricare Google Maps.");
    };
    document.head.append(script);
  });
  return loading;
}

/** Solo per i test. */
export function resetGoogleMapsLoader() {
  loading = undefined;
}
