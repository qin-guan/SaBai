import { z } from "zod";
import type { Option } from "./options";
import type { SaBaiAPIDomainStockStatusKey } from "@sabai/sdk/types";
import { saBaiAPIDomainStockStatus } from "@sabai/sdk/types";
import type { LocalMedicationStock } from "~/composables/useMedication";

export const MAX_SPLITS = 10;
export const MIN_SPLITS = 2;

export type StockStatus = SaBaiAPIDomainStockStatusKey;
export type MedicationStockRow = LocalMedicationStock;

export const STOCK_STATUSES = Object.values(saBaiAPIDomainStockStatus) as StockStatus[];

export const stockStatusOptions: Option[] = STOCK_STATUSES.map((status) => ({
  label: status,
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

export function validateSplits(splits: SplitPayload[], parentQty: number) {
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
      message: "Child stock quantity does not equal parent stock quantity!",
    };
  }

  if (!areSplitsDistinct(splits)) {
    return { success: false, message: "Splits are not distinct!" };
  }

  return { success: true, message: "Splits are valid!" };
}
