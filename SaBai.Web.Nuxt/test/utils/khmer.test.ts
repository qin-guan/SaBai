import { describe, expect, it } from "vitest";
import {
  cleanKhmerText,
  expandKhmerSearchTerms,
  hasKhmerScript,
  matchesSearchText,
  normalizeKhmerDigits,
  segmentKhmerWords,
} from "~/utils/khmer";

describe("normalizeKhmerDigits", () => {
  it("converts Khmer digits ០–៩ to ASCII 0–9", () => {
    expect(normalizeKhmerDigits("០១២៣៤៥៦៧៨៩")).toBe("0123456789");
    expect(normalizeKhmerDigits("PC០០១២")).toBe("PC0012");
    expect(normalizeKhmerDigits("០១២ ៣៤៥ ៦៧៨")).toBe("012 345 678");
  });

  it("leaves ASCII digits and non-digit characters untouched", () => {
    expect(normalizeKhmerDigits("Sok Dara 012")).toBe("Sok Dara 012");
    expect(normalizeKhmerDigits("សុខ តារា")).toBe("សុខ តារា");
  });
});

describe("cleanKhmerText", () => {
  it("replaces zero-width spaces with normal spaces and trims invisible edges", () => {
    expect(cleanKhmerText("\u200Bសុខ\u200B\u200Bតារា\u200B")).toBe("សុខ តារា");
    expect(cleanKhmerText("\u200B \u200C \uFEFF")).toBe("");
  });

  it("normalizes Unicode to NFC", () => {
    const nfd = "សុខ".normalize("NFD");
    expect(cleanKhmerText(nfd)).toBe("សុខ".normalize("NFC"));
  });
});

describe("hasKhmerScript", () => {
  it("detects Khmer script characters", () => {
    expect(hasKhmerScript("សុខ តារា")).toBe(true);
    expect(hasKhmerScript("Sok Dara")).toBe(false);
  });
});

describe("segmentKhmerWords", () => {
  it("segments unspaced, spaced, and zero-width-spaced Khmer names", () => {
    expect(segmentKhmerWords("សុខតារា")).toEqual(["សុខ", "តារា"]);
    expect(segmentKhmerWords("សុខ តារា")).toEqual(["សុខ", "តារា"]);
    expect(segmentKhmerWords("សុខ\u200Bតារា")).toEqual(["សុខ", "តារា"]);
  });
});

describe("expandKhmerSearchTerms", () => {
  it("includes individual Khmer words and joined forms", () => {
    const expandedSpaced = expandKhmerSearchTerms("សុខ តារា");
    expect(expandedSpaced).toContain("សុខ");
    expect(expandedSpaced).toContain("តារា");
    expect(expandedSpaced).toContain("សុខតារា");

    const expandedUnspaced = expandKhmerSearchTerms("សុខតារា");
    expect(expandedUnspaced).toContain("សុខ");
    expect(expandedUnspaced).toContain("តារា");
    expect(expandedUnspaced).toContain("សុខតារា");
  });
});

describe("matchesSearchText", () => {
  it("matches Khmer names regardless of spaces or zero-width spaces", () => {
    expect(matchesSearchText("សុខ តារា", "សុខតារា")).toBe(true);
    expect(matchesSearchText("សុខតារា", "សុខ តារា")).toBe(true);
    expect(matchesSearchText("សុខ\u200Bតារា", "សុខ តារា")).toBe(true);
    expect(matchesSearchText("សុខ\u200Bតារា", "តារា")).toBe(true);
    expect(matchesSearchText("សុខ តារា", "ហេង")).toBe(false);
  });

  it("matches Khmer digits against ASCII digits and vice versa", () => {
    expect(matchesSearchText("PC0012", "pc០០១២")).toBe(true);
    expect(matchesSearchText("០១២ ៣៤៥ ៦៧៨", "012 345")).toBe(true);
  });
});
