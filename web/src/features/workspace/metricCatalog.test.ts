import { metricCatalog } from "./metricCatalog";

describe("catalogo scientifico", () => {
  it("riproduce tutte le metriche, unità, famiglie e interpretazioni del catalogo canonico", () => {
    const files = import.meta.glob("../../../../tools/athlete-analysis/catalog.json", {
      eager: true,
      import: "default",
    });
    expect(Object.values(files)[0]).toEqual({
      schemaVersion: 1,
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
