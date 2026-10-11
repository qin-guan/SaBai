import { useState } from "react";
import { FormProvider, useForm, SubmitHandler } from "react-hook-form";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import TableRow from "@/components/TableRow";
import TableCell from "@/components/TableCell";
import EditableCell from "@/components/interactive/EditableCell";
import { Button } from "@/components/interactive/Button/Button";
import type { SaBaiAPIEndpointsMedicationIngredientsListGetResponse } from "@sabai/sdk/types";
import {
  saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpointMutationOptions,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryKey,
  saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { positiveNumberOptions } from "@/lib/utils/medication";
import DeleteConfirmModal from "./DeleteConfirmModal";
import { EditFormFields } from "./types";

export default function IngredientRow(
  ingredient: SaBaiAPIEndpointsMedicationIngredientsListGetResponse,
) {
  const [isEditing, setIsEditing] = useState<boolean>(false);
  const [isDeleting, setIsDeleting] = useState<boolean>(false);

  const form = useForm<EditFormFields>({
    values: {
      id: ingredient.id,
      name: ingredient.name,
      unitOfMeasurement: ingredient.unitOfMeasurement,
      fallBelow: ingredient.fallBelow,
      remarks: ingredient.remarks ?? "",
    },
  });

  const queryClient = useQueryClient();
  const updateMutation = useMutation({
    ...saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpointMutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully updated!");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryKey(),
      });
    },
    onError: (err) => {
      toast.error(getApiErrorMessage(err, "An error has occurred."));
      console.error(err);
      form.reset();
    },
  });

  const deleteMutation = useMutation({
    ...saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpointMutationOptions(
      apiConfig,
    ),
    onSuccess: () => {
      toast.success("Successfully deleted.");
      queryClient.invalidateQueries({
        queryKey: saBaiAPIEndpointsMedicationIngredientsListGetEndpointQueryKey(),
      });
    },
    onError: (err) => {
      toast.error(
        getApiErrorMessage(
          err,
          "Error! Check that there are no brands that depend on this active ingredient.",
        ),
      );
      setIsDeleting(false);
    },
  });

  const isSubmitting = form.formState.isSubmitting;

  const handleSubmit: SubmitHandler<EditFormFields> = async (data) => {
    const hasChanged =
      data.name !== ingredient.name ||
      data.unitOfMeasurement !== ingredient.unitOfMeasurement ||
      data.fallBelow !== ingredient.fallBelow ||
      data.remarks !== ingredient.remarks;

    if (!hasChanged) {
      toast.error("No form field changed!");
      return;
    }

    updateMutation.mutate({
      path: { id: ingredient.id },
      body: {
        name: data.name,
        unitOfMeasurement: data.unitOfMeasurement,
        fallBelow: data.fallBelow,
        remarks: data.remarks,
      },
    });

    setIsEditing(!isEditing);
  };

  return (
    <TableRow>
      <FormProvider {...form}>
        <TableCell>
          <span className="text-sm font-medium text-slate-900">
            {ingredient.id}
          </span>
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
          <span className="text-sm font-medium text-slate-900">
            <EditableCell
              isEditing={isEditing}
              name="unitOfMeasurement"
              type="text"
              value={form.getValues().unitOfMeasurement}
              label=""
            />
          </span>
        </TableCell>
        <TableCell>
          <span className="text-sm font-medium text-slate-900">
            <EditableCell
              isEditing={isEditing}
              name="fallBelow"
              type="number"
              value={form.getValues().fallBelow}
              label=""
              registerOptions={positiveNumberOptions}
            />
          </span>
        </TableCell>
        <TableCell>
          <span className="text-sm font-medium text-slate-900">
            <EditableCell
              isEditing={isEditing}
              name="remarks"
              type="text"
              value={form.getValues().remarks}
              label=""
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
                  deleteMutation.mutate({ path: { id: ingredient.id } })
                }
                ingredient={ingredient}
              />
            )}
          </div>
        </TableCell>
      </FormProvider>
    </TableRow>
  );
}
