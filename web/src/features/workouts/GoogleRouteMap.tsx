import { useEffect, useRef, useState } from "react";
import type { Position } from "../../lib/geo";
import { loadGoogleMaps } from "../../lib/googleMaps";

/**
 * Tracciato su Google Maps. Il tracciato è disegnato nel browser: a Google arrivano solo le richieste
 * delle porzioni di mappa visualizzate, non le coordinate del percorso.
 */
export function GoogleRouteMap({
  apiKey,
  coordinates,
  onError,
}: {
  apiKey: string;
  coordinates: readonly Position[];
  onError: (message: string) => void;
}) {
  const container = useRef<HTMLDivElement>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    let cancelled = false;
    loadGoogleMaps(apiKey)
      .then((maps) => {
        if (cancelled || !container.current) return;
        const path = coordinates.map((c) => ({ lat: c[1] ?? 0, lng: c[0] ?? 0 }));
        const map = new maps.Map(container.current, {
          mapTypeId: maps.MapTypeId.ROADMAP,
          mapTypeControl: true,
          streetViewControl: false,
          fullscreenControl: true,
          gestureHandling: "cooperative",
        });
        const bounds = new maps.LatLngBounds();
        path.forEach((p) => bounds.extend(p));
        map.fitBounds(bounds, 24);
        const marker = (color: string) => ({
          path: maps.SymbolPath.CIRCLE,
          scale: 6,
          fillColor: color,
          fillOpacity: 1,
          strokeColor: "#ffffff",
          strokeWeight: 2,
        });
        new maps.Polyline({
          map,
          path,
          strokeColor: "#0f766e",
          strokeOpacity: 0.9,
          strokeWeight: 4,
          // Partenza (verde) e arrivo (arancio) come simboli sulla linea: nessun marker aggiuntivo da caricare.
          icons: [
            { icon: marker("#15803d"), offset: "0%" },
            { icon: marker("#c2410c"), offset: "100%" },
          ],
        });
        setReady(true);
      })
      .catch((e: unknown) => {
        if (!cancelled) onError(e instanceof Error ? e.message : "Mappa non disponibile.");
      });
    return () => {
      cancelled = true;
    };
  }, [apiKey, coordinates, onError]);

  return (
    <div
      className="route-map"
      ref={container}
      role="region"
      aria-label="Mappa del percorso"
      aria-busy={!ready}
    />
  );
}
