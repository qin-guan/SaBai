import type { SaBaiAPIEndpointsMedicationStockByIngredientGetResponse } from "@sabai/sdk/types";
import type { OrderFormValue } from "@/types/consults";

export type IngredientAvailability =
  SaBaiAPIEndpointsMedicationStockByIngredientGetResponse;

/**
 * What can still be ordered of an ingredient in this consult: its available
 * units (the server reserves stock only when the order is placed) less what
 * orders already staged in the form ask for.
 */
export function remainingFor(
  ingredient: IngredientAvailability,
  stagedOrders: readonly Pick<
    OrderFormValue,
    "activeIngredientId" | "quantity"
  >[],
) {
  const staged = stagedOrders
    .filter((order) => order.activeIngredientId === ingredient.ingredientId)
    .reduce((total, order) => total + order.quantity, 0);
  return Math.max(0, ingredient.availableQuantity - staged);
}

/** Dropdown options: every ingredient, out-of-stock ones listed but disabled. */
export function ingredientOptions(
  ingredients: readonly IngredientAvailability[],
  stagedOrders: readonly Pick<
    OrderFormValue,
    "activeIngredientId" | "quantity"
  >[],
) {
  return ingredients.map((ingredient) => {
    const remaining = remainingFor(ingredient, stagedOrders);
    return {
      value: ingredient.ingredientId,
      label:
        remaining > 0
          ? `${ingredient.ingredientName} (${ingredient.unitOfMeasurement}) (Qty: ${remaining})`
          : `${ingredient.ingredientName} (${ingredient.unitOfMeasurement}) — out of stock`,
      disabled: remaining <= 0,
    };
  });
}
