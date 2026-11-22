import { FormProvider, useFieldArray, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsConsultsCreatePostEndpointMutationOptions,
  saBaiAPIEndpointsConsultsListByVisitGetEndpointQueryKey,
  saBaiAPIEndpointsMedicationStockByIngredientGetEndpointQueryKey,
  saBaiAPIEndpointsMedicationStockWithBrandGetEndpointQueryKey,
  saBaiAPIEndpointsOrdersCreatePostEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import { useState } from "react";
import toast from "react-hot-toast";
import { HiOutlineTrash } from "react-icons/hi2";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { RHFTextArea } from "@/components/interactive/RHF/RHFTextArea";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import { Button } from "@/components/interactive/Button/Button";
import { IsRequiredStar } from "@/components/IsRequiredStar";
import {
  DIAGNOSIS_CATEGORY_OPTIONS,
  DiagnosisCategory,
} from "@/lib/constants/diagnosisCategories";
import OrderModal from "./OrderModal";

/**
 * Client-side form schema. Mirrors the create-consult request (minus `visitId`,
 * which the page supplies). Category is relaxed to a plain string so the empty
 * default is valid until the user picks one; the API re-validates the enum.
 */
const consultFormSchema = z.object({
  pastMedicalHistory: z.string().trim().optional(),
  consultation: z.string().trim().optional(),
  treatmentPlan: z.string().trim().optional(),
  remarks: z.string().trim().optional(),
  diagnoses: z.array(
    z.object({
      details: z.string().trim().min(1, "Diagnosis details are required"),
      category: z.string().min(1, "Please select a category"),
    }),
  ),
  // Orders are checked in OrderModal and again by the server when placed.
  orders: z.array(
    z.object({
      activeIngredientId: z.string(),
      activeIngredientName: z.string(),
      unitOfMeasurement: z.string(),
      quantity: z.number().int().positive(),
      dosageInstructions: z.string(),
    }),
  ),
});

type ConsultFormValues = z.infer<typeof consultFormSchema>;

const BLANK_CONSULT: ConsultFormValues = {
  pastMedicalHistory: "",
  consultation: "",
  treatmentPlan: "",
  remarks: "",
  diagnoses: [{ details: "", category: "" }],
  orders: [],
};

/**
 * The consultation form for a single visit. Owns its own form state.
 * Remount via `key` (e.g. per visit) to get a fresh form.
 *
 * @param visitId - The visit this consult belongs to.
 */
export function ConsultForm({ visitId }: { visitId: string }) {
  const methods = useForm<ConsultFormValues>({
    resolver: zodResolver(consultFormSchema),
    defaultValues: BLANK_CONSULT,
  });
  const { control, handleSubmit } = methods;

  const { fields, append, remove } = useFieldArray({
    control,
    name: "diagnoses",
  });

  const {
    fields: orderFields,
    append: appendOrder,
    remove: removeOrder,
  } = useFieldArray({
    control,
    name: "orders",
  });
  const [orderModalIsOpen, setOrderModalIsOpen] = useState(false);
  const [isSaving, setIsSaving] = useState(false);

  const queryClient = useQueryClient();
  const createConsult = useMutation(
    saBaiAPIEndpointsConsultsCreatePostEndpointMutationOptions(apiConfig),
  );
  const createOrder = useMutation(
    saBaiAPIEndpointsOrdersCreatePostEndpointMutationOptions(apiConfig),
  );

  const onInvalid = () =>
    toast.error("Please fill in all required fields before saving.");

  const onSubmit = async (data: ConsultFormValues) => {
    setIsSaving(true);

    let consult;
    try {
      consult = await createConsult.mutateAsync({
        body: {
          visitId,
          pastMedicalHistory: data.pastMedicalHistory || undefined,
          consultation: data.consultation || undefined,
          treatmentPlan: data.treatmentPlan || undefined,
          remarks: data.remarks || undefined,
          diagnoses: data.diagnoses.map((d) => ({
            details: d.details,
            category: d.category as DiagnosisCategory,
          })),
        },
      });
    } catch (error) {
      setIsSaving(false);
      toast.error(getApiErrorMessage(error, "Failed to save consult."));
      return;
    }

    // Orders are placed one at a time against the saved consult; the server
    // reserves stock for each. One it cannot cover does not affect the others
    // or the consult.
    const unfilled: string[] = [];
    for (const order of data.orders) {
      try {
        await createOrder.mutateAsync({
          body: {
            consultId: consult.id,
            medicationActiveIngredientId: order.activeIngredientId,
            quantity: order.quantity,
            dosageInstructions: order.dosageInstructions,
          },
        });
      } catch (error) {
        unfilled.push(
          `${order.activeIngredientName} (${getApiErrorMessage(error, "could not be placed")})`,
        );
      }
    }

    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsConsultsListByVisitGetEndpointQueryKey({
        path: { visitId },
      }),
    });
    if (data.orders.length > 0) {
      // Placing an order reserves stock.
      queryClient.invalidateQueries({
        queryKey:
          saBaiAPIEndpointsMedicationStockByIngredientGetEndpointQueryKey(),
      });
      queryClient.invalidateQueries({
        queryKey:
          saBaiAPIEndpointsMedicationStockWithBrandGetEndpointQueryKey(),
      });
    }

    if (unfilled.length > 0) {
      toast.error(
        `Consult saved, but ${unfilled.length} order(s) could not be filled: ${unfilled.join(", ")}. Please re-order from the pharmacy.`,
        { duration: 10000 },
      );
    } else {
      toast.success("Consult has been saved successfully!");
    }

    methods.reset(BLANK_CONSULT);
    setIsSaving(false);
  };

  return (
    <FormProvider {...methods}>
      {orderModalIsOpen && (
        <OrderModal
          setOrderModalIsOpen={setOrderModalIsOpen}
          onSave={appendOrder}
          stagedOrders={orderFields}
        />
      )}
      <form onSubmit={handleSubmit(onSubmit, onInvalid)} className="space-y-8">
        <h2 className="text-lg font-semibold text-slate-900">
          Doctor&apos;s Consult Form
        </h2>

        <section className="space-y-4">
          <RHFTextArea
            name="pastMedicalHistory"
            label="Past Medical History"
            rows={3}
            placeholder="Type the patient's past medical history here..."
          />
          <RHFTextArea
            name="consultation"
            label="Consultation"
            rows={4}
            placeholder="Type your consultation here..."
          />
        </section>

        <section className="space-y-4">
          <div className="flex items-center gap-1">
            <h3 className="text-sm font-semibold text-slate-700">Diagnoses</h3>
            <IsRequiredStar isRequired />
          </div>

          <Button
            type="button"
            title="Add Diagnosis"
            colour="emerald"
            variant="filled"
            onClick={() => append({ details: "", category: "" })}
            className="w-full"
          />

          {fields.map((field, index) => (
            <div
              key={field.id}
              className="space-y-4 rounded-lg border border-slate-200 p-4"
            >
              <div className="flex items-center justify-between">
                <p className="text-sm font-semibold text-slate-600">
                  Diagnosis {index + 1}
                </p>
                {fields.length > 1 && (
                  <button
                    type="button"
                    onClick={() => remove(index)}
                    title="Remove diagnosis"
                    className="inline-flex items-center gap-1 rounded p-1 text-sm text-red-600 transition hover:bg-red-50"
                  >
                    <HiOutlineTrash className="h-4 w-4" />
                    Remove
                  </button>
                )}
              </div>
              <RHFDropdown
                name={`diagnoses.${index}.category`}
                label="Category"
                dropdownOptions={DIAGNOSIS_CATEGORY_OPTIONS}
                isRequired
                placeholder="Choose a category"
              />
              <RHFTextArea
                name={`diagnoses.${index}.details`}
                label="Details"
                isRequired
                rows={2}
                placeholder="Describe the diagnosis..."
              />
            </div>
          ))}
        </section>

        <section className="space-y-4">
          <div className="flex items-center gap-1">
            <h3 className="text-sm font-semibold text-slate-700">Orders</h3>
          </div>

          <Button
            type="button"
            title="Add Order"
            colour="emerald"
            variant="filled"
            onClick={() => setOrderModalIsOpen(true)}
            className="w-full"
          />

          {orderFields.map((field, index) => (
            <div
              key={field.id}
              className="flex items-start justify-between gap-4 rounded-lg border border-slate-200 p-4"
            >
              <div className="space-y-1">
                <p className="text-sm font-semibold text-slate-700">
                  {field.activeIngredientName}
                </p>
                <p className="text-sm text-slate-600">
                  Quantity: {field.quantity} {field.unitOfMeasurement}
                </p>
                <p className="text-sm text-slate-600">
                  {field.dosageInstructions}
                </p>
              </div>
              <button
                type="button"
                onClick={() => removeOrder(index)}
                title="Remove order"
                className="inline-flex items-center gap-1 rounded p-1 text-sm text-red-600 transition hover:bg-red-50"
              >
                <HiOutlineTrash className="h-4 w-4" />
                Remove
              </button>
            </div>
          ))}
        </section>

        <section className="space-y-4">
          <RHFTextArea
            name="treatmentPlan"
            label="Plan"
            rows={3}
            placeholder="Type your plan here..."
          />
          <RHFTextArea
            name="remarks"
            label="Remarks"
            rows={2}
            placeholder="Type your remarks here..."
          />
        </section>

        <div className="flex flex-col gap-4 border-t border-slate-100 pt-4 sm:flex-row sm:items-center sm:justify-end">
          <div className="w-full sm:w-44">
            <Button
              type="submit"
              title="Save Consult"
              colour="emerald"
              variant="filled"
              loading={isSaving}
            />
          </div>
        </div>
      </form>
    </FormProvider>
  );
}
