import type { Journey } from "~/utils/journey";
import { estimatedWaitMinutes, formatRate, formatWait, stationThroughput, withServiceTimeFallback } from "~/utils/waitTime";
import { formCompleteness, median, serviceSample } from "~/utils/serviceTime";

const now = new Date(2026, 10, 6, 12, 0).getTime();
const minsAgo = (m: number) => new Date(now - m * 60_000).toISOString();
const done = (consultAt: string | null): Journey => ({
  vitals: true,
  vision: true,
  consult: !!consultAt,
  dental: false,
  dentalReferred: false,
  pharmacyOrders: 0,
  pharmacyPending: 0,
  vitalsAt: null,
  visionAt: null,
  consultAt,
  dentalAt: null,
  pharmacyAt: null,
});

describe("stationThroughput", () => {
  it("measures completions over the last hour", () => {
    // 6 consults in the last hour, station busy all hour.
    const journeys = [90, 50, 40, 30, 20, 10, 5].map(m => done(minsAgo(m)));
    const t = stationThroughput(journeys, "consults", now);
    expect(t.basis).toBe("last hour");
    expect(t.samples).toBe(6);
    expect(t.perMinute).toBeCloseTo(6 / 60);
  });

  it("measures from the first completion when the station started within the hour", () => {
    const journeys = [30, 20, 10].map(m => done(minsAgo(m)));
    expect(stationThroughput(journeys, "consults", now).perMinute).toBeCloseTo(3 / 30);
  });

  it("falls back to the whole day when the last hour is quiet", () => {
    const journeys = [240, 180, 120, 30].map(m => done(minsAgo(m)));
    const t = stationThroughput(journeys, "consults", now);
    expect(t.basis).toBe("today");
    expect(t.perMinute).toBeCloseTo(4 / 240);
  });

  it("has no estimate with too few completions", () => {
    const t = stationThroughput([done(minsAgo(5)), done(null)], "consults", now);
    expect(t.perMinute).toBeNull();
    expect(t.samples).toBe(1);
  });

  it("does not divide by a tiny span", () => {
    const journeys = [3, 2, 1].map(m => done(minsAgo(m)));
    expect(stationThroughput(journeys, "consults", now).perMinute).toBeCloseTo(3 / 10);
  });
});

describe("estimatedWaitMinutes", () => {
  it("scales with queue position", () => {
    const t = { perMinute: 0.1, samples: 6, basis: "last hour" as const };
    expect(estimatedWaitMinutes(t, 1)).toBeCloseTo(10);
    expect(estimatedWaitMinutes(t, 3)).toBeCloseTo(30);
  });

  it("is null without a rate", () => {
    expect(estimatedWaitMinutes({ perMinute: null, samples: 0, basis: null }, 1)).toBeNull();
  });
});

describe("formatWait", () => {
  it("rounds to five minutes", () => {
    expect(formatWait(null)).toBe("—");
    expect(formatWait(3)).toBe("<5 min");
    expect(formatWait(13)).toBe("~15 min");
    expect(formatWait(80)).toBe("~1 h 20 min");
    expect(formatWait(119)).toBe("~2 h");
  });
});

describe("formatRate", () => {
  it("shows patients per hour", () => {
    expect(formatRate(null)).toBe("—");
    expect(formatRate(0.1)).toBe("6.0 / h");
    expect(formatRate(0.5)).toBe("30 / h");
  });
});

describe("formCompleteness", () => {
  it("counts filled text, numbers and list items, ignoring booleans", () => {
    expect(formCompleteness({ a: "x", b: "", c: 3, d: null, e: true })).toBeCloseTo(2 / 4);
    expect(formCompleteness({ list: [{ v: "a" }, { v: "" }], empty: [] })).toBeCloseTo(1 / 3);
    expect(formCompleteness({})).toBe(1);
  });
});

describe("serviceSample", () => {
  const t0 = 1_000_000;
  it("uses the elapsed time for a finished form", () => {
    expect(serviceSample(t0, t0 + 6 * 60_000, 0.96)).toEqual({ at: t0 + 6 * 60_000, minutes: 6 });
  });

  it("scales a partly filled form up to a full one", () => {
    expect(serviceSample(t0, t0 + 4 * 60_000, 0.5)!.minutes).toBeCloseTo(8);
    expect(serviceSample(t0, t0 + 4 * 60_000, 0.1)!.minutes).toBeCloseTo(16);
  });

  it("ignores instant saves and forms left open", () => {
    expect(serviceSample(t0, t0 + 5_000, 1)).toBeNull();
    expect(serviceSample(t0, t0 + 90 * 60_000, 1)).toBeNull();
  });
});

describe("median", () => {
  it("handles odd, even and empty lists", () => {
    expect(median([3, 1, 2])).toBe(2);
    expect(median([4, 1, 2, 3])).toBe(2.5);
    expect(median([])).toBeNull();
  });
});

describe("withServiceTimeFallback", () => {
  it("prefers measured throughput", () => {
    const measured = { perMinute: 0.2, samples: 5, basis: "last hour" as const };
    expect(withServiceTimeFallback(measured, 10)).toBe(measured);
  });

  it("falls back to one patient per typical form time", () => {
    const t = withServiceTimeFallback({ perMinute: null, samples: 1, basis: null }, 8);
    expect(t).toMatchObject({ perMinute: 1 / 8, basis: "form timing" });
  });
});
