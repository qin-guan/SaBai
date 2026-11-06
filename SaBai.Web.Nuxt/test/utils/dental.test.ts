import { describe, expect, it } from "vitest";
import { toDentalFormValues, toDentalRequest } from "~/utils/dental";
import type { DentalResponse } from "~/utils/dental";

describe("dental form mapping", () => {
  it("maps an empty record to an all-unanswered form", () => {
    const values = toDentalFormValues(null);
    expect(values.toothache).toBe("");
    expect(values.previousDentalVisit).toBe("");
    expect(values.fluorideDone).toBe("");
    expect(values.diagnoses).toEqual([]);
  });

  it("round-trips booleans, choices, text and diagnoses", () => {
    const saved = {
      toothache: true,
      extraction: false,
      previousDentalVisit: "Yes",
      chiefComplaint: "Pain lower left",
      fluorideDone: true,
      fluorideDate: "2026-11-06",
      sealantDone: false,
      diagnoses: ["Caries 36", "Gingivitis"],
    } as unknown as DentalResponse;

    const values = toDentalFormValues(saved);
    expect(values.toothache).toBe("yes");
    expect(values.extraction).toBe("no");
    expect(values.fluorideDone).toBe("done");
    expect(values.sealantDone).toBe("notDone");

    const body = toDentalRequest(values) as Record<string, unknown>;
    expect(body.toothache).toBe(true);
    expect(body.extraction).toBe(false);
    expect(body.bleedingGums).toBeNull();
    expect(body.previousDentalVisit).toBe("Yes");
    expect(body.chiefComplaint).toBe("Pain lower left");
    expect(body.fluorideDone).toBe(true);
    expect(body.fluorideDate).toBe("2026-11-06");
    expect(body.sealantDone).toBe(false);
    expect(body.scalingDone).toBeNull();
    expect(body.remarks).toBeNull();
    expect(body.diagnoses).toEqual(["Caries 36", "Gingivitis"]);
  });

  it("trims text and drops blank diagnoses", () => {
    const values = toDentalFormValues(null);
    values.remarks = "  note  ";
    values.diagnoses = [{ value: " a " }, { value: "   " }];
    const body = toDentalRequest(values) as Record<string, unknown>;
    expect(body.remarks).toBe("note");
    expect(body.diagnoses).toEqual(["a"]);
  });
});
