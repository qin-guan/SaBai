import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpointQueryOptions,
  saBaiAPIEndpointsMedicationStockCreatePostEndpointMutationOptions,
  saBaiAPIEndpointsMedicationStockWithBrandGetEndpointQueryKey,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import toast from "react-hot-toast";
import { useForm, FormProvider, SubmitHandler } from "react-hook-form";
import Modal from "@/components/interactive/Modal";
import { RHFInput } from "@/components/interactive/RHF/RHFInput";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import LoadingSpinner from "@/components/LoadingSpinner";
import {
  stockStatusDropdown,
  CreateFormFields,
} from "@/types/medication-stock";
import { Button } from "@/components/interactive/Button/Button";

export default function CreateModal({
  setModalIsOpen,
}: {
  setModalIsOpen: React.Dispatch<React.SetStateAction<boolean>>;
}) {
  const queryClient = useQueryClient();

  const form = useForm<CreateFormFields>();

  const {
    data: brandWithActiveIngredientData,
    isLoading: brandWithActiveIngredientIsLoading,
    isError: brandWithActiveIngredientIsError,
  } = useQuery(
    saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpointQueryOptions(
      apiConfig,
    ),
  );

  const createMutation = useMutation({
    ...saBaiAPIEndpointsMedicationStockCreatePostEndpointMutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully created!");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationStockWithBrandGetEndpointQueryKey(),
      });
      form.reset();
      setModalIsOpen(false);
    },
    onError: (err) => {
      console.error(err);
      toast.error(getApiErrorMessage(err, "An error has occurred."));
    },
  });

  const handleSubmit: SubmitHandler<CreateFormFields> = async (data) => {
    createMutation.mutate({
      body: {
        medicationBrandId: Number(data.medicationBrandId),
        quantity: data.quantity,
        expiry: data.expiry,
        location: data.location,
        stockStatus: data.stockStatus,
        remarks: data.remarks,
      },
    });
  };

  return (
    <Modal onClose={() => setModalIsOpen(false)}>
      <h2 className="text-xl font-bold tracking-tight text-slate-900 mb-4">
        Add New Stock
      </h2>
      <FormProvider {...form}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            form.handleSubmit(handleSubmit)(e);
          }}
        >
          <RHFInput
            name="location"
            label="Location"
            type="text"
            className="mb-4"
            isRequired
          />
          <RHFInput
            name="quantity"
            label="Quantity"
            type="number"
            registerOptions={{
              valueAsNumber: true,
              min: {
                value: 1,
                message: "Please input only positive values.",
              },
            }}
            className="mb-4"
            isRequired
          />
          <RHFInput
            name="expiry"
            label="Expiry Date"
            type="date"
            className="mb-4"
            isRequired
          />

          {brandWithActiveIngredientIsLoading && (
            <LoadingSpinner message="Loading brands with active ingredients..." />
          )}

          {brandWithActiveIngredientIsError &&
            "An error has occurred while loading the dropdown options for brand + active ingredient. Please refresh the page."}

          {!brandWithActiveIngredientIsError &&
            !brandWithActiveIngredientIsLoading && (
              <RHFDropdown
                name="medicationBrandId"
                label="Brand + Active Ingredient"
                dropdownOptions={brandWithActiveIngredientData!.map((elem) => ({
                  value: elem.id.toString(),
                  label: `${elem.activeIngredientName} (${elem.name})`,
                }))}
                className="mb-4"
                isRequired
              />
            )}

          <RHFDropdown
            name="stockStatus"
            label="Status"
            dropdownOptions={stockStatusDropdown}
            className="mb-4"
            isRequired
          />
          <RHFInput
            name="remarks"
            label="Remarks"
            type="text"
            className="mb-8"
          />
          <div className="flex flex-row gap-2">
            <Button
              colour="emerald"
              disabled={createMutation.isPending}
              type="submit"
              title={createMutation.isPending ? "Saving..." : "Save"}
            />
            <Button
              title="Cancel"
              colour="red"
              onClick={() => {
                form.reset();
                setModalIsOpen(false);
              }}
            />
          </div>
        </form>
      </FormProvider>
    </Modal>
  );
}
