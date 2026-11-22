import { describe, expect, it } from "vitest";
import { orderableIngredients, orderableLabel, orderQuantityError, stagedQuantities } from "~/utils/ordering";
import type { IngredientAvailability } from "~/utils/medicationCache";

const rows: IngredientAvailability[] = [
  { ingredientId: "p", ingredientName: "Paracetamol", unitOfMeasurement: "tablet", availableQuantity: 20 },
  { ingredientId: "a", ingredientName: "Amoxicillin", unitOfMeasurement: "capsule", availableQuantity: 0 },
];

describe("orderableIngredients", () => {
  it("lists every ingredient by name and marks the ones with nothing left", () => {
    const options = orderableIngredients(rows);
    expect(options.map((o) => [o.name, o.remaining, o.outOfStock])).toEqual([
      ["Amoxicillin", 0, true],
      ["Paracetamol", 20, false],
    ]);
  });

  it("takes off what other lines of the same form ask for", () => {
    const options = orderableIngredients(rows, { staged: new Map([["p", 15]]) });
    expect(options.find((o) => o.ingredientId === "p")!.remaining).toBe(5);
    const all = orderableIngredients(rows, { staged: new Map([["p", 20]]) });
    expect(all.find((o) => o.ingredientId === "p")).toMatchObject({ remaining: 0, outOfStock: true });
  });

  it("counts the units an order already holds as available to that order", () => {
    const options = orderableIngredients(rows, { holding: { ingredientId: "a", quantity: 6 } });
    expect(options.find((o) => o.ingredientId === "a")).toMatchObject({ remaining: 6, outOfStock: false });
  });
});

describe("orderableLabel", () => {
  it("says what is available or that it is out of stock", () => {
    const [amox, para] = orderableIngredients(rows);
    expect(orderableLabel(para!)).toBe("Paracetamol — 20 tablet available");
    expect(orderableLabel(amox!)).toBe("Amoxicillin — out of stock");
  });
});

describe("stagedQuantities", () => {
  it("sums valid lines by ingredient", () => {
    const totals = stagedQuantities([
      { ingredientId: "p", quantity: 2 },
      { ingredientId: "p", quantity: 3 },
      { ingredientId: "", quantity: 4 },
      { ingredientId: "a", quantity: Number.NaN },
    ]);
    expect([...totals]).toEqual([["p", 5]]);
  });
});

describe("orderQuantityError", () => {
  const option = { remaining: 5, unit: "tablet" };

  it("requires a whole number of at least 1", () => {
    expect(orderQuantityError("", option)).toBe("Quantity is required");
    expect(orderQuantityError("1.5", option)).toBe("Quantity must be a whole number of at least 1");
    expect(orderQuantityError("0", option)).toBe("Quantity must be a whole number of at least 1");
  });

  it("caps the quantity at what is available", () => {
    expect(orderQuantityError("5", option)).toBeNull();
    expect(orderQuantityError("6", option)).toBe("Only 5 tablet available");
    expect(orderQuantityError("1", { remaining: 0, unit: "tablet" })).toBe("Out of stock: choose another medication");
  });
});
