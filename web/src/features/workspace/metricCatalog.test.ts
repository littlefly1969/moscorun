import { readFileSync } from "node:fs";
import { metricCatalog } from "./metricCatalog";

describe("catalogo scientifico", () => {
  it("riproduce tutte le metriche, unità, famiglie e interpretazioni del catalogo canonico", () => {
    const canonical: unknown = JSON.parse(readFileSync("../tools/athlete-analysis/catalog.json", "utf8"));
    expect(canonical).toEqual({
      schemaVersion: expect.any(Number),
      metrics: metricCatalog.map((metric) => ({
        metric: metric.id,
        unit: metric.unit,
        category: metric.family,
        availability: "capability-dependent",
        interpretation: metric.interpretation,
      })),
    });
  });
});
