import { applySplit, patchStock, upsertById, upsertStock, withLowStockFlags } from "~/utils/medicationCache";
import type { StockRowData } from "~/utils/medicationCache";

function line(overrides: Partial<StockRowData> = {}): StockRowData {
  return {
    id: "00000000-0000-7000-8000-000000000001",
    medicationBrandId: "brand-1",
    quantity: 10,
    expiry: "2030-01-01",
    location: "Shelf A",
    stockStatus: "active",
    remarks: null,
    brandName: "Brand",
    ingredientName: "Ingredient",
    ingredientId: "ing-1",
    fallBelow: 5,
    isLowStock: false,
    ...overrides,
  };
}

describe("upsertById", () => {
  it("replaces in place or adds at the requested end", () => {
    const list = [{ id: "a", v: 1 }, { id: "b", v: 1 }];
    expect(upsertById(list, { id: "a", v: 2 })).toEqual([{ id: "a", v: 2 }, { id: "b", v: 1 }]);
    expect(upsertById(list, { id: "c", v: 1 }).map((x) => x.id)).toEqual(["a", "b", "c"]);
    expect(upsertById(list, { id: "c", v: 1 }, "start").map((x) => x.id)).toEqual(["c", "a", "b"]);
  });

  it("does not change the input", () => {
    const list = [{ id: "a", v: 1 }];
    upsertById(list, { id: "b", v: 1 });
    expect(list).toHaveLength(1);
  });
});

describe("withLowStockFlags", () => {
  it("flags active lines when the ingredient's active total is at or below the level", () => {
    const rows = [
      line({ id: "1", quantity: 2 }),
      line({ id: "2", quantity: 3 }),
      line({ id: "3", quantity: 100, stockStatus: "discarded" }),
    ];
    expect(withLowStockFlags(rows).map((r) => r.isLowStock)).toEqual([true, true, false]);
  });

  it("clears the flag when stock is enough", () => {
    const rows = [line({ id: "1", quantity: 6, isLowStock: true })];
    expect(withLowStockFlags(rows)[0]!.isLowStock).toBe(false);
  });
});

describe("patchStock", () => {
  it("sets an absolute quantity, so applying it again changes nothing", () => {
    const rows = [line({ id: "1", quantity: 10 })];
    const once = patchStock(rows, "1", { quantity: 7 });
    const twice = patchStock(once, "1", { quantity: 7 });
    expect(once[0]!.quantity).toBe(7);
    expect(twice).toEqual(once);
  });

  it("drops a line that reaches zero, as the API list does", () => {
    expect(patchStock([line({ id: "1" })], "1", { quantity: 0 })).toEqual([]);
  });
});

describe("upsertStock", () => {
  it("keeps the list newest (largest id) first", () => {
    const rows = [line({ id: "c" }), line({ id: "a" })];
    expect(upsertStock(rows, line({ id: "b" })).map((r) => r.id)).toEqual(["c", "b", "a"]);
    expect(upsertStock(rows, line({ id: "d" })).map((r) => r.id)).toEqual(["d", "c", "a"]);
    expect(upsertStock(rows, line({ id: "0" })).map((r) => r.id)).toEqual(["c", "a", "0"]);
  });
});

describe("applySplit", () => {
  const parent = line({ id: "p", quantity: 10 });
  const splits = [
    { id: "s1", quantity: 4, location: "A", stockStatus: "active" as const, remarks: null },
    { id: "s2", quantity: 6, location: "B", stockStatus: "reserved" as const, remarks: "x" },
  ];

  it("removes the parent and adds one line per split with the parent's brand and expiry", () => {
    const result = applySplit([parent, line({ id: "other" })], parent, splits);
    expect(result.map((r) => r.id).sort()).toEqual(["other", "s1", "s2"]);
    const s2 = result.find((r) => r.id === "s2")!;
    expect(s2).toMatchObject({ quantity: 6, location: "B", stockStatus: "reserved", remarks: "x", brandName: "Brand", expiry: "2030-01-01" });
  });

  it("gives the same list when applied again", () => {
    const once = applySplit([parent], parent, splits);
    expect(applySplit(once, parent, splits)).toEqual(once);
  });
});
