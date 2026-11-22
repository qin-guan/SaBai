/** An order staged in the consult form; placed after the consult is saved. */
export type OrderFormValue = {
  activeIngredientId: string;
  /** Only so the staged list can show the name. */
  activeIngredientName: string;
  unitOfMeasurement: string;
  quantity: number;
  dosageInstructions: string;
};
