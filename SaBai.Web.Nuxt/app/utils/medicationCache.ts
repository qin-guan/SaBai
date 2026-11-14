import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
} from "@sabai/sdk/types";

/**
 * Pure list edits behind the medication offline writes. Every function returns
 * new arrays/objects and is safe to apply again with the same input (the
 * offline layer re-applies a queued write's edit after each refetch).
 */

export type StockRowData = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse;

export interface StockSplitData {
  id: string;
  quantity: number;
  location: string;
  stockStatus: SaBaiAPIDomainStockStatusKey;
  remarks: string | null;
}

/** Replaces the entry with the same id in place, or adds `item` at the start / end. */
export function upsertById<T extends { id: string }>(
  list: readonly T[],
  item: T,
  addAt: "start" | "end" = "end",
): T[] {
  if (list.some((x) => x.id === item.id)) return list.map((x) => (x.id === item.id ? item : x));
  return addAt === "start" ? [item, ...list] : [...list, item];
}

/**
 * The API flags a line as low when it is active and the active quantity of its
 * whole ingredient is at or below the ingredient's "fall below" level.
 */
export function withLowStockFlags<T extends StockRowData>(rows: readonly T[]): T[] {
  const activeTotals = new Map<string, number>();
  for (const row of rows) {
    if (row.stockStatus === "active") {
      activeTotals.set(row.ingredientId, (activeTotals.get(row.ingredientId) ?? 0) + row.quantity);
    }
  }
  return rows.map((row) => {
    const isLowStock =
      row.stockStatus === "active" && (activeTotals.get(row.ingredientId) ?? 0) <= row.fallBelow;
    return row.isLowStock === isLowStock ? row : { ...row, isLowStock };
  });
}

/** Stock lists show only lots that still have quantity, newest (largest id) first. */
function visibleStock<T extends StockRowData>(rows: readonly T[]): T[] {
  return withLowStockFlags(rows.filter((row) => row.quantity !== 0));
}

/** Adds or replaces a stock line, keeping the list newest first. */
export function upsertStock(rows: readonly StockRowData[], row: StockRowData): StockRowData[] {
  const without = rows.filter((x) => x.id !== row.id);
  const index = without.findIndex((x) => x.id < row.id);
  const next = [...without];
  next.splice(index === -1 ? next.length : index, 0, row);
  return visibleStock(next);
}

/** Changes fields of one stock line; lines left without quantity drop out, as in the API list. */
export function patchStock(
  rows: readonly StockRowData[],
  id: string,
  patch: Partial<StockRowData>,
): StockRowData[] {
  return visibleStock(rows.map((x) => (x.id === id ? { ...x, ...patch } : x)));
}

/**
 * Applies a split: the parent line ends at zero quantity (and so leaves the
 * list) and one new line per split appears, keyed by the split's own id.
 * `parent` is the line as it was when the user split it, so the result does not
 * depend on what the cache holds now.
 */
export function applySplit(
  rows: readonly StockRowData[],
  parent: StockRowData,
  splits: readonly StockSplitData[],
): StockRowData[] {
  let next = rows.filter((x) => x.id !== parent.id);
  for (const split of splits) {
    next = upsertStock(next, {
      ...parent,
      id: split.id,
      quantity: split.quantity,
      location: split.location,
      stockStatus: split.stockStatus,
      remarks: split.remarks,
    });
  }
  return next;
}
