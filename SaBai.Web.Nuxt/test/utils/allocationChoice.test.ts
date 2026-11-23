import { describe, expect, it } from "vitest";
import {
  batchUnavailableReason,
  chosenTotal,
  coversOrder,
  currentChoice,
  defaultChoice,
  parseChoiceQuantity,
  preferBatch,
  sameChoice,
  validateChoice,
  type BatchOption,
} from "~/utils/allocationChoice";

/** Earliest expiry first, as the API lists them. */
const batches: BatchOption[] = [
  { stockId: "old", isExpired: true, maxQuantity: 50, allocatedQuantity: 0 },
  { stockId: "open", isExpired: false, maxQuantity: 12, allocatedQuantity: 12 },
  { stockId: "box", isExpired: false, maxQuantity: 100, allocatedQuantity: 8 },
  { stockId: "held", isExpired: false, maxQuantity: 0, allocatedQuantity: 0 },
  { stockId: "store", isExpired: false, maxQuantity: 500, allocatedQuantity: 0 },
];

describe("allocation choice", () => {
  it("starts from what the order holds", () => {
    expect(currentChoice(batches)).toEqual({ open: 12, box: 8 });
  });

  it("the system default takes the earliest usable batches until the order is covered", () => {
    expect(defaultChoice(batches, 20)).toEqual({ open: 12, box: 8 });
    expect(defaultChoice(batches, 5)).toEqual({ open: 5 });
    // Not enough stock: everything usable, never the expired or fully reserved batch.
    expect(defaultChoice(batches, 1000)).toEqual({ open: 12, box: 100, store: 500 });
  });

  it("one tap takes the whole order from a batch that covers it", () => {
    expect(coversOrder(batches[2]!, 20)).toBe(true);
    expect(preferBatch(batches, "box", 20)).toEqual({ box: 20 });
    expect(preferBatch(batches, "store", 20)).toEqual({ store: 20 });
  });

  it("a batch that cannot cover the order gives all it has; the rest comes earliest expiry first", () => {
    expect(coversOrder(batches[1]!, 20)).toBe(false);
    expect(preferBatch(batches, "open", 20)).toEqual({ open: 12, box: 8 });
    expect(preferBatch(batches, "box", 150)).toEqual({ box: 100, open: 12, store: 38 });
  });

  it("an expired or fully reserved batch cannot be preferred", () => {
    expect(batchUnavailableReason(batches[0]!)).toMatch(/Expired/);
    expect(batchUnavailableReason(batches[3]!)).toMatch(/reserved/);
    expect(batchUnavailableReason(batches[1]!)).toBeNull();
    expect(coversOrder(batches[0]!, 20)).toBe(false);
    expect(preferBatch(batches, "old", 20)).toEqual(defaultChoice(batches, 20));
  });

  it("totals and compares choices, treating zero as not chosen", () => {
    expect(chosenTotal({ open: 12, box: 8 })).toBe(20);
    expect(chosenTotal({})).toBe(0);
    expect(sameChoice({ open: 12, box: 0 }, { open: 12 })).toBe(true);
    expect(sameChoice({ open: 12 }, { open: 11, box: 1 })).toBe(false);
  });

  it("reads a quantity field as whole units within the batch's limit", () => {
    expect(parseChoiceQuantity("7", 12)).toBe(7);
    expect(parseChoiceQuantity(" 30 ", 12)).toBe(12);
    expect(parseChoiceQuantity("-3", 12)).toBe(0);
    expect(parseChoiceQuantity("2.9", 12)).toBe(2);
    expect(parseChoiceQuantity("", 12)).toBe(0);
    expect(parseChoiceQuantity("abc", 12)).toBe(0);
  });
});

describe("validateChoice", () => {
  it("accepts a choice that adds up, and lists the chosen batches in list order", () => {
    expect(validateChoice(batches, { store: 15, open: 5 }, 20, "tablet")).toEqual({
      valid: true,
      message: null,
      total: 20,
      allocations: [
        { stockId: "open", quantity: 5 },
        { stockId: "store", quantity: 15 },
      ],
    });
  });

  it("says how many are missing or too many, with the unit", () => {
    expect(validateChoice(batches, {}, 20, "tablet").message).toBe("Choose at least one batch.");
    expect(validateChoice(batches, { open: 12 }, 20, "tablet").message).toBe("Choose 8 tablets more.");
    expect(validateChoice(batches, { open: 12, box: 9 }, 20, "tablet").message).toBe(
      "1 tablet too many: the order is for 20 tablets.",
    );
    expect(validateChoice(batches, { store: 30 }, 20, "mL").message).toBe("10 mL too many: the order is for 20 mL.");
  });

  it("refuses more than a batch can give, expired batches and fractions", () => {
    expect(validateChoice(batches, { open: 13, box: 7 }, 20, "tablet")).toMatchObject({
      valid: false,
      message: "Only 12 tablets can be taken from one of the batches.",
    });
    expect(validateChoice(batches, { old: 20 }, 20, "tablet").message).toBe("An expired batch cannot be dispensed.");
    expect(validateChoice(batches, { box: 19.5, open: 0.5 }, 20, "tablet").message).toBe(
      "Quantities must be whole numbers.",
    );
  });
});
