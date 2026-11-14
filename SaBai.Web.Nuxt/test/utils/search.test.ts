import { describe, expect, it } from "vitest";
import { buildPageIndex, buildPatientIndex, searchIndex } from "~/utils/search";

const patients = [
  { id: 12, name: "Sok Dara", identificationNumber: "KH1012", contactNo: "012 345 678", villageCode: "PC", displayId: "PC0012", tags: ["Diabetic", "Elderly"] },
  { id: 4, name: "Heng Sophea", identificationNumber: "KH1004", contactNo: "097-111-222", villageCode: "KK", displayId: "KK0004" },
  { id: 7, name: "Kim Vuthy", identificationNumber: null, contactNo: null, villageCode: null, displayId: "0007" },
];
const ids = (q: string) => searchIndex(buildPatientIndex(patients), q).map((r) => r.id);

describe("patient search", () => {
  it("matches tags", () => {
    expect(ids("diab")).toEqual([12]);
    expect(ids("elderly")).toEqual([12]);
  });
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
    expect(ids("\u200B\u200B")).toEqual([]);
  });
  it("matches Khmer digits in patient IDs and phone numbers", () => {
    expect(ids("១២")[0]).toBe(12);
    expect(ids("០០០៤")).toEqual([4]);
    expect(ids("០៩៧១១១")).toEqual([4]);
  });
});

describe("Khmer patient search", () => {
  const khmerPatients = [
    { id: 1, name: "សុខ តារា", identificationNumber: "KH១០០១", contactNo: "០១២ ៣៤៥ ៦៧៨", villageCode: "ភ្នំពេញ", displayId: "PP0001" },
    { id: 2, name: "ហេងសុភា", identificationNumber: "KH1002", contactNo: "097 222 333", villageCode: "KK", displayId: "KK0002" },
    { id: 3, name: "គីម\u200Bវុទ្ធី", identificationNumber: null, contactNo: null, villageCode: null, displayId: "0003" },
  ];
  const khmerIds = (q: string) => searchIndex(buildPatientIndex(khmerPatients), q).map((r) => r.id);

  it("matches Khmer names whether stored or queried with spaces, zero-width spaces, or no spaces", () => {
    expect(khmerIds("សុខ")).toEqual([1]);
    expect(khmerIds("តារា")).toEqual([1]);
    expect(khmerIds("សុខ តារា")).toEqual([1]);
    expect(khmerIds("សុខតារា")).toEqual([1]);
    expect(khmerIds("សុខតា")).toEqual([1]);

    expect(khmerIds("ហេង")).toEqual([2]);
    expect(khmerIds("សុភា")).toEqual([2]);
    expect(khmerIds("ហេង សុភា")).toEqual([2]);
    expect(khmerIds("ហេងសុភា")).toEqual([2]);

    expect(khmerIds("គីម")).toEqual([3]);
    expect(khmerIds("វុទ្ធី")).toEqual([3]);
    expect(khmerIds("គីម វុទ្ធី")).toEqual([3]);
    expect(khmerIds("គីមវុទ្ធី")).toEqual([3]);
  });

  it("matches prefixes of compound Khmer words and Khmer digits in stored records", () => {
    expect(khmerIds("ភ្នំពេ")).toEqual([1]);
    expect(khmerIds("ភ្នំពេញ")).toEqual([1]);
    expect(khmerIds("KH1001")).toEqual([1]);
    expect(khmerIds("012345")).toEqual([1]);
  });

  it("tolerates a typo in a Khmer word with combining vowel/consonant marks", () => {
    expect(khmerIds("វុទ្ធិ")).toEqual([3]);
  });

  it("requires every word in a multi-word Khmer query to match", () => {
    expect(khmerIds("សុខ វុទ្ធី")).toEqual([]);
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
