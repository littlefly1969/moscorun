import { nearestSample, observedSamples, windowBounds } from "./series";

describe("supporto osservato del cronoscopio", () => {
  it("non sostituisce i campioni mancanti con zero", () => {
    expect(
      observedSamples({
        metric: "HeartRate",
        unit: "bpm",
        times: [3, 1, 2],
        values: [0, 120],
        totalSamples: 3,
      }),
    ).toEqual([
      { time: 1, value: 120 },
      { time: 3, value: 0 },
    ]);
  });

  it("non restituisce un campione fuori dalla tolleranza", () => {
    const samples = [{ time: 1000, value: 125 }];
    expect(nearestSample(samples, 2000)?.value).toBe(125);
    expect(nearestSample(samples, 7000)).toBeUndefined();
  });

  it("limita la finestra temporale ai confini della seduta", () => {
    expect(windowBounds(0, 3600000, 0, 5)).toEqual({ left: 0, right: 300000 });
    expect(windowBounds(0, 3600000, 3600000, 5)).toEqual({ left: 3300000, right: 3600000 });
  });
});
