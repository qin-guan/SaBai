import {
  adjustAvailability,
  applyAllocations,
  applySplit,
  hasPendingDelta,
  patchStock,
  upsertById,
  upsertStock,
  withLowStockFlags,
  withPendingDelta,
  upsertOrderInPharmacyQueue,
  patchOrderInPharmacyQueue,
  removeOrderFromPharmacyQueue,
} from "~/utils/medicationCache";
import type { IngredientAvailability, StockRowData } from "~/utils/medicationCache";

function line(overrides: Partial<StockRowData> = {}): StockRowData {
  return {
    id: "00000000-0000-7000-8000-000000000001",
    medicationBrandId: "brand-1",
    quantity: 10,
    reservedQuantity: 0,
    availableQuantity: 10,
    expiry: "2030-01-01",
    location: "Shelf A",
    stockStatus: "active",
    remarks: null,
    parentStockId: null,
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
      line({ id: "1", quantity: 2, availableQuantity: 2 }),
      line({ id: "2", quantity: 3, availableQuantity: 3 }),
      line({ id: "3", quantity: 100, availableQuantity: 100, stockStatus: "discarded" }),
    ];
    expect(withLowStockFlags(rows).map((r) => r.isLowStock)).toEqual([true, true, false]);
  });

  it("counts only the units not reserved for pending orders", () => {
    const rows = [line({ id: "1", quantity: 20, reservedQuantity: 16, availableQuantity: 4 })];
    expect(withLowStockFlags(rows)[0]!.isLowStock).toBe(true);
  });

  it("clears the flag when stock is enough", () => {
    const rows = [line({ id: "1", quantity: 6, availableQuantity: 6, isLowStock: true })];
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

  it("keeps a parent's reserved units in the parent", () => {
    const held = line({ id: "p", quantity: 10, reservedQuantity: 4, availableQuantity: 6 });
    const result = applySplit([held], held, [
      { id: "c1", quantity: 2, location: "A", stockStatus: "active", remarks: null },
      { id: "c2", quantity: 4, location: "B", stockStatus: "donated", remarks: null },
    ]);
    expect(result.find((r) => r.id === "p")).toMatchObject({ quantity: 4, reservedQuantity: 4, availableQuantity: 0 });
    expect(result.find((r) => r.id === "c1")).toMatchObject({ quantity: 2, reservedQuantity: 0, availableQuantity: 2, parentStockId: "p" });
  });

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

describe("withPendingDelta", () => {
  const row = { id: "a", quantity: 10, reservedQuantity: 2 };

  it("applies a change once per key, from the server's values", () => {
    const once = withPendingDelta(row, "k1", { quantity: -3 });
    expect(once.quantity).toBe(7);
    expect(withPendingDelta(once, "k1", { quantity: -3 })).toBe(once);
    const both = withPendingDelta(once, "k2", { quantity: -1, reservedQuantity: 1 });
    expect(both).toMatchObject({ quantity: 6, reservedQuantity: 3 });
    expect(hasPendingDelta(both)).toBe(true);
  });

  it("removing every change restores the server's values and drops the bookkeeping", () => {
    const changed = withPendingDelta(withPendingDelta(row, "k1", { quantity: -3 }), "k2", { reservedQuantity: 1 });
    const back = withPendingDelta(withPendingDelta(changed, "k1", null), "k2", null);
    expect(back).toEqual(row);
    expect(hasPendingDelta(back)).toBe(false);
  });

  it("leaves a row without the key alone when removing", () => {
    expect(withPendingDelta(row, "missing", null)).toBe(row);
  });
});

describe("adjustAvailability", () => {
  const rows: IngredientAvailability[] = [
    { ingredientId: "i1", ingredientName: "A", unitOfMeasurement: "tablet", availableQuantity: 10 },
    { ingredientId: "i2", ingredientName: "B", unitOfMeasurement: "ml", availableQuantity: 0 },
  ];

  it("changes only the ingredient, once per key", () => {
    const once = adjustAvailability(rows, "i1", "order:1", -4);
    expect(adjustAvailability(once, "i1", "order:1", -4)).toEqual(once);
    expect(once.map((r) => r.availableQuantity)).toEqual([6, 0]);
    expect(adjustAvailability(once, "i1", "order:1", null).map((r) => r.availableQuantity)).toEqual([10, 0]);
  });
});

describe("applyAllocations", () => {
  const reserved = [
    line({ id: "b1", quantity: 10, reservedQuantity: 4, availableQuantity: 6 }),
    line({ id: "b2", quantity: 3, reservedQuantity: 3, availableQuantity: 0 }),
  ];
  const allocations = [
    { stockId: "b1", quantity: 4 },
    { stockId: "b2", quantity: 3 },
  ];

  it("dispensing takes the units off hand and out of the reservation, and drops emptied batches", () => {
    const once = applyAllocations(reserved, allocations, "d", "dispense");
    expect(applyAllocations(once, allocations, "d", "dispense")).toEqual(once);
    expect(once.map((r) => r.id)).toEqual(["b1"]);
    expect(once[0]).toMatchObject({ quantity: 6, reservedQuantity: 0, availableQuantity: 6 });
  });

  it("releasing makes the units available again", () => {
    const once = applyAllocations(reserved, allocations, "c", "release");
    expect(applyAllocations(once, allocations, "c", "release")).toEqual(once);
    expect(once.map((r) => [r.quantity, r.reservedQuantity, r.availableQuantity])).toEqual([[10, 0, 10], [3, 0, 3]]);
    expect(applyAllocations(once, allocations, "c", null).map((r) => r.reservedQuantity)).toEqual([4, 3]);
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
            { orderId: "ord-1", medicationName: "Paracetamol", quantity: 10, dosageInstructions: "1 tab", isLowStock: false, allocations: [] },
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
      allocations: [],
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
      allocations: [],
      patientDisplayId: "P001",
      patientName: "Alice",
      villageCode: "V1",
      visitId: "V001",
      visitDate: "2026-11-15",
      diagnoses: [{ category: "Infectious", details: "Fever" }],
    });
    expect(twice).toEqual(updated);
  });

  it("patchOrderInPharmacyQueue removes order on DISPENSED or CANCELLED, updates fields otherwise", () => {
    const patched = patchOrderInPharmacyQueue(sampleQueue, "ord-1", { dosageInstructions: "Updated instruction", quantity: 4, medicationName: "Ibuprofen" });
    expect(patched[0]!.visits[0]!.orders[0]).toMatchObject({ dosageInstructions: "Updated instruction", quantity: 4, medicationName: "Ibuprofen" });

    expect(patchOrderInPharmacyQueue(sampleQueue, "ord-1", { status: "DISPENSED" })).toHaveLength(0); // Prunes empty visit and patient group
    expect(patchOrderInPharmacyQueue(sampleQueue, "ord-1", { status: "CANCELLED" })).toHaveLength(0);
  });

  it("removeOrderFromPharmacyQueue removes order and prunes empty branches", () => {
    const removed = removeOrderFromPharmacyQueue(sampleQueue, "ord-1");
    expect(removed).toHaveLength(0);
  });
});

