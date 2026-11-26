/** Sorgenti ammesse dall'acquisizione scientifica (WearableIngestCatalog sul server). */
const scienceSources = [
  { id: "health-connect", label: "Health Connect (app MoscoRun)" },
  { id: "samsung-health", label: "Samsung Health" },
  { id: "garmin-connect", label: "Garmin Connect" },
] as const;

/** Predefinita: l'app Android invia i dati giornalieri letti da Health Connect, qualunque sia l'orologio. */
const defaultScienceSource = "health-connect";

export function ScienceSourceField({ label }: { label: string }) {
  return (
    <label>
      {label}
      <select name="source" defaultValue={defaultScienceSource} required>
        {scienceSources.map((source) => (
          <option key={source.id} value={source.id}>
            {source.label}
          </option>
        ))}
      </select>
    </label>
  );
}
