import type { SaBaiAPIDomainOrderStatusKey } from "@sabai/sdk/types";
import type { IngredientAvailability } from "./medicationCache";
import { formatQuantity } from "./quantity";

/**
 * Ordering by active ingredient. The server reserves stock when an order is
 * placed (earliest expiry first, across brands), so what a doctor may order is
 * the ingredient's available quantity from `GET /medication-stock/by-ingredient`,
 * less what the same form already asks for.
 */

export interface OrderableIngredient {
  ingredientId: string;
  name: string;
  unit: string;
  /** Units that can still be ordered here. */
  remaining: number;
  /** Nothing left to order: shown, but not selectable. */
  outOfStock: boolean;
  /** Brands of this ingredient: searchable in the picker. */
  brandNames: string[];
}

export interface OrderableOptions {
  /** Units of each ingredient already asked for elsewhere in the same form. */
  staged?: ReadonlyMap<string, number>;
  /**
   * The order being changed: the units it already holds count as available to
   * it (they are released when it is re-allocated).
   */
  holding?: { ingredientId: string; quantity: number };
  /**
   * Brand names by ingredient id (see `brandNamesByIngredient`), for callers
   * that have the brand list. Without it the names the availability rows carry
   * (`brandNames`, which every ordering role may read) are used.
   */
  brands?: ReadonlyMap<string, readonly string[]>;
}

/** Every ingredient, by name, with what can still be ordered of it. */
export function orderableIngredients(
  rows: readonly IngredientAvailability[],
  options: OrderableOptions = {},
): OrderableIngredient[] {
  return [...rows]
    .sort((a, b) => a.ingredientName.localeCompare(b.ingredientName))
    .map((row) => {
      const held = options.holding?.ingredientId === row.ingredientId ? options.holding.quantity : 0;
      const remaining = Math.max(
        0,
        row.availableQuantity + held - (options.staged?.get(row.ingredientId) ?? 0),
      );
      return {
        ingredientId: row.ingredientId,
        name: row.ingredientName,
        unit: row.unitOfMeasurement,
        remaining,
        outOfStock: remaining <= 0,
        brandNames: [...(options.brands?.get(row.ingredientId) ?? row.brandNames ?? [])].sort((a, b) => a.localeCompare(b)),
      };
    });
}

/** "120 tablets available" / "Out of stock". */
export function orderableAvailability(option: Pick<OrderableIngredient, "remaining" | "unit" | "outOfStock">): string {
  return option.outOfStock ? "Out of stock" : `${formatQuantity(option.remaining, option.unit)} available`;
}

/** "Paracetamol — 120 tablets available" / "Amoxicillin — out of stock". */
export function orderableLabel(option: OrderableIngredient): string {
  return option.outOfStock
    ? `${option.name} — out of stock`
    : `${option.name} — ${orderableAvailability(option)}`;
}

/** Brand names of each ingredient, sorted, without repeats. */
export function brandNamesByIngredient(
  brands: readonly { name: string; activeIngredientId: string }[],
): Map<string, string[]> {
  const result = new Map<string, string[]>();
  for (const brand of brands) {
    const names = result.get(brand.activeIngredientId) ?? [];
    if (!names.includes(brand.name)) names.push(brand.name);
    result.set(brand.activeIngredientId, names);
  }
  for (const names of result.values()) names.sort((a, b) => a.localeCompare(b));
  return result;
}

const searchText = (text: string) => text.normalize("NFKD").replace(/\p{M}/gu, "").toLowerCase();

/**
 * The options matching what was typed: every word must be in the ingredient
 * name or one of its brand names (any order, case and accents ignored).
 * Names starting with the query come first; the rest keep their order.
 */
export function searchOrderableIngredients<T extends Pick<OrderableIngredient, "name" | "brandNames">>(
  options: readonly T[],
  query: string,
): T[] {
  const words = searchText(query).split(/\s+/).filter(Boolean);
  if (words.length === 0) return [...options];
  const first = searchText(query).trim();
  const starts: T[] = [];
  const rest: T[] = [];
  for (const option of options) {
    const name = searchText(option.name);
    const haystack = [name, ...option.brandNames.map(searchText)].join("\n");
    if (!words.every((word) => haystack.includes(word))) continue;
    (name.startsWith(first) ? starts : rest).push(option);
  }
  return [...starts, ...rest];
}

/** The brand names of `option` that `query` matches, to show why a result is listed. */
export function matchingBrandNames(option: Pick<OrderableIngredient, "brandNames">, query: string): string[] {
  const words = searchText(query).split(/\s+/).filter(Boolean);
  if (words.length === 0) return [];
  return option.brandNames.filter((brand) => words.some((word) => searchText(brand).includes(word)));
}

/** Sums the quantity asked for each ingredient across order lines (ignores blank or invalid lines). */
export function stagedQuantities(
  lines: readonly { ingredientId: string; quantity: number }[],
): Map<string, number> {
  const totals = new Map<string, number>();
  for (const line of lines) {
    if (!line.ingredientId || !Number.isFinite(line.quantity) || line.quantity <= 0) continue;
    totals.set(line.ingredientId, (totals.get(line.ingredientId) ?? 0) + line.quantity);
  }
  return totals;
}

/**
 * The error for an order quantity, or null when it can be ordered. `raw` is
 * the text in the field.
 */
export function orderQuantityError(
  raw: string,
  option: Pick<OrderableIngredient, "remaining" | "unit"> | undefined,
): string | null {
  const text = raw.trim();
  if (!text) return "Quantity is required";
  const quantity = Number(text);
  if (!Number.isInteger(quantity) || quantity < 1) return "Quantity must be a whole number of at least 1";
  if (!option) return null;
  if (option.remaining <= 0) return "Out of stock: choose another medication";
  if (quantity > option.remaining) return `Only ${formatQuantity(option.remaining, option.unit)} available`;
  return null;
}

export const ORDER_STATUS_LABELS: Record<SaBaiAPIDomainOrderStatusKey, string> = {
  PENDING: "Pending",
  DISPENSED: "Dispensed",
  CANCELLED: "Cancelled",
};

/** Badge classes for an order status, in the app's semantic colours. */
export const ORDER_STATUS_CLASSES: Record<SaBaiAPIDomainOrderStatusKey, string> = {
  PENDING: "border-warning/40 bg-warning-subtle text-warning",
  DISPENSED: "border-success/40 bg-success-subtle text-success",
  CANCELLED: "border-destructive/40 bg-destructive-subtle text-destructive",
};
