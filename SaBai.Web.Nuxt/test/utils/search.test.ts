import { describe, expect, it } from "vitest";
import { buildPageIndex, buildPatientIndex, searchIndex } from "~/utils/search";

const patients = [
  { id: 12, name: "Sok Dara", identificationNumber: "KH1012", contactNo: "012 345 678", villageCode: "PC", displayId: "PC0012" },
  { id: 4, name: "Heng Sophea", identificationNumber: "KH1004", contactNo: "097-111-222", villageCode: "KK", displayId: "KK0004" },
  { id: 7, name: "Kim Vuthy", identificationNumber: null, contactNo: null, villageCode: null, displayId: "0007" },
];
const ids = (q: string) => searchIndex(buildPatientIndex(patients), q).map((r) => r.id);

describe("patient search", () => {
  it("matches name prefixes", () => {
    expect(ids("sok")).toEqual([12]);
    expect(ids("soph")).toEqual([4]);
  });
  it("matches every form of the id", () => {
    expect(ids("12")[0]).toBe(12);
    expect(ids("0004")).toEqual([4]);
    expect(ids("pc0012")).toEqual([12]);
  });
  it("matches identification and phone numbers (ignoring separators)", () => {
    expect(ids("KH1004")).toEqual([4]);
    expect(ids("097111")).toEqual([4]);
  });
  it("tolerates a typo in longer words", () => {
    expect(ids("sopea")).toEqual([4]);
  });
  it("requires every word to match", () => {
    expect(ids("heng sophea")).toEqual([4]);
    expect(ids("heng dara")).toEqual([]);
  });
  it("returns nothing for an empty query", () => {
    expect(ids("  ")).toEqual([]);
  });
});

describe("page search", () => {
  const index = buildPageIndex([
    { id: "/vitals", title: "Vitals", keywords: "bp blood pressure height weight" },
    { id: "/vision", title: "Vision", keywords: "eye glasses acuity" },
  ]);
  it("finds sections by clinical keywords", () => {
    expect(searchIndex(index, "bp")[0]?.id).toBe("/vitals");
    expect(searchIndex(index, "glasses")[0]?.id).toBe("/vision");
    expect(searchIndex(index, "vit")[0]?.id).toBe("/vitals");
  });
});
