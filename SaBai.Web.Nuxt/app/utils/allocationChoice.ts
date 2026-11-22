import { formatQuantity } from "./quantity";

/**
 * Choosing the batches a pending order is dispensed from. The server lists the
 * candidate batches (`GET /orders/{id}/allocation-options`, earliest expiry
 * first) with the most this order may take from each; the pharmacist's choice
 * is a quantity per batch that must add up to the order's quantity.
 */

export interface BatchOption {
  stockId: string;
  isExpired: boolean;
  /** The most this order may take from the batch (what is free plus what the order already holds). */
  maxQuantity: number;
  /** What the order holds on the batch now. */
  allocatedQuantity: number;
}

/** Units chosen per batch (`stockId`); batches left out or at 0 are not used. */
export type AllocationChoice = Record<string, number>;

/** Whether units can be taken from the batch at all, and if not, why. */
export function batchUnavailableReason(batch: Pick<BatchOption, "isExpired" | "maxQuantity">): string | null {
  if (batch.isExpired) return "Expired: cannot be dispensed";
  if (batch.maxQuantity <= 0) return "Fully reserved for other orders";
  return null;
}

const usable = (batch: BatchOption) => batchUnavailableReason(batch) === null;

/** What the order holds now. */
export function currentChoice(batches: readonly BatchOption[]): AllocationChoice {
  const choice: AllocationChoice = {};
  for (const batch of batches) {
    if (batch.allocatedQuantity > 0) choice[batch.stockId] = batch.allocatedQuantity;
  }
  return choice;
}

function fill(batches: readonly BatchOption[], quantity: number, choice: AllocationChoice = {}): AllocationChoice {
  let remaining = quantity - chosenTotal(choice);
  for (const batch of batches) {
    if (remaining <= 0) break;
    if (!usable(batch) || choice[batch.stockId]) continue;
    const take = Math.min(batch.maxQuantity, remaining);
    choice[batch.stockId] = take;
    remaining -= take;
  }
  return choice;
}

/**
 * The system's own pick: the batch that expires first first (the order
 * `batches` come in), skipping expired batches, until the order is covered.
 */
export function defaultChoice(batches: readonly BatchOption[], orderQuantity: number): AllocationChoice {
  return fill(batches, orderQuantity);
}

/**
 * Takes as much of the order as possible from one batch (all of it when the
 * batch covers it) and the rest, if any, from the others, earliest expiry first.
 */
export function preferBatch(batches: readonly BatchOption[], stockId: string, orderQuantity: number): AllocationChoice {
  const batch = batches.find((b) => b.stockId === stockId);
  if (!batch || !usable(batch)) return defaultChoice(batches, orderQuantity);
  return fill(batches, orderQuantity, { [stockId]: Math.min(batch.maxQuantity, orderQuantity) });
}

/** True when the batch alone can cover the order. */
export function coversOrder(batch: BatchOption, orderQuantity: number): boolean {
  return usable(batch) && batch.maxQuantity >= orderQuantity;
}

export function chosenTotal(choice: AllocationChoice): number {
  return Object.values(choice).reduce((sum, quantity) => sum + (Number.isFinite(quantity) ? quantity : 0), 0);
}

/** Same batches with the same quantities (zero and missing are the same). */
export function sameChoice(a: AllocationChoice, b: AllocationChoice): boolean {
  const ids = new Set([...Object.keys(a), ...Object.keys(b)]);
  for (const id of ids) {
    if ((a[id] ?? 0) !== (b[id] ?? 0)) return false;
  }
  return true;
}

/** The text of a quantity field as units: whole, at least 0, at most `max`. Blank or unreadable is 0. */
export function parseChoiceQuantity(raw: string, max: number): number {
  const value = Number(raw.trim());
  if (!raw.trim() || !Number.isFinite(value)) return 0;
  return Math.max(0, Math.min(Math.floor(value), Math.max(0, max)));
}

export interface ChoiceCheck {
  valid: boolean;
  /** Why it cannot be saved; null when valid. */
  message: string | null;
  /** The request's `allocations`: the batches with a quantity, in list order. */
  allocations: { stockId: string; quantity: number }[];
  total: number;
}

/** Checks a choice before it is sent: every batch within its limit, and the total equal to the order. */
export function validateChoice(
  batches: readonly BatchOption[],
  choice: AllocationChoice,
  orderQuantity: number,
  unit?: string | null,
): ChoiceCheck {
  const allocations = batches
    .map((batch) => ({ stockId: batch.stockId, quantity: choice[batch.stockId] ?? 0 }))
    .filter((a) => a.quantity > 0);
  const total = chosenTotal(choice);
  let message: string | null = null;
  for (const batch of batches) {
    const quantity = choice[batch.stockId] ?? 0;
    if (quantity === 0) continue;
    if (!Number.isInteger(quantity) || quantity < 0) message = "Quantities must be whole numbers.";
    else if (batch.isExpired) message = "An expired batch cannot be dispensed.";
    else if (quantity > batch.maxQuantity) message = `Only ${formatQuantity(batch.maxQuantity, unit)} can be taken from one of the batches.`;
    if (message) break;
  }
  if (!message) {
    if (total === 0) message = "Choose at least one batch.";
    else if (total < orderQuantity) message = `Choose ${formatQuantity(orderQuantity - total, unit)} more.`;
    else if (total > orderQuantity) message = `${formatQuantity(total - orderQuantity, unit)} too many: the order is for ${formatQuantity(orderQuantity, unit)}.`;
  }
  return { valid: message === null, message, allocations, total };
}
