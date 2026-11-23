import { describe, expect, it } from "vitest";
import {
  brandNamesByIngredient,
  matchingBrandNames,
  orderableAvailability,
  orderableIngredients,
  orderableLabel,
  orderQuantityError,
  searchOrderableIngredients,
  stagedQuantities,
} from "~/utils/ordering";
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
    expect(orderableLabel(para!)).toBe("Paracetamol — 20 tablets available");
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
    expect(orderQuantityError("6", option)).toBe("Only 5 tablets available");
    expect(orderQuantityError("1", { remaining: 0, unit: "tablet" })).toBe("Out of stock: choose another medication");
  });
});

describe("orderableAvailability", () => {
  it("names the unit", () => {
    expect(orderableAvailability({ remaining: 1, unit: "tablet", outOfStock: false })).toBe("1 tablet available");
    expect(orderableAvailability({ remaining: 60, unit: "mL", outOfStock: false })).toBe("60 mL available");
    expect(orderableAvailability({ remaining: 0, unit: "mL", outOfStock: true })).toBe("Out of stock");
  });
});

describe("brand names from the availability rows", () => {
  const withBrands = [
    { ...rows[0]!, brandNames: ["Panadol", "Calpol"] },
    { ...rows[1]!, brandNames: [] },
  ];

  it("uses the names each row carries, sorted, so any ordering role can search by brand", () => {
    const options = orderableIngredients(withBrands);
    expect(options.find((o) => o.ingredientId === "p")!.brandNames).toEqual(["Calpol", "Panadol"]);
    expect(searchOrderableIngredients(options, "calpol").map((o) => o.name)).toEqual(["Paracetamol"]);
    expect(matchingBrandNames(options.find((o) => o.ingredientId === "p")!, "pana")).toEqual(["Panadol"]);
  });

  it("an explicit brand map still wins", () => {
    const options = orderableIngredients(withBrands, { brands: new Map([["p", ["Tylenol"]]]) });
    expect(options.find((o) => o.ingredientId === "p")!.brandNames).toEqual(["Tylenol"]);
  });
});

describe("searching the medication picker", () => {
  const brands = brandNamesByIngredient([
    { name: "Panadol", activeIngredientId: "p" },
    { name: "Calpol", activeIngredientId: "p" },
    { name: "Panadol", activeIngredientId: "p" },
    { name: "Amoxil", activeIngredientId: "a" },
  ]);
  const options = orderableIngredients(
    [...rows, { ingredientId: "c", ingredientName: "Co-amoxiclav 625 mg", unitOfMeasurement: "tablet", availableQuantity: 4 }],
    { brands },
  );
  const names = (query: string) => searchOrderableIngredients(options, query).map((o) => o.name);

  it("groups brand names by ingredient, sorted and without repeats", () => {
    expect(brands.get("p")).toEqual(["Calpol", "Panadol"]);
    expect(options.find((o) => o.ingredientId === "p")?.brandNames).toEqual(["Calpol", "Panadol"]);
    expect(options.find((o) => o.ingredientId === "c")?.brandNames).toEqual([]);
  });

  it("lists everything for an empty query", () => {
    expect(names("  ")).toEqual(["Amoxicillin", "Co-amoxiclav 625 mg", "Paracetamol"]);
  });

  it("matches the ingredient name anywhere, ignoring case, names that start with it first", () => {
    expect(names("AMOX")).toEqual(["Amoxicillin", "Co-amoxiclav 625 mg"]);
    expect(names("cetam")).toEqual(["Paracetamol"]);
  });

  it("matches brand names", () => {
    expect(names("panadol")).toEqual(["Paracetamol"]);
    expect(names("amoxil")).toEqual(["Amoxicillin"]);
    expect(matchingBrandNames(options.find((o) => o.ingredientId === "p")!, "pana")).toEqual(["Panadol"]);
    expect(matchingBrandNames(options.find((o) => o.ingredientId === "p")!, "")).toEqual([]);
  });

  it("needs every word to match", () => {
    expect(names("amox 625")).toEqual(["Co-amoxiclav 625 mg"]);
    expect(names("amox zzz")).toEqual([]);
  });
});
