import { describe, expect, it } from "vitest";
import {
  dispensedUnits,
  formatQuantityChange,
  groupStockChanges,
  totalDispensed,
  type StockChangeRow,
} from "~/utils/stockChanges";

let nextId = 1;
function row(overrides: Partial<StockChangeRow>): StockChangeRow {
  return {
    id: `c${nextId++}`,
    stockId: "s1",
    kind: "edit",
    field: "remarks",
    previousValue: "",
    newValue: "",
    quantityChange: null,
    quantityAfter: null,
    userId: "u1",
    userName: "Pharmacist",
    createdAt: "2026-11-22T10:00:00Z",
    dispensing: null,
    ...overrides,
  };
}

const dispensing = {
  orderId: "o1",
  orderQuantity: 20,
  consultId: "c1",
  visitId: "v1",
  visitDate: "2026-11-22T08:00:00Z",
  patientId: "p1",
  patientName: "Sok Dara",
  patientDisplayId: "KS0001",
};

// Newest first, as the API returns them.
const log: StockChangeRow[] = [
  row({ kind: "dispensed", field: "quantity", previousValue: "116", newValue: "96", quantityChange: -20, quantityAfter: 96, createdAt: "2026-11-22T12:00:00Z", dispensing }),
  row({ kind: "edit", field: "location", previousValue: "Shelf A1", newValue: "Shelf B2", createdAt: "2026-11-22T11:30:00Z" }),
  row({ kind: "edit", field: "stock_status", previousValue: "active", newValue: "discarded", createdAt: "2026-11-22T11:30:00Z" }),
  row({ kind: "edit", field: "remarks", previousValue: "", newValue: "Stock count: 4 damaged", createdAt: "2026-11-22T11:00:00Z" }),
  row({ kind: "adjustment", field: "quantity", previousValue: "120", newValue: "116", quantityChange: -4, quantityAfter: 116, createdAt: "2026-11-22T11:00:00Z" }),
  row({ kind: "created", field: "stock_status", newValue: "active", createdAt: "2026-11-22T10:00:00Z" }),
  row({ kind: "created", field: "location", newValue: "Shelf A1", createdAt: "2026-11-22T10:00:00Z" }),
  row({ kind: "created", field: "quantity", previousValue: "0", newValue: "120", quantityChange: 120, quantityAfter: 120, createdAt: "2026-11-22T10:00:00Z" }),
];

describe("groupStockChanges", () => {
  const events = groupStockChanges(log);

  it("collapses rows written together into one event, newest first", () => {
    expect(events.map((e) => e.kind)).toEqual(["dispensed", "edit", "adjustment", "created"]);
  });

  it("creating a batch is one event with its quantity and where it was put", () => {
    expect(events[3]).toMatchObject({
      kind: "created",
      quantityChange: 120,
      quantityAfter: 120,
      details: ["Location: Shelf A1", "Status: active"],
    });
  });

  it("an adjustment carries its reason", () => {
    expect(events[2]).toMatchObject({
      kind: "adjustment",
      quantityChange: -4,
      quantityAfter: 116,
      details: ["Remarks: Stock count: 4 damaged"],
    });
  });

  it("an edit says what changed from what to what, without a quantity", () => {
    expect(events[1]).toMatchObject({
      kind: "edit",
      quantityChange: null,
      quantityAfter: null,
      details: ["Location: Shelf A1 → Shelf B2", "Status: active → discarded"],
    });
  });

  it("a dispensing keeps who it was for", () => {
    expect(events[0]!.dispensing?.patientName).toBe("Sok Dara");
    expect(dispensedUnits(events[0]!)).toBe(20);
  });

  it("never merges two quantity changes, even at the same instant", () => {
    const sameInstant = groupStockChanges([
      row({ kind: "dispensed", field: "quantity", quantityChange: -5, quantityAfter: 5, dispensing }),
      row({ kind: "dispensed", field: "quantity", quantityChange: -10, quantityAfter: 10, dispensing }),
    ]);
    expect(sameInstant).toHaveLength(2);
    expect(totalDispensed(sameInstant)).toBe(15);
  });

  it("does not merge rows of different users", () => {
    expect(groupStockChanges([
      row({ field: "location", previousValue: "A", newValue: "B" }),
      row({ field: "remarks", newValue: "x", userId: "u2" }),
    ])).toHaveLength(2);
  });

  it("says when remarks were removed", () => {
    expect(groupStockChanges([row({ field: "remarks", previousValue: "old note", newValue: "" })])[0]!.details).toEqual([
      "Remarks removed (was old note)",
    ]);
  });
});

describe("totals and signs", () => {
  it("sums only the dispensings", () => {
    expect(totalDispensed(groupStockChanges(log))).toBe(20);
    expect(totalDispensed([])).toBe(0);
  });

  it("writes a change with its sign and unit", () => {
    expect(formatQuantityChange(10, "tablet")).toBe("+10 tablets");
    expect(formatQuantityChange(-1, "tablet")).toBe("−1 tablet");
    expect(formatQuantityChange(-5, "mL")).toBe("−5 mL");
    expect(formatQuantityChange(0, "mL")).toBe("");
    expect(formatQuantityChange(null)).toBe("");
  });
});
