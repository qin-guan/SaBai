import { useState } from "react";
import {
  FormProvider,
  useForm,
  SubmitHandler,
  useWatch,
} from "react-hook-form";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import { zodResolver } from "@hookform/resolvers/zod";
import TableRow from "@/components/TableRow";
import TableCell from "@/components/TableCell";
import EditableCell from "@/components/interactive/EditableCell";
import DeleteConfirmModal from "@/components/medication-brands/DeleteConfirmModal";
import { Button } from "@/components/interactive/Button/Button";
import type { SaBaiAPIEndpointsMedicationBrandsListGetResponse } from "@sabai/sdk/types";
import {
  saBaiAPIEndpointsMedicationBrandsDeleteDeleteEndpointMutationOptions,
  saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey,
  saBaiAPIEndpointsMedicationBrandsUpdatePatchEndpoint1MutationOptions,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { editBrandFormSchema } from "@/lib/validation/medicationBrands";
import type { EditFormFields } from "./types";
import IngredientDropdown from "./IngredientDropdown";

export type BrandRowProps = SaBaiAPIEndpointsMedicationBrandsListGetResponse & {
  activeIngredientName: string;
  unitOfMeasurement: string;
};

export default function BrandRow(brand: BrandRowProps) {
  const [isEditing, setIsEditing] = useState(false);
  const [isDeleting, setIsDeleting] = useState<boolean>(false);

  const form = useForm<EditFormFields>({
    resolver: zodResolver(editBrandFormSchema),
    values: {
      id: brand.id,
      name: brand.name,
      activeIngredientId: brand.activeIngredientId,
      remarks: brand.remarks ?? "",
    },
  });

  const {
    data: ingredients,
    isLoading,
    isError,
  } = useQuery({
    ...saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryOptions(
      apiConfig,
    ),
    enabled: isEditing,
  });

  const selectedIngredientId = useWatch({
    control: form.control,
    name: "activeIngredientId",
  });
  const selectedIngredient = ingredients?.find(
    (ingredient) => ingredient.id === selectedIngredientId,
  );

  const queryClient = useQueryClient();

  const updateMutation = useMutation({
    ...saBaiAPIEndpointsMedicationBrandsUpdatePatchEndpoint1MutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully updated!");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey(),
      });
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, "An error has occurred."));
      console.error(err);
      form.reset();
    },
  });

  const deleteMutation = useMutation({
    ...saBaiAPIEndpointsMedicationBrandsDeleteDeleteEndpointMutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully deleted.");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationBrandsListGetEndpointQueryKey(),
      });
    },
    onError: (err) => {
      toast.error(
        getApiErrorMessage(
          err,
          "Error! Check that there are no stock records that depend on this brand.",
        ),
      );
      setIsDeleting(false);
    },
  });

  const isSubmitting = form.formState.isSubmitting;

  const handleSubmit: SubmitHandler<EditFormFields> = async (data) => {
    const hasChanged =
      data.name !== brand.name ||
      data.activeIngredientId !== brand.activeIngredientId ||
      (data.remarks ?? "") !== (brand.remarks ?? "");

    if (!hasChanged) {
      toast.error("No form field changed!");
      return;
    }

    updateMutation.mutate({
      path: { id: brand.id },
      body: {
        name: data.name,
        activeIngredientId: data.activeIngredientId,
        // the API requires remarks, so a blank value is sent as an empty string
        remarks: data.remarks ?? "",
      },
    });

    setIsEditing(!isEditing);
  };

  return (
    <TableRow>
      <FormProvider {...form}>
        <TableCell>
          <span className="text-sm font-medium text-slate-900">{brand.id}</span>
        </TableCell>

        <TableCell>
          <span className="text-sm font-medium text-slate-900">
            <EditableCell
              isEditing={isEditing}
              name="name"
              type="text"
              value={form.getValues().name}
              label=""
            />
          </span>
        </TableCell>

        <TableCell>
          {isEditing ? (
            <div className="text-sm font-medium text-slate-900">
              <IngredientDropdown
                ingredients={ingredients}
                isLoading={isLoading}
                isError={isError}
              />
            </div>
          ) : (
            <span className="text-sm font-medium text-slate-900">
              {brand.activeIngredientName}
            </span>
          )}
        </TableCell>

        <TableCell>
          <span className="text-sm font-medium text-slate-900">
            {isEditing
              ? (selectedIngredient?.unitOfMeasurement ??
                brand.unitOfMeasurement)
              : brand.unitOfMeasurement}
          </span>
        </TableCell>

        <TableCell className="!whitespace-normal max-w-xs break-words">
          <span className="text-sm font-medium text-slate-900">
            <EditableCell
              isEditing={isEditing}
              name="remarks"
              type="text"
              value={form.getValues().remarks}
              label=""
              multiline
            />
          </span>
        </TableCell>

        <TableCell>
          <div className="flex items-left gap-2">
            {isEditing ? (
              <Button
                colour="emerald"
                disabled={isSubmitting}
                onClick={() => form.handleSubmit(handleSubmit)()}
                title={isSubmitting ? "Saving..." : "Save"}
              />
            ) : (
              <Button
                colour="emerald"
                onClick={() => setIsEditing(!isEditing)}
                title="Edit"
              />
            )}

            {isEditing ? (
              <Button
                onClick={() => {
                  form.reset();
                  setIsEditing(false);
                }}
                colour="red"
                title="Cancel"
              />
            ) : (
              <Button
                onClick={() => {
                  setIsDeleting(true);
                }}
                colour="red"
                title="Delete"
              />
            )}

            {isDeleting && (
              <DeleteConfirmModal
                onCancel={() => setIsDeleting(false)}
                onConfirm={() =>
                  deleteMutation.mutate({ path: { id: brand.id } })
                }
                brand={brand}
              />
            )}
          </div>
        </TableCell>
      </FormProvider>
    </TableRow>
  );
}
