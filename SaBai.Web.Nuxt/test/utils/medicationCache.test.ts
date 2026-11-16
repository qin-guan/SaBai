import { applySplit, patchStock, upsertById, upsertStock, withLowStockFlags, deductStockFifo, upsertOrderInPharmacyQueue, patchOrderInPharmacyQueue, removeOrderFromPharmacyQueue } from "~/utils/medicationCache";
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

describe("deductStockFifo", () => {
  it("deducts quantity FIFO across active lots and drops zero quantity lots", () => {
    const rows = [
      line({ id: "lot-1", expiry: "2030-01-01", quantity: 5 }),
      line({ id: "lot-2", expiry: "2029-01-01", quantity: 10 }), // earlier expiry
    ];
    const result = deductStockFifo(rows, "brand-1", 12);
    // lot-2 (10 qty) should be fully depleted (dropped), lot-1 (5 qty) should have 3 left.
    expect(result.map((r) => r.id)).toEqual(["lot-1"]);
    expect(result[0]!.quantity).toBe(3);
  });

  it("is idempotent when snapshotLots are provided", () => {
    const initial = [line({ id: "lot-1", quantity: 10 })];
    const snapshot = [line({ id: "lot-1", quantity: 10 })];
    const once = deductStockFifo(initial, "brand-1", 4, snapshot);
    const twice = deductStockFifo(once, "brand-1", 4, snapshot);
    expect(twice).toEqual(once);
  });
});

describe("pharmacy queue cache helpers", () => {
  const sampleQueue = [
    {
      patient: { displayId: "P001", name: "Alice", villageCode: "V1" },
      visits: [
        {
          visitId: "V001",
          visitDate: "2026-11-15",
          orders: [
            { orderId: "ord-1", medicationName: "Paracetamol", quantity: 10, dosageInstructions: "1 tab", isLowStock: false },
          ],
          diagnoses: [{ category: "Infectious", details: "Fever" }],
        },
      ],
    },
  ];

  it("upsertOrderInPharmacyQueue adds or updates orders correctly", () => {
    const updated = upsertOrderInPharmacyQueue(sampleQueue, {
      orderId: "ord-2",
      medicationName: "Amoxicillin",
      quantity: 5,
      isLowStock: false,
      dosageInstructions: "2 tabs",
      patientDisplayId: "P001",
      patientName: "Alice",
      villageCode: "V1",
      visitId: "V001",
      visitDate: "2026-11-15",
      diagnoses: [{ category: "Infectious", details: "Fever" }],
    });
    expect(updated[0]!.visits[0]!.orders).toHaveLength(2);
    expect(updated[0]!.visits[0]!.orders[1]!.orderId).toBe("ord-2");

    // Idempotent test
    const twice = upsertOrderInPharmacyQueue(updated, {
      orderId: "ord-2",
      medicationName: "Amoxicillin",
      quantity: 5,
      isLowStock: false,
      dosageInstructions: "2 tabs",
      patientDisplayId: "P001",
      patientName: "Alice",
      villageCode: "V1",
      visitId: "V001",
      visitDate: "2026-11-15",
      diagnoses: [{ id: "d1", name: "Fever" }],
    });
    expect(twice).toEqual(updated);
  });

  it("patchOrderInPharmacyQueue removes order on APPROVED or CANCELLED, updates instructions otherwise", () => {
    const patched = patchOrderInPharmacyQueue(sampleQueue, "ord-1", { dosageInstructions: "Updated instruction" });
    expect(patched[0]!.visits[0]!.orders[0]!.dosageInstructions).toBe("Updated instruction");

    const approved = patchOrderInPharmacyQueue(sampleQueue, "ord-1", { status: "APPROVED" });
    expect(approved).toHaveLength(0); // Prunes empty visit and patient group
  });

  it("removeOrderFromPharmacyQueue removes order and prunes empty branches", () => {
    const removed = removeOrderFromPharmacyQueue(sampleQueue, "ord-1");
    expect(removed).toHaveLength(0);
  });
});

