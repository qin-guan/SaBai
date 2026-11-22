import type { SaBaiAPIDomainOrderStatusKey } from "@sabai/sdk/types";
import type { IngredientAvailability } from "./medicationCache";

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
}

export interface OrderableOptions {
  /** Units of each ingredient already asked for elsewhere in the same form. */
  staged?: ReadonlyMap<string, number>;
  /**
   * The order being changed: the units it already holds count as available to
   * it (they are released when it is re-allocated).
   */
  holding?: { ingredientId: string; quantity: number };
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
      };
    });
}

/** "Paracetamol — 120 tablet available" / "Amoxicillin — out of stock". */
export function orderableLabel(option: OrderableIngredient): string {
  return option.outOfStock
    ? `${option.name} — out of stock`
    : `${option.name} — ${option.remaining} ${option.unit} available`;
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
  if (quantity > option.remaining) return `Only ${option.remaining} ${option.unit} available`;
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

/** " tablet" after a quantity, or nothing when the unit is unknown. */
export function formatQuantityUnit(unit: string | null | undefined): string {
  return unit ? ` ${unit}` : "";
}
