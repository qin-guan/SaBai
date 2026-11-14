import {
  hasDrugAllergy,
  parseDrugAllergies,
  patientMatches,
  isRecentVisit,
  formatPatientCode,
  formatPatientId,
  toDateInputValue,
  calculateAge,
  matchesTags,
  toggleTagSelection,
  hasTagSelected,
  collectUniqueTags,
  buildTagDropdownOptions,
} from "~/utils/patient";

describe("formatPatientId", () => {
  it("zero-pads ids to 4 digits", () => {
    expect(formatPatientId(12)).toBe("0012");
  });

  it("leaves ids of 4 or more digits unchanged", () => {
    expect(formatPatientId(1234)).toBe("1234");
    expect(formatPatientId(12345)).toBe("12345");
  });
});

describe("formatPatientCode", () => {
  it("prefixes the village code when present", () => {
    expect(formatPatientCode("PC", 12)).toBe("PC0012");
  });
});

describe("toDateInputValue", () => {
  it("returns yyyy-MM-dd for an ISO string with a time component", () => {
    // The shape <input type='date'> requires; the time must be dropped.
    expect(toDateInputValue("1987-04-10T00:00:00.000Z")).toBe("1987-04-10");
  });

  it("returns yyyy-MM-dd for a bare date string", () => {
    expect(toDateInputValue("1987-04-10")).toBe("1987-04-10");
  });

  it("preserves single-digit month padding", () => {
    expect(toDateInputValue("2000-01-05")).toBe("2000-01-05");
  });

  it("preserves single-digit day padding", () => {
    expect(toDateInputValue("2000-12-01")).toBe("2000-12-01");
  });

  it("handles end-of-month dates correctly", () => {
    expect(toDateInputValue("2024-02-29T00:00:00.000Z")).toBe("2024-02-29");
  });

  it("handles a Date object", () => {
    expect(toDateInputValue(new Date("2005-06-15T00:00:00.000Z"))).toBe(
      "2005-06-15",
    );
  });

  it("returns an empty string for an unparseable date string", () => {
    expect(toDateInputValue("not a date")).toBe("");
  });

  it("returns an empty string for an invalid month/day string", () => {
    expect(toDateInputValue("2000-13-01")).toBe("");
  });

  it("returns an empty string for an invalid Date object", () => {
    expect(toDateInputValue(new Date(NaN))).toBe("");
  });
});

describe("calculateAge", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it("returns the correct age when birthday has already passed this year", () => {
    vi.setSystemTime(new Date(2024, 5, 15)); // June 15, 2024
    expect(calculateAge(new Date(1990, 2, 10))).toBe(34); // March 10, 1990
  });

  it("returns the correct age when birthday is today", () => {
    vi.setSystemTime(new Date(2024, 5, 15)); // June 15, 2024
    expect(calculateAge(new Date(1990, 5, 15))).toBe(34); // June 15, 1990
  });

  it("subtracts one year when birthday has not yet passed this year", () => {
    vi.setSystemTime(new Date(2024, 5, 15)); // June 15, 2024
    expect(calculateAge(new Date(1990, 11, 31))).toBe(33); // December 31, 1990
  });
});


describe("hasDrugAllergy", () => {
  it("flags a recorded allergy", () => {
    expect(hasDrugAllergy("Penicillin")).toBe(true);
    expect(hasDrugAllergy("ប៉េនីស៊ីលីន")).toBe(true);
  });

  it("ignores empty, zero-width space, and English or Khmer 'none' answers", () => {
    for (const value of [null, "", " ", "\u200B", "nil", "NKDA", "None", "-", "n/a", "គ្មាន", "គ្មាន។", "មិនមាន", "ទេ", "គ្មានអាឡែស៊ី"])
      expect(hasDrugAllergy(value)).toBe(false);
  });
});

describe("parseDrugAllergies", () => {
  it("splits on commas, semicolons, slashes, new lines and 'and'", () => {
    expect(parseDrugAllergies("Penicillin, sulfa drugs; aspirin\nNSAIDs / codeine and latex")).toEqual([
      "Penicillin", "sulfa drugs", "aspirin", "NSAIDs", "codeine", "latex",
    ]);
  });

  it("splits on Khmer punctuation (។, ៕) and conjunction (និង)", () => {
    expect(parseDrugAllergies("ប៉េនីស៊ីលីន និង អាស្ពីរីន។")).toEqual([
      "ប៉េនីស៊ីលីន",
      "អាស្ពីរីន",
    ]);
  });

  it("drops duplicates and empty or 'none' answers", () => {
    expect(parseDrugAllergies("Penicillin, penicillin, , nil")).toEqual(["Penicillin"]);
    expect(parseDrugAllergies("None")).toEqual([]);
    expect(parseDrugAllergies("គ្មាន")).toEqual([]);
    expect(parseDrugAllergies(null)).toEqual([]);
  });
});

describe("patientMatches", () => {
  it("matches Khmer names with or without spaces and Khmer digits", () => {
    const patient = { id: 12, name: "សុខ តារា", identificationNumber: "KH1012" };
    expect(patientMatches(patient, "សុខតារា")).toBe(true);
    expect(patientMatches(patient, "តារា")).toBe(true);
    expect(patientMatches(patient, "១២")).toBe(true);
    expect(patientMatches(patient, "KH១០១២")).toBe(true);
    expect(patientMatches(patient, "\u200B")).toBe(false);
  });
});

describe("isRecentVisit", () => {
  it("includes visits within the last 24 hours, including across midnight", () => {
    const afterMidnight = new Date(2026, 10, 7, 0, 30);
    expect(isRecentVisit(new Date(2026, 10, 6, 23, 45), afterMidnight)).toBe(true);
    expect(isRecentVisit(new Date(2026, 10, 5, 23, 45), afterMidnight)).toBe(false);
    expect(isRecentVisit(null, afterMidnight)).toBe(false);
  });
});

describe("tag filtering utilities", () => {
  it("matches all rows when no tags are selected", () => {
    expect(matchesTags([], [])).toBe(true);
    expect(matchesTags(null, [])).toBe(true);
    expect(matchesTags(["Diabetic"], [])).toBe(true);
  });

  it("matches any selected tag (OR) case-insensitively", () => {
    expect(matchesTags(["Diabetic", "Elderly"], ["diabetic", "Pediatric"], "any")).toBe(true);
    expect(matchesTags(["Diabetic"], ["Pediatric", "Follow-Up"], "any")).toBe(false);
    expect(matchesTags([], ["Diabetic"], "any")).toBe(false);
  });

  it("matches all selected tags (AND) case-insensitively", () => {
    expect(matchesTags(["Diabetic", "Elderly", "Follow-Up"], ["diabetic", "ELDERLY"], "all")).toBe(true);
    expect(matchesTags(["Diabetic"], ["Diabetic", "Elderly"], "all")).toBe(false);
  });

  it("toggles tag selection case-insensitively", () => {
    expect(toggleTagSelection(["Diabetic"], "Elderly")).toEqual(["Diabetic", "Elderly"]);
    expect(toggleTagSelection(["Diabetic", "Elderly"], "diabetic")).toEqual(["Elderly"]);
    expect(hasTagSelected(["Diabetic"], "DIABETIC")).toBe(true);
    expect(hasTagSelected(["Diabetic"], "Elderly")).toBe(false);
  });

  it("collects unique tags preserving first-seen casing and sorts alphabetically", () => {
    expect(collectUniqueTags([["Follow-Up", "Diabetic"], ["diabetic", "Pediatric"], null])).toEqual([
      "Diabetic",
      "Follow-Up",
      "Pediatric",
    ]);
  });

  it("builds inline tag dropdown options with matching existing tags and a create option when unmatched", () => {
    const known = ["country:singapore", "kairos", "Follow-Up", "Mobility-Aid"];
    const selected = ["country:singapore", "kairos"];

    // Empty query shows all unselected existing tags, sorted
    expect(buildTagDropdownOptions(known, selected, "")).toEqual([
      { type: "existing", tag: "Follow-Up" },
      { type: "existing", tag: "Mobility-Aid" },
    ]);

    // Partial match shows matching existing tags plus a "create" option for the partial string
    expect(buildTagDropdownOptions(known, selected, "follow")).toEqual([
      { type: "existing", tag: "Follow-Up" },
      { type: "create", tag: "follow" },
    ]);

    // Case-insensitive exact match of an unselected existing tag shows only that existing tag (no duplicate create option)
    expect(buildTagDropdownOptions(known, selected, "follow-up")).toEqual([
      { type: "existing", tag: "Follow-Up" },
    ]);

    // Unmatched query offers only the "create" option
    expect(buildTagDropdownOptions(known, selected, "  VIP  ")).toEqual([
      { type: "create", tag: "VIP" },
    ]);

    // Already-selected tag does not offer a duplicate "create" option
    expect(buildTagDropdownOptions(known, selected, "kairos")).toEqual([]);
  });
});
