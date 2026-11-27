import type { Schemas } from "../../api/client";

// Capacità del codice HealthConnectScienceReader/HealthConnectScienceMapping, non del dispositivo dell'utente.
export const healthConnectReaderMetrics = new Set([
  "restingHeartRate",
  "hrvRmssd",
  "sleepDuration",
  "sleepInBedDuration",
  "spo2",
  "respirationRate",
  "vo2max",
  "heartRate",
  "weight",
  "fatPercentage",
  "fatFreeMass",
  "bodyWater",
  "boneMass",
  "basalMetabolicRate",
  "height",
  "systolicBloodPressure",
  "diastolicBloodPressure",
  "bodyTemperature",
  "bloodGlucose",
  "steps",
  "distance",
  "floorsClimbed",
  "energyExpenditure",
  "activeEnergyExpenditure",
  "energyIntake",
  "carbohydrateIntake",
  "proteinIntake",
  "fatIntake",
  "sodiumIntake",
  "caffeineIntake",
  "fiberIntake",
  "sugarIntake",
  "saturatedFatIntake",
  "fluidIntake",
]);

export function archiveStatus(inventory: Schemas["ScientificInventory"] | undefined, metric: string): string {
  if (!inventory) return "Non verificato";
  const observations = inventory.observations.filter((row) => row.metric === metric);
  const rows = observations.reduce((sum, row) => sum + row.revisionRows, 0);
  const values = observations.reduce((sum, row) => sum + row.nonNullRows, 0);
  const chunks = inventory.signals
    .filter((row) => row.metric === metric)
    .reduce((sum, row) => sum + row.chunkRevisions, 0);
  const scale =
    metric === "weight" || metric === "impedance"
      ? inventory.scales.reduce(
          (sum, row) => sum + (metric === "weight" ? row.weighings : row.withImpedance),
          0,
        )
      : 0;
  if (values > 0 || chunks > 0 || scale > 0) {
    return (
      "Presente · " +
      [
        values > 0 ? `${values} valori` : "",
        chunks > 0 ? `${chunks} chunk` : "",
        scale > 0 ? `${scale} pesate` : "",
      ]
        .filter(Boolean)
        .join(" · ")
    );
  }
  return rows > 0 ? "Solo record senza valore" : "Nessun record scientifico/bilancia";
}
