import Modal from "@/components/interactive/Modal";
import { useQuery } from "@tanstack/react-query";
import { saBaiAPIEndpointsMedicationStockByIngredientGetEndpointQueryOptions } from "@sabai/sdk/hooks";
import { FormProvider, SubmitHandler, useForm } from "react-hook-form";
import LoadingSpinner from "@/components/LoadingSpinner";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import { RHFInput } from "@/components/interactive/RHF/RHFInput";
import { Button } from "@/components/interactive/Button/Button";
import { apiConfig } from "@/lib/api/config";
import { ingredientOptions, remainingFor } from "@/lib/utils/ordering";
import type { OrderFormValue } from "@/types/consults";

type OrderModalFields = {
  activeIngredientId: string;
  quantity: number;
  dosageInstructions: string;
};

/**
 * Stages a medication order for the consult being written: an active
 * ingredient (the server picks the batches when the consult is saved), a
 * quantity up to what is available and dosage instructions. Ingredients with
 * nothing left are listed but cannot be chosen.
 */
export default function OrderModal({
  setOrderModalIsOpen,
  onSave,
  stagedOrders,
}: {
  setOrderModalIsOpen: React.Dispatch<React.SetStateAction<boolean>>;
  onSave: (order: OrderFormValue) => void;
  stagedOrders: OrderFormValue[];
}) {
  const { data, isLoading, isError } = useQuery(
    saBaiAPIEndpointsMedicationStockByIngredientGetEndpointQueryOptions(
      apiConfig,
    ),
  );

  const form = useForm<OrderModalFields>({
    defaultValues: {
      activeIngredientId: "",
      quantity: undefined,
      dosageInstructions: "",
    },
  });

  const handleSubmit: SubmitHandler<OrderModalFields> = (values) => {
    const selected = data?.find(
      (ingredient) => ingredient.ingredientId === values.activeIngredientId,
    );
    if (!selected) return;

    const available = remainingFor(selected, stagedOrders);

    // A soft check only; the server enforces stock when the order is placed.
    if (values.quantity > available) {
      form.setError("quantity", {
        message:
          available === 0
            ? `${selected.ingredientName} is out of stock.`
            : `Only ${available} ${selected.unitOfMeasurement} available.`,
      });
      return;
    }

    onSave({
      activeIngredientId: values.activeIngredientId,
      activeIngredientName: selected.ingredientName,
      unitOfMeasurement: selected.unitOfMeasurement,
      quantity: values.quantity,
      dosageInstructions: values.dosageInstructions.trim(),
    });
    setOrderModalIsOpen(false);
  };

  return (
    <Modal onClose={() => setOrderModalIsOpen(false)}>
      <h2 className="text-xl font-bold mb-4">Add Order</h2>
      {isError && (
        <p className="text-red-500">
          An error has occurred while loading medicine.
        </p>
      )}
      {isLoading && (
        <LoadingSpinner message="Loading medicine..." className="p-6" />
      )}
      {!isLoading && !isError && (
        <FormProvider {...form}>
          <form onSubmit={form.handleSubmit(handleSubmit)}>
            <RHFDropdown
              name="activeIngredientId"
              label="Medicine:"
              dropdownOptions={ingredientOptions(data ?? [], stagedOrders)}
              className="mb-4"
              isRequired
            />
            <RHFInput
              name="quantity"
              label="Quantity to Order"
              className="mb-4"
              type="number"
              isRequired
              registerOptions={{
                valueAsNumber: true,
                min: {
                  value: 1,
                  message: "Please input only positive values.",
                },
                validate: (value: number) =>
                  Number.isInteger(value) || "Please input a whole number.",
              }}
            />
            <RHFInput
              name="dosageInstructions"
              label="Dosage Instructions"
              type="text"
              isRequired
              registerOptions={{
                // `required` accepts whitespace, which the server rejects after trimming
                validate: (value: string) =>
                  value.trim() !== "" || "Missing Dosage Instructions!",
              }}
              className="mb-4"
            />
            <Button colour="emerald" title="Save Order" type="submit" />
          </form>
        </FormProvider>
      )}
    </Modal>
  );
}
