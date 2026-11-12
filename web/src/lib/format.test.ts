import { formatDeltaKg, formatKg, formatRelative, freshness } from "./format";

const now = new Date("2026-11-12T12:00:00Z");

describe("format", () => {
  it("formatta kg in italiano e lascia vuoti i dati mancanti", () => {
    expect(formatKg(78.4)).toBe("78,4 kg");
    expect(formatKg(null)).toBe("—");
    expect(formatDeltaKg(-0.55)).toBe("-0,55 kg");
    expect(formatDeltaKg(1.2)).toBe("+1,2 kg");
    expect(formatDeltaKg(0)).toBe("0,0 kg");
    expect(formatDeltaKg(undefined)).toBe("—");
  });

  it("descrive il tempo trascorso", () => {
    expect(formatRelative(null, now)).toBe("mai");
    expect(formatRelative("2026-11-12T11:59:30Z", now)).toBe("adesso");
    expect(formatRelative("2026-11-12T11:30:00Z", now)).toBe("30 minuti fa");
    expect(formatRelative("2026-11-12T09:00:00Z", now)).toBe("3 ore fa");
    expect(formatRelative("2026-11-11T12:00:00Z", now)).toBe("ieri");
  });

  it("segnala sorgenti ferme da oltre 48 ore", () => {
    expect(freshness(undefined, now)).toBe("missing");
    expect(freshness("2026-11-11T12:00:00Z", now)).toBe("fresh");
    expect(freshness("2026-11-10T11:59:00Z", now)).toBe("stale");
  });
});
