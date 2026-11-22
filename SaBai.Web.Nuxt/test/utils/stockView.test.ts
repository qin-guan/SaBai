import { describe, expect, it } from "vitest";
import type { StockRowData } from "~/utils/medicationCache";
import {
  batchExpiryFlag,
  countStock,
  daysUntilExpiry,
  expiryNote,
  expiryState,
  filterStockGroups,
  formatExpiryDate,
  groupStockByIngredient,
} from "~/utils/stockView";

const now = new Date("2026-11-22T12:00:00Z");

function batch(overrides: Partial<StockRowData>): StockRowData {
  return {
    id: "b1",
    medicationBrandId: "panadol",
    quantity: 100,
    reservedQuantity: 0,
    availableQuantity: 100,
    expiry: "2028-01-01T00:00:00Z",
    location: "Shelf A1",
    stockStatus: "active",
    remarks: null,
    parentStockId: null,
    brandName: "Panadol",
    ingredientName: "Paracetamol 500mg",
    ingredientId: "para",
    unitOfMeasurement: "tablet",
    fallBelow: 50,
    isLowStock: false,
    ...overrides,
  };
}

const rows: StockRowData[] = [
  batch({ id: "b3", brandName: "Paracap", medicationBrandId: "paracap", location: "Store room", expiry: "2027-06-01T00:00:00Z" }),
  batch({ id: "b2", quantity: 40, reservedQuantity: 20, availableQuantity: 20, expiry: "2027-01-06T00:00:00Z", location: "Shelf A1 (open box)" }),
  batch({ id: "b1" }),
  batch({ id: "b4", stockStatus: "discarded", quantity: 12, availableQuantity: 12, remarks: "Damaged in transit" }),
  batch({ id: "c1", ingredientId: "cet", ingredientName: "Cetirizine 10mg", brandName: "Zyrtec", medicationBrandId: "zyrtec", quantity: 8, availableQuantity: 8, fallBelow: 30, isLowStock: true, expiry: "2026-12-20T00:00:00Z" }),
  batch({ id: "c2", ingredientId: "cet", ingredientName: "Cetirizine 10mg", brandName: "Zyrtec", medicationBrandId: "zyrtec", quantity: 30, availableQuantity: 30, fallBelow: 30, expiry: "2026-10-01T00:00:00Z" }),
];
const ingredients = [
  { id: "para", name: "Paracetamol 500mg", unitOfMeasurement: "tablet", fallBelow: 50 },
  { id: "ors", name: "Oral rehydration salts", unitOfMeasurement: "sachet", fallBelow: 20 },
];

describe("expiry", () => {
  it("counts whole days to the expiry", () => {
    expect(daysUntilExpiry("2026-11-23T12:00:00Z", now)).toBe(1);
    expect(daysUntilExpiry("2026-11-22T11:00:00Z", now)).toBe(-1);
    expect(daysUntilExpiry("nonsense", now)).toBeNull();
  });

  it("flags expired and expiring-soon batches", () => {
    expect(expiryState("2026-11-01T00:00:00Z", now)).toBe("expired");
    expect(expiryState("2027-01-06T00:00:00Z", now)).toBe("soon");
    expect(expiryState("2027-06-01T00:00:00Z", now)).toBe("ok");
    // A batch already written off is not flagged.
    expect(batchExpiryFlag({ expiry: "2026-11-01T00:00:00Z", stockStatus: "discarded" }, now)).toBe("ok");
  });

  it("puts it in words", () => {
    expect(expiryNote("2026-11-10T12:00:00Z", now)).toBe("Expired 12 days ago");
    expect(expiryNote("2026-11-21T12:00:00Z", now)).toBe("Expired yesterday");
    expect(expiryNote("2026-11-22T18:00:00Z", now)).toBe("Expires today");
    expect(expiryNote("2026-12-17T12:00:00Z", now)).toBe("Expires in 25 days");
    expect(expiryNote("2028-01-01T00:00:00Z", now)).toBe("");
    expect(formatExpiryDate("2027-01-06T00:00:00Z")).toBe("6 Jan 2027");
  });
});

describe("groupStockByIngredient", () => {
  const groups = groupStockByIngredient(rows, ingredients);

  it("groups by ingredient name, with ingredients that have no batch at all", () => {
    expect(groups.map((g) => g.ingredientName)).toEqual([
      "Cetirizine 10mg",
      "Oral rehydration salts",
      "Paracetamol 500mg",
    ]);
    expect(groups[1]).toMatchObject({ unit: "sachet", batches: [], isOut: true, isLow: true, available: 0 });
  });

  it("orders batches by brand, then earliest expiry", () => {
    const para = groups[2]!;
    expect(para.batches.map((b) => b.id)).toEqual(["b2", "b1", "b4", "b3"]);
    expect(para.brands).toEqual(["Panadol", "Paracap"]);
  });

  it("totals the active batches only and compares them with the low-stock level", () => {
    expect(groups[2]).toMatchObject({ onHand: 240, reserved: 20, available: 220, isLow: false, isOut: false, unit: "tablet" });
    expect(groups[0]).toMatchObject({ available: 38, fallBelow: 30, isLow: false });
    const low = groupStockByIngredient([rows[4]!])[0]!;
    expect(low).toMatchObject({ available: 8, isLow: true, isOut: false });
  });
});

describe("filterStockGroups and countStock", () => {
  const groups = groupStockByIngredient(rows, ingredients);

  it("counts what needs attention", () => {
    expect(countStock(groups, now)).toEqual({ batches: 6, ingredients: 3, lowIngredients: 1, expiringSoon: 2, expired: 1 });
  });

  it("keeps a whole ingredient when its name matches, else the matching batches", () => {
    expect(filterStockGroups(groups, { query: "paracetamol" }, now)[0]!.batches).toHaveLength(4);
    const byBrand = filterStockGroups(groups, { query: "paracap" }, now);
    expect(byBrand).toHaveLength(1);
    expect(byBrand[0]!.batches.map((b) => b.id)).toEqual(["b3"]);
    expect(filterStockGroups(groups, { query: "open box" }, now)[0]!.batches.map((b) => b.id)).toEqual(["b2"]);
    expect(filterStockGroups(groups, { query: "damaged" }, now)[0]!.batches.map((b) => b.id)).toEqual(["b4"]);
    expect(filterStockGroups(groups, { query: "zzz" }, now)).toEqual([]);
  });

  it("narrows to low stock, expiring soon, expired or a status", () => {
    expect(filterStockGroups(groups, { flag: "low" }, now).map((g) => g.ingredientName)).toEqual(["Oral rehydration salts"]);
    expect(filterStockGroups(groups, { flag: "soon" }, now).flatMap((g) => g.batches.map((b) => b.id))).toEqual(["c1", "b2"]);
    expect(filterStockGroups(groups, { flag: "expired" }, now).flatMap((g) => g.batches.map((b) => b.id))).toEqual(["c2"]);
    expect(filterStockGroups(groups, { status: "discarded" }, now).flatMap((g) => g.batches.map((b) => b.id))).toEqual(["b4"]);
  });

  it("returns everything, untouched, without a filter", () => {
    expect(filterStockGroups(groups, {}, now)).toEqual(groups);
  });
});
