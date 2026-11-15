import { useCallback, useState } from "react";
import { GoogleRouteMap } from "./GoogleRouteMap";
import { RouteSketch } from "./RouteSketch";
import { useMapsConfig, useRoute } from "./queries";

export function RouteMap({ workoutId, hasTrack }: { workoutId: string; hasTrack: boolean }) {
  const config = useMapsConfig();
  const route = useRoute(workoutId, hasTrack);
  const [mapError, setMapError] = useState<string>();
  const onError = useCallback((message: string) => {
    setMapError(message);
  }, []);

  if (!hasTrack) return <p className="muted">Questo allenamento non ha un percorso GPS.</p>;
  if (route.isPending || config.isPending) return <p role="status">Caricamento del percorso…</p>;
  if (route.isError) {
    return <p className="muted">Percorso non disponibile (può essere interamente in una zona privata).</p>;
  }

  const coordinates = route.data.geometry.coordinates;
  const hidden = route.data.properties.hiddenStartPoints + route.data.properties.hiddenEndPoints;
  const apiKey = config.data?.provider === "google" ? config.data.googleApiKey : null;

  return (
    <>
      {apiKey && !mapError ? (
        <GoogleRouteMap apiKey={apiKey} coordinates={coordinates} onError={onError} />
      ) : (
        <RouteSketch coordinates={coordinates} />
      )}
      {mapError && <p className="notice">{mapError} Il percorso è mostrato senza mappa di sfondo.</p>}
      {!apiKey && !mapError && (
        <p className="muted small">
          Cartografia non configurata: per la mappa Google impostare MOSCORUN_GOOGLE_MAPS_API_KEY sul server.
        </p>
      )}
      {hidden > 0 && <p className="muted small">Partenza e arrivo nascosti: zona privata.</p>}
    </>
  );
}
