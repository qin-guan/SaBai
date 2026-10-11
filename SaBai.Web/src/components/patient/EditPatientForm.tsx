import { useState } from "react";
import { useForm, FormProvider } from "react-hook-form";
import toast from "react-hot-toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsPatientsGetEndpointQueryKey,
  saBaiAPIEndpointsPatientsListGetEndpointQueryKey,
  saBaiAPIEndpointsPatientsUpdatePatchEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsPatientsGetResponse } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage, getApiErrorStatus } from "@/lib/api/errors";
import { patientPhotoQueryKey } from "@/lib/api/patientPhoto";
import { setPatientPhotoAndFace } from "@/lib/api/patientEnrollment";
import { RHFInput } from "@/components/interactive/RHF/RHFInput";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import { Button } from "@/components/interactive/Button/Button";
import { WebcamInput } from "@/components/interactive/inputs/WebcamInput";
import { toDateInputValue } from "@/lib/utils/patient";

type Patient = SaBaiAPIEndpointsPatientsGetResponse;

type EditPatientFormValues = {
  name: string;
  identificationNumber: string;
  contactNo: string;
  gender: "male" | "female";
  drugAllergy: string;
  dateOfBirth: string;
  hasPoorCard: boolean;
  hasBS2Card: boolean;
  hasSabaiCard: boolean;
};

/**
 * Reusable form for editing an existing patient's details. Prefills
 * from the given patient and persists via the SDK patients update endpoint.
 * Optionally captures a new photo, which is stored in RustFS and enrolled as a
 * face for the patient.
 */
export default function EditPatientForm({
  patient,
  onSuccess,
}: {
  patient: Patient;
  onSuccess?: () => void;
}) {
  const [newPhoto, setNewPhoto] = useState<string | null>(null);

  const form = useForm<EditPatientFormValues>({
    defaultValues: {
      name: patient.name,
      identificationNumber: patient.identificationNumber ?? "",
      contactNo: patient.contactNo ?? "",
      gender: patient.gender,
      drugAllergy: patient.drugAllergy,
      // <input type="date"> only accepts a yyyy-MM-dd value; patient.dateOfBirth
      // arrives as an ISO string, so convert it or the field renders blank.
      dateOfBirth: toDateInputValue(patient.dateOfBirth),
      hasPoorCard: patient.hasPoorCard,
      hasBS2Card: patient.hasBS2Card,
      hasSabaiCard: patient.hasSabaiCard,
    },
  });

  const queryClient = useQueryClient();
  const updateMutation = useMutation(
    saBaiAPIEndpointsPatientsUpdatePatchEndpointMutationOptions(apiConfig),
  );

  const handleSubmit = async (data: EditPatientFormValues) => {
    try {
      await updateMutation.mutateAsync({
        path: { id: patient.id },
        body: {
          name: data.name,
          identificationNumber: data.identificationNumber,
          contactNo: data.contactNo,
          gender: data.gender,
          dateOfBirth: new Date(data.dateOfBirth).toISOString(),
          drugAllergy: data.drugAllergy,
          hasPoorCard: data.hasPoorCard,
          hasBS2Card: data.hasBS2Card,
          hasSabaiCard: data.hasSabaiCard,
        },
      });
    } catch (error) {
      console.error("Error updating patient:", error);
      // Surface a known conflict (duplicate ID) directly; fall back to a
      // generic message otherwise.
      toast.error(
        getApiErrorStatus(error) === 409
          ? getApiErrorMessage(error, "Failed to update patient. Please try again.")
          : "Failed to update patient. Please try again.",
      );
      return;
    }

    if (newPhoto) {
      try {
        await setPatientPhotoAndFace(patient.id, newPhoto);
        queryClient.invalidateQueries({
          queryKey: patientPhotoQueryKey(patient.id),
        });
        setNewPhoto(null);
      } catch (error) {
        console.error("Error saving patient photo/face:", error);
        toast.error(
          getApiErrorMessage(
            error,
            "Patient updated, but the photo or face could not be saved.",
          ),
        );
      }
    }

    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsPatientsGetEndpointQueryKey({
        path: { id: patient.id },
      }),
    });
    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsPatientsListGetEndpointQueryKey({}),
    });
    toast.success("Patient details updated successfully!");
    onSuccess?.();
  };

  return (
    <FormProvider {...form}>
      <form
        onSubmit={(e) => {
          e.preventDefault();
          form.handleSubmit(handleSubmit)(e);
        }}
        className="space-y-4"
      >
        <div className="grid grid-cols-2 gap-x-6 gap-y-6">
          <RHFInput name="name" label="Name" type="text" isRequired />
          <RHFInput
            name="identificationNumber"
            label="Identification Number"
            type="text"
            isRequired
          />
          <RHFInput
            name="contactNo"
            label="Contact Number"
            type="text"
            isRequired
          />
          <RHFDropdown
            name="gender"
            label="Gender"
            dropdownOptions={[
              { label: "Male", value: "male" },
              { label: "Female", value: "female" },
            ]}
            isRequired
          />
          <RHFInput
            name="drugAllergy"
            label="Drug Allergy"
            type="text"
            isRequired
          />
          <RHFInput
            name="dateOfBirth"
            label="Date of Birth"
            type="date"
            isRequired
          />
          <RHFInput
            name="hasPoorCard"
            label="Has POOR Card?"
            type="checkbox"
            className="flex flex-row-reverse"
          />
          <RHFInput
            name="hasBS2Card"
            label="Has BS2 Card?"
            type="checkbox"
            className="flex flex-row-reverse"
          />
          <RHFInput
            name="hasSabaiCard"
            label="Has Sabai Card?"
            type="checkbox"
            className="flex flex-row-reverse"
          />
        </div>

        <div className="border-t border-slate-100 pt-6">
          <h3 className="mb-1 text-sm font-semibold text-slate-700">
            Profile photo &amp; face
          </h3>
          <p className="mb-4 text-xs text-slate-500">
            {patient.hasPhoto
              ? "Capture a new photo to replace the current one and re-enroll the patient's face."
              : "Capture a photo to set the patient's profile photo and enroll their face."}
          </p>
          <WebcamInput
            imageDetails={newPhoto}
            setImageDetails={setNewPhoto}
            autoOpenCamera={false}
            size={240}
          />
        </div>

        <div className="flex gap-3 mt-6 justify-center">
          <Button
            type="submit"
            disabled={updateMutation.isPending}
            colour="emerald"
            title={updateMutation.isPending ? "Saving..." : "Save Changes"}
          />
        </div>
      </form>
    </FormProvider>
  );
}
