import { z } from "zod";
import type { Option } from "./options";
import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
} from "@sabai/sdk/types";
import { saBaiAPIDomainStockStatus } from "@sabai/sdk/types";
import { formatQuantity } from "./quantity";

export const MAX_SPLITS = 10;
export const MIN_SPLITS = 2;

export type StockStatus = SaBaiAPIDomainStockStatusKey;
export type MedicationStockRow = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse;

export const STOCK_STATUSES = Object.values(saBaiAPIDomainStockStatus) as StockStatus[];

/** "active" → "Active": statuses are shown capitalised, like every other badge. */
export function stockStatusLabel(status: string): string {
  return status ? status.charAt(0).toUpperCase() + status.slice(1).replace(/_/g, " ") : "";
}

export const stockStatusOptions: Option[] = STOCK_STATUSES.map((status) => ({
  label: stockStatusLabel(status),
  value: status,
}));

export const splitSchema = z.object({
  quantity: z.number().int().positive(),
  location: z.string(),
  stockStatus: z.enum(STOCK_STATUSES as [StockStatus, ...StockStatus[]]),
  remarks: z.string().optional(),
});

export type SplitPayload = z.infer<typeof splitSchema>;

function splitKey(split: SplitPayload) {
  return JSON.stringify([split.location, split.stockStatus, split.quantity, split.remarks ?? ""]);
}

function areSplitsDistinct(splits: SplitPayload[]) {
  return new Set(splits.map(splitKey)).size == splits.length;
}

/**
 * Checks a split before it is sent. Only a batch's unreserved units can be
 * split off (units reserved for pending orders stay in it), so `parentQty` is
 * the batch's available quantity and `reservedQty` what stays behind. `unit`
 * (the ingredient's, "tablet") makes the quantities in the messages explicit.
 */
export function validateSplits(splits: SplitPayload[], parentQty: number, reservedQty = 0, unit?: string | null) {
  if (parentQty <= 0) {
    return {
      success: false,
      message: "All of this stock is reserved for pending orders, so none of it can be split off.",
    };
  }

  if (splits.length === 0) {
    return { success: false, message: "Submitting without a split does nothing!" };
  }

  if (splits.length === 1) {
    return {
      success: false,
      message: "Please use the editing function instead to edit a single split!",
    };
  }

  if (splits.length > MAX_SPLITS) {
    return { success: false, message: `Please do not exceed ${MAX_SPLITS} splits!` };
  }

  const quantity = splits.reduce((accumulator, current) => accumulator + current.quantity, 0);

  if (quantity !== parentQty) {
    return {
      success: false,
      message:
        reservedQty > 0
          ? `The new batches must add up to the ${unit ? formatQuantity(parentQty, unit) : `${parentQty} units`} that are not reserved (now ${unit ? formatQuantity(quantity, unit) : quantity}); ${unit ? formatQuantity(reservedQty, unit) : reservedQty} are reserved for pending orders.`
          : unit
            ? `The new batches must add up to ${formatQuantity(parentQty, unit)} (now ${formatQuantity(quantity, unit)}).`
            : "Child stock quantity does not equal parent stock quantity!",
    };
  }

  if (!areSplitsDistinct(splits)) {
    return { success: false, message: "Splits are not distinct!" };
  }

  return { success: true, message: "Splits are valid!" };
}
