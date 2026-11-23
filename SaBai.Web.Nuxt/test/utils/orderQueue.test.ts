import { describe, expect, it } from "vitest";
import type { PharmacyQueue } from "~/utils/medicationCache";
import { countPharmacyQueue, filterPharmacyQueue } from "~/utils/orderQueue";

const order = (orderId: string, medicationName: string, extra: Partial<PharmacyQueue["visits"][number]["orders"][number]> = {}) => ({
  orderId,
  medicationActiveIngredientId: `ing-${orderId}`,
  medicationName,
  unitOfMeasurement: "tablet",
  quantity: 10,
  isLowStock: false,
  dosageInstructions: "1 tablet daily",
  allocations: [],
  ...extra,
});

const queue: PharmacyQueue[] = [
  {
    patient: { displayId: "KS0001", name: "Sok Dara", villageCode: "KS" },
    visits: [
      {
        visitId: "v1",
        visitDate: "2026-11-22T08:00:00Z",
        diagnoses: [{ category: "Cardiovascular", details: "Hypertension" }],
        orders: [
          order("o1", "Amlodipine 5mg"),
          order("o2", "Paracetamol 500mg", {
            allocations: [{ stockId: "s1", medicationBrandId: "b1", brandName: "Panadol", location: "Shelf A1", expiry: "2027-01-06T00:00:00Z", quantity: 10 }],
          }),
        ],
      },
      { visitId: "v0", visitDate: "2026-11-01T08:00:00Z", diagnoses: [], orders: [order("o0", "Metformin 500mg")] },
    ],
  },
  {
    patient: { displayId: "PC0007", name: "Chan Sreymom", villageCode: "PC" },
    visits: [
      {
        visitId: "v2",
        visitDate: "2026-11-22T09:00:00Z",
        diagnoses: [],
        orders: [order("o3", "Cetirizine 10mg", { isLowStock: true, dosageInstructions: "1 tablet at night" })],
      },
    ],
  },
];

const ids = (result: PharmacyQueue[]) => result.flatMap((g) => g.visits.flatMap((v) => v.orders.map((o) => o.orderId)));

describe("filterPharmacyQueue", () => {
  it("returns the whole queue without a filter", () => {
    expect(ids(filterPharmacyQueue(queue, {}))).toEqual(["o1", "o2", "o0", "o3"]);
    expect(countPharmacyQueue(queue)).toEqual({ patients: 2, orders: 4 });
  });

  it("a patient or diagnosis match keeps the visit's orders; otherwise orders match one by one", () => {
    expect(ids(filterPharmacyQueue(queue, { query: "sok" }))).toEqual(["o1", "o2", "o0"]);
    expect(ids(filterPharmacyQueue(queue, { query: "ks0001" }))).toEqual(["o1", "o2", "o0"]);
    expect(ids(filterPharmacyQueue(queue, { query: "hypertension" }))).toEqual(["o1", "o2"]);
    expect(ids(filterPharmacyQueue(queue, { query: "paracetamol" }))).toEqual(["o2"]);
    expect(ids(filterPharmacyQueue(queue, { query: "panadol" }))).toEqual(["o2"]);
    expect(ids(filterPharmacyQueue(queue, { query: "at night" }))).toEqual(["o3"]);
    expect(ids(filterPharmacyQueue(queue, { query: "with food" }, { remarksOf: (id) => (id === "o0" ? "Take with food" : null) }))).toEqual(["o0"]);
  });

  it("filters by village and low stock, dropping emptied visits and patients", () => {
    expect(filterPharmacyQueue(queue, { villageCode: "PC" }).map((g) => g.patient.name)).toEqual(["Chan Sreymom"]);
    const low = filterPharmacyQueue(queue, { lowStockOnly: true });
    expect(low).toHaveLength(1);
    expect(ids(low)).toEqual(["o3"]);
  });

  it("follows a deep link to a visit or a patient", () => {
    expect(ids(filterPharmacyQueue(queue, { visitId: "v0" }))).toEqual(["o0"]);
    expect(ids(filterPharmacyQueue(queue, { patient: "PC0007" }))).toEqual(["o3"]);
    expect(ids(filterPharmacyQueue(queue, { patient: "patient-uuid" }, { patientIdOf: (d) => (d === "KS0001" ? "patient-uuid" : null) }))).toEqual(["o1", "o2", "o0"]);
    expect(filterPharmacyQueue(queue, { patient: "nobody" })).toEqual([]);
  });
});
