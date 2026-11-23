import type { StockRowData } from "./medicationCache";

/**
 * The stock list as a pharmacist reads it: active ingredient → brand → batch,
 * with the ingredient's totals against its low-stock level and each batch's
 * expiry flagged. Framework-free so it can be unit-tested.
 */

/** A batch expiring within this many days is flagged "expiring soon". */
export const EXPIRING_SOON_DAYS = 90;

const DAY_MS = 24 * 60 * 60 * 1000;

export type ExpiryState = "expired" | "soon" | "ok";

/** Whole days until `expiry` (negative once past); null when the date cannot be read. */
export function daysUntilExpiry(expiry: string | null | undefined, now: Date = new Date()): number | null {
  if (!expiry) return null;
  const time = new Date(expiry).getTime();
  if (Number.isNaN(time)) return null;
  return Math.floor((time - now.getTime()) / DAY_MS);
}

export function expiryState(expiry: string | null | undefined, now: Date = new Date()): ExpiryState {
  const days = daysUntilExpiry(expiry, now);
  if (days === null) return "ok";
  if (days < 0) return "expired";
  return days <= EXPIRING_SOON_DAYS ? "soon" : "ok";
}

/** "Expired 12 days ago" / "Expires today" / "Expires in 25 days"; "" when it is not close. */
export function expiryNote(expiry: string | null | undefined, now: Date = new Date()): string {
  const days = daysUntilExpiry(expiry, now);
  if (days === null) return "";
  if (days < 0) {
    const ago = -days;
    return ago === 1 ? "Expired yesterday" : `Expired ${ago} days ago`;
  }
  if (days > EXPIRING_SOON_DAYS) return "";
  if (days === 0) return "Expires today";
  return days === 1 ? "Expires tomorrow" : `Expires in ${days} days`;
}

/** "6 Jan 2027": day first, month in letters, so 06/01 is never read as June. */
export function formatExpiryDate(expiry: string | null | undefined): string {
  if (!expiry) return "-";
  const date = new Date(expiry);
  if (Number.isNaN(date.getTime())) return expiry;
  return date.toLocaleDateString("en-GB", { day: "numeric", month: "short", year: "numeric" });
}

export interface StockIngredientInfo {
  id: string;
  name: string;
  unitOfMeasurement: string;
  fallBelow: number;
}

export interface StockIngredientGroup<T extends StockRowData = StockRowData> {
  ingredientId: string;
  ingredientName: string;
  unit: string;
  /** The ingredient's low-stock level: flagged when what is available is at or below it. */
  fallBelow: number;
  /** Totals of the active batches (discarded and donated batches are not counted). */
  onHand: number;
  reserved: number;
  available: number;
  /** Available units are at or below `fallBelow` (as the API flags its lines). */
  isLow: boolean;
  /** Nothing available to order. */
  isOut: boolean;
  /** Brand names in the group, sorted. */
  brands: string[];
  /** Sorted by brand, then earliest expiry first. */
  batches: T[];
}

/**
 * Groups stock lines by active ingredient (by name), each with its batches by
 * brand and expiry. `ingredients` adds the ingredients that have no batch at
 * all, so an ingredient that ran out still shows (as out of stock).
 */
export function groupStockByIngredient<T extends StockRowData>(
  rows: readonly T[],
  ingredients: readonly StockIngredientInfo[] = [],
): StockIngredientGroup<T>[] {
  const groups = new Map<string, StockIngredientGroup<T>>();
  for (const row of rows) {
    let group = groups.get(row.ingredientId);
    if (!group) {
      group = {
        ingredientId: row.ingredientId,
        ingredientName: row.ingredientName,
        unit: row.unitOfMeasurement ?? "",
        fallBelow: row.fallBelow,
        onHand: 0,
        reserved: 0,
        available: 0,
        isLow: false,
        isOut: false,
        brands: [],
        batches: [],
      };
      groups.set(row.ingredientId, group);
    }
    group.batches.push(row);
    if (!group.brands.includes(row.brandName)) group.brands.push(row.brandName);
    if (row.stockStatus === "active") {
      group.onHand += row.quantity;
      group.reserved += row.reservedQuantity;
      group.available += row.availableQuantity;
    }
  }
  for (const ingredient of ingredients) {
    if (groups.has(ingredient.id)) continue;
    groups.set(ingredient.id, {
      ingredientId: ingredient.id,
      ingredientName: ingredient.name,
      unit: ingredient.unitOfMeasurement,
      fallBelow: ingredient.fallBelow,
      onHand: 0,
      reserved: 0,
      available: 0,
      isLow: false,
      isOut: false,
      brands: [],
      batches: [],
    });
  }
  const result = [...groups.values()];
  for (const group of result) {
    group.isOut = group.available <= 0;
    group.isLow = group.available <= group.fallBelow;
    group.brands.sort((a, b) => a.localeCompare(b));
    group.batches.sort(
      (a, b) => a.brandName.localeCompare(b.brandName) || a.expiry.localeCompare(b.expiry) || a.id.localeCompare(b.id),
    );
  }
  return result.sort((a, b) => a.ingredientName.localeCompare(b.ingredientName));
}

/** What the stock list is narrowed to. */
export type StockFlag = "all" | "low" | "soon" | "expired";

export interface StockFilter {
  /** Matches the ingredient name (keeps all its batches) or a batch's brand, location or remarks. */
  query?: string;
  /** A stock status ("active"…); empty for all. */
  status?: string;
  flag?: StockFlag;
}

const folded = (text: string | null | undefined) => (text ?? "").normalize("NFKD").replace(/\p{M}/gu, "").toLowerCase();

/** An active batch that is past its expiry or close to it: it needs the pharmacist's attention. */
export function batchExpiryFlag(row: Pick<StockRowData, "expiry" | "stockStatus">, now: Date = new Date()): ExpiryState {
  return row.stockStatus === "active" ? expiryState(row.expiry, now) : "ok";
}

export function filterStockGroups<T extends StockRowData>(
  groups: readonly StockIngredientGroup<T>[],
  filter: StockFilter,
  now: Date = new Date(),
): StockIngredientGroup<T>[] {
  const words = folded(filter.query).split(/\s+/).filter(Boolean);
  const flag = filter.flag ?? "all";
  const narrowsBatches = !!filter.status || flag === "soon" || flag === "expired";
  const result: StockIngredientGroup<T>[] = [];
  for (const group of groups) {
    if (flag === "low" && !group.isLow) continue;
    let batches = group.batches;
    if (filter.status) batches = batches.filter((b) => b.stockStatus === filter.status);
    if (flag === "soon" || flag === "expired") batches = batches.filter((b) => batchExpiryFlag(b, now) === flag);
    let queryNarrowed = false;
    if (words.length > 0) {
      const name = folded(group.ingredientName);
      if (!words.every((word) => name.includes(word))) {
        queryNarrowed = true;
        batches = batches.filter((b) => {
          const haystack = `${name}\n${folded(b.brandName)}\n${folded(b.location)}\n${folded(b.remarks)}`;
          return words.every((word) => haystack.includes(word));
        });
      }
    }
    if (batches.length === 0 && (narrowsBatches || queryNarrowed)) continue;
    result.push(batches === group.batches ? group : { ...group, batches });
  }
  return result;
}

export interface StockCounts {
  batches: number;
  ingredients: number;
  lowIngredients: number;
  expiringSoon: number;
  expired: number;
}

export function countStock(groups: readonly StockIngredientGroup[], now: Date = new Date()): StockCounts {
  const counts: StockCounts = { batches: 0, ingredients: groups.length, lowIngredients: 0, expiringSoon: 0, expired: 0 };
  for (const group of groups) {
    if (group.isLow) counts.lowIngredients += 1;
    for (const batch of group.batches) {
      counts.batches += 1;
      const state = batchExpiryFlag(batch, now);
      if (state === "soon") counts.expiringSoon += 1;
      else if (state === "expired") counts.expired += 1;
    }
  }
  return counts;
}
