import { validateSplits } from "~/utils/medicationStock";
import type { SplitPayload } from "~/utils/medicationStock";
import { MAX_SPLITS } from "~/utils/medicationStock";

function makeSplit(overrides: Partial<SplitPayload> = {}): SplitPayload {
  return {
    quantity: 5,
    location: "Shelf A",
    stockStatus: "active",
    remarks: "",
    ...overrides,
  };
}

describe("validateSplits", () => {
  it("rejects when there are no splits", () => {
    expect(validateSplits([], 10)).toEqual({
      success: false,
      message: "Submitting without a split does nothing!",
    });
  });

  it("rejects if there is only one split", () => {
    expect(validateSplits([makeSplit({ quantity: 10 })], 10)).toEqual({
      success: false,
      message:
        "Please use the editing function instead to edit a single split!",
    });
  });

  it("rejects if the length of splits exceed MAX_SPLITS", () => {
    const splits = Array.from({ length: MAX_SPLITS + 1 }, (_, index) =>
      makeSplit({ quantity: 1, location: `Shelf ${index}` }),
    );

    expect(validateSplits(splits, splits.length)).toEqual({
      success: false,
      message: "Please do not exceed " + MAX_SPLITS + " splits!",
    });
  });

  it("rejects if the total quantity in the splits does not equal the parent split quantity", () => {
    const splits = [
      makeSplit({ quantity: 4, location: "Shelf A" }),
      makeSplit({ quantity: 5, location: "Shelf B" }),
    ];

    expect(validateSplits(splits, 10)).toEqual({
      success: false,
      message: "Child stock quantity does not equal parent stock quantity!",
    });
  });

  it("rejects if splits are not distinct", () => {
    const splits = [makeSplit({ quantity: 5 }), makeSplit({ quantity: 5 })];

    expect(validateSplits(splits, 10)).toEqual({
      success: false,
      message: "Splits are not distinct!",
    });
  });

  it("accepts distinct splits that sum to the parent quantity at MAX_SPLITS", () => {
    const splits = Array.from({ length: MAX_SPLITS }, (_, index) =>
      makeSplit({ quantity: 1, location: `Shelf ${index}` }),
    );

    expect(validateSplits(splits, MAX_SPLITS)).toEqual({
      success: true,
      message: "Splits are valid!",
    });
  });

  it("accepts splits that differ only by stock status", () => {
    const splits = [
      makeSplit({ quantity: 3, stockStatus: "active" }),
      makeSplit({ quantity: 7, stockStatus: "reserved" }),
    ];

    expect(validateSplits(splits, 10)).toEqual({
      success: true,
      message: "Splits are valid!",
    });
  });
});

import { validateAdjustment } from "~/utils/medicationStock";

describe("validateAdjustment", () => {
  it("rejects an empty or non-integer change", () => {
    expect(validateAdjustment("", 5)).toEqual({ success: false, message: "Missing quantity change!" });
    expect(validateAdjustment("1.5", 5)).toEqual({ success: false, message: "Please input a whole number." });
  });

  it("rejects a zero change", () => {
    expect(validateAdjustment("0", 5).success).toBe(false);
  });

  it("rejects a change that would leave a negative quantity", () => {
    expect(validateAdjustment("-6", 5)).toEqual({ success: false, message: "Stock quantity cannot be negative." });
  });

  it("returns the quantity the lot holds afterwards", () => {
    expect(validateAdjustment("-5", 5)).toEqual({ success: true, quantityChange: -5, resultingQuantity: 0 });
    expect(validateAdjustment("7", 5)).toEqual({ success: true, quantityChange: 7, resultingQuantity: 12 });
  });
});
