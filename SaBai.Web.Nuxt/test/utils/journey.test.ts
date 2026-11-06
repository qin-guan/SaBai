import { isWaitingFor, journeySteps, nextStep, requiredProgress, stationPriority } from "~/utils/journey";
import type { Journey } from "~/utils/journey";

const blank: Journey = {
  vitals: false,
  vision: false,
  consult: false,
  dental: false,
  dentalReferred: false,
  pharmacyOrders: 0,
  pharmacyPending: 0,
};
const j = (over: Partial<Journey>): Journey => ({ ...blank, ...over });

describe("journeySteps", () => {
  it("lists the stations in clinic order", () => {
    expect(journeySteps(blank).map(s => s.key)).toEqual(["vitals", "vision", "consults", "dental", "pharmacy"]);
  });

  it("marks required steps todo and optional steps optional for a new visit", () => {
    expect(journeySteps(blank).map(s => s.state)).toEqual(["todo", "todo", "todo", "optional", "optional"]);
  });

  it("makes dental todo when a consult referred the patient", () => {
    const dental = journeySteps(j({ dentalReferred: true }))[3]!;
    expect(dental.state).toBe("todo");
    expect(dental.detail).toBe("Referred");
  });

  it("tracks pharmacy by pending and dispensed orders", () => {
    expect(journeySteps(j({ pharmacyOrders: 2, pharmacyPending: 2 }))[4]).toMatchObject({ state: "todo", detail: "2 to dispense" });
    expect(journeySteps(j({ pharmacyOrders: 2, pharmacyPending: 0 }))[4]).toMatchObject({ state: "done" });
  });
});

describe("nextStep", () => {
  it("returns the first step still to do", () => {
    expect(nextStep(j({ vitals: true }))?.key).toBe("vision");
  });

  it("skips optional steps nobody asked for", () => {
    expect(nextStep(j({ vitals: true, vision: true, consult: true }))).toBeNull();
  });

  it("includes a dental referral and undispensed medication", () => {
    expect(nextStep(j({ vitals: true, vision: true, consult: true, dentalReferred: true }))?.key).toBe("dental");
    expect(nextStep(j({ vitals: true, vision: true, consult: true, pharmacyOrders: 1, pharmacyPending: 1 }))?.key).toBe("pharmacy");
  });
});

describe("requiredProgress", () => {
  it("counts vitals, vision and consult only", () => {
    expect(requiredProgress(j({ vitals: true, dental: true }))).toEqual({ done: 1, total: 3 });
  });
});

describe("isWaitingFor", () => {
  it("waits for required stations until recorded", () => {
    expect(isWaitingFor(blank, "vitals")).toBe(true);
    expect(isWaitingFor(j({ vitals: true }), "vitals")).toBe(false);
  });

  it("waits for dental once referred or done with the consult", () => {
    expect(isWaitingFor(blank, "dental")).toBe(false);
    expect(isWaitingFor(j({ dentalReferred: true }), "dental")).toBe(true);
    expect(isWaitingFor(j({ consult: true }), "dental")).toBe(true);
    expect(isWaitingFor(j({ consult: true, dental: true }), "dental")).toBe(false);
  });

  it("only waits for pharmacy with medication to dispense", () => {
    expect(isWaitingFor(blank, "pharmacy")).toBe(false);
    expect(isWaitingFor(j({ pharmacyOrders: 1, pharmacyPending: 1 }), "pharmacy")).toBe(true);
  });
});

describe("stationPriority", () => {
  it("puts patients who finished earlier steps first", () => {
    expect(stationPriority(j({ vitals: true, vision: true }), "consults")).toBe(0);
    expect(stationPriority(j({ vitals: true }), "consults")).toBe(1);
  });

  it("puts dental referrals before walk-ins", () => {
    expect(stationPriority(j({ dentalReferred: true }), "dental")).toBeLessThan(stationPriority(blank, "dental"));
  });
});
