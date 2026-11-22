import { SplitPayload } from "@/types/medication-stock";
import { MAX_SPLITS } from "@/lib/constants/medicationStock";

function splitKey(split: SplitPayload) {
  return JSON.stringify([
    split.location,
    split.stockStatus,
    split.quantity,
    split.remarks ?? "",
  ]);
}

function areSplitsDistinct(splits: SplitPayload[]) {
  return new Set(splits.map(splitKey)).size == splits.length;
}

/**
 * Checks a split before it is sent. Only a batch's unreserved units can be
 * split off (units reserved for pending orders stay in it), so `parentQty` is
 * the batch's available quantity and `reservedQty` what stays behind.
 */
export function validateSplits(
  splits: SplitPayload[],
  parentQty: number,
  reservedQty = 0,
) {
  if (parentQty <= 0) {
    return {
      success: false,
      message:
        "All of this stock is reserved for pending orders, so none of it can be split off.",
    };
  }

  if (splits.length === 0) {
    return {
      success: false,
      message: "Submitting without a split does nothing!",
    };
  }

  if (splits.length === 1) {
    return {
      success: false,
      message:
        "Please use the editing function instead to edit a single split!",
    };
  }

  if (splits.length > MAX_SPLITS) {
    return {
      success: false,
      message: `Please do not exceed ${MAX_SPLITS} splits!`,
    };
  }

  const quantity = splits.reduce(
    (accumulator, current) => accumulator + current.quantity,
    0,
  );

  if (quantity !== parentQty) {
    return {
      success: false,
      message:
        reservedQty > 0
          ? `Child stock quantity must equal the ${parentQty} unreserved units; ${reservedQty} are reserved for pending orders.`
          : "Child stock quantity does not equal parent stock quantity!",
    };
  }

  if (!areSplitsDistinct(splits)) {
    return {
      success: false,
      message: "Splits are not distinct!",
    };
  }

  return {
    success: true,
    message: "Splits are valid!",
  };
}
