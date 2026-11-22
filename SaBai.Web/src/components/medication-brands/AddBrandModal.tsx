import { useState } from "react";
import { FormProvider, useForm, SubmitHandler } from "react-hook-form";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import { zodResolver } from "@hookform/resolvers/zod";
import Modal from "@/components/interactive/Modal";
import ModalToast from "@/components/interactive/ModalToast";
import { RHFInput } from "@/components/interactive/RHF/RHFInput";
import { Button } from "@/components/interactive/Button/Button";
import {
  saBaiAPIEndpointsMedicationBrandsCreatePostEndpointMutationOptions,
  saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { brandFormSchema } from "@/lib/validation/medicationBrands";
import type { AddFormFields } from "./types";
import IngredientDropdown from "./IngredientDropdown";

export default function AddBrandModal() {
  const [modalIsOpen, setModalIsOpen] = useState<boolean>(false);

  const form = useForm<AddFormFields>({
    resolver: zodResolver(brandFormSchema),
  });

  const queryClient = useQueryClient();

  const {
    data: ingredients,
    isLoading,
    isError,
  } = useQuery({
    ...saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions(
      apiConfig,
    ),
    enabled: modalIsOpen,
  });

  const createMutation = useMutation({
    ...saBaiAPIEndpointsMedicationBrandsCreatePostEndpointMutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully created!");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey(),
      });
      form.reset();
      setModalIsOpen(false);
    },
    onError: (err) => {
      console.error(err);
      toast.error(getApiErrorMessage(err, "An error has occurred."), {
        toasterId: "modal",
      });
    },
  });

  const handleSubmit: SubmitHandler<AddFormFields> = async (data) => {
    createMutation.mutate({
      body: {
        name: data.name,
        activeIngredientId: data.activeIngredientId,
        // the API requires remarks, so a blank value is sent as an empty string
        remarks: data.remarks ?? "",
      },
    });
  };

  return (
    <>
      {modalIsOpen && (
        <Modal title="Add New Brand" onClose={() => setModalIsOpen(false)}>
          <ModalToast />
          <FormProvider {...form}>
            <form
              onSubmit={(e) => {
                form.handleSubmit(handleSubmit)(e);
              }}
            >
              <RHFInput name="name" label="Name" type="text" />

              <IngredientDropdown
                ingredients={ingredients}
                isLoading={isLoading}
                isError={isError}
                label="Active Ingredient"
                isRequired={true}
              />

              <RHFInput name="remarks" label="Remarks" type="text" />

              <div className="mt-4 flex flex-row gap-2">
                <Button
                  colour="emerald"
                  type="submit"
                  className="w-full"
                  title={createMutation.isPending ? "Saving..." : "Save"}
                  disabled={
                    createMutation.isPending ||
                    isLoading ||
                    isError ||
                    !ingredients?.length
                  }
                />
                <Button
                  colour="red"
                  title="Cancel"
                  className="w-full"
                  onClick={() => {
                    form.reset();
                    setModalIsOpen(false);
                  }}
                />
              </div>
            </form>
          </FormProvider>
        </Modal>
      )}

      <Button
        colour="indigo"
        onClick={() => setModalIsOpen(true)}
        title="Add Brand"
      />
    </>
  );
}
