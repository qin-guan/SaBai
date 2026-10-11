import { useForm, FormProvider } from "react-hook-form";
import toast from "react-hot-toast";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsPatientsCreatePostEndpointMutationOptions,
  saBaiAPIEndpointsPatientsListGetEndpointQueryKey,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { setPatientPhotoAndFace } from "@/lib/api/patientEnrollment";
import { RHFInput } from "@/components/interactive/RHF/RHFInput";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import { Mode } from "@/types/scan";
import { useVillageCode } from "@/lib/context/VillageCodeContext";
import { Button } from "@/components/interactive/Button/Button";

export type PatientForm = {
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

export default function RegistrationPage({
  imgDetails,
  setImgDetails,
  setMode,
}: {
  imgDetails: string | null;
  setImgDetails: React.Dispatch<React.SetStateAction<string | null>>;
  setMode: React.Dispatch<React.SetStateAction<Mode>>;
}) {
  const form = useForm<PatientForm>();

  const queryClient = useQueryClient();
  const createMutation = useMutation(
    saBaiAPIEndpointsPatientsCreatePostEndpointMutationOptions(apiConfig),
  );

  const { selectedVillageCodeId } = useVillageCode();
  const handleSubmit = async (data: PatientForm) => {
    if (!imgDetails) {
      toast.error("Please capture a face image before submitting.");
      return;
    }

    if (!selectedVillageCodeId) {
      toast.error("Please select a village code before submitting.");
      return;
    }

    let patient;
    try {
      patient = await createMutation.mutateAsync({
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
          toGetReport: false,
          villageCodeId: selectedVillageCodeId,
        },
      });
    } catch (error) {
      console.error("Error creating patient:", error);
      toast.error(
        getApiErrorMessage(error, "Failed to create patient. Please try again."),
      );
      return;
    }

    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsPatientsListGetEndpointQueryKey({}),
    });

    // Store the captured photo and enroll the face. These are best-effort: the
    // patient already exists, so failures shouldn't discard the registration.
    try {
      await setPatientPhotoAndFace(patient.id, imgDetails);
    } catch (error) {
      console.error("Error saving patient photo/face:", error);
      toast.error(
        "Patient created, but the photo or face could not be saved. You can retry from the patient page.",
      );
    }

    toast.success("Patient created successfully!");
    setImgDetails(null);
    form.reset();
  };

  return (
    <>
      <FormProvider {...form}>
        <form
          onSubmit={(e) => {
            e.preventDefault();
            form.handleSubmit(handleSubmit)(e);
          }}
          className="space-y-4"
        >
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
          <div className="rounded-lg border border-gray-200 bg-gray-50 divide-y divide-gray-200">
            <RHFInput
              name="hasPoorCard"
              label="Has POOR Card?"
              type="checkbox"
            />
            <RHFInput name="hasBS2Card" label="Has BS2 Card?" type="checkbox" />
            <RHFInput
              name="hasSabaiCard"
              label="Has Sabai Card?"
              type="checkbox"
            />
          </div>

          {/* Submit Button */}
          <div className="flex gap-3 mt-6 justify-center">
            <Button
              type="submit"
              disabled={createMutation.isPending}
              colour="emerald"
              title={
                createMutation.isPending
                  ? "Creating new patient..."
                  : "Create New Patient"
              }
            />
          </div>
        </form>
      </FormProvider>
      <div className="flex gap-3 mt-6 justify-center">
        <Button
          onClick={() => setMode(Mode.MATCHING)}
          colour="indigo"
          title="Match Instead"
        />
      </div>
    </>
  );
}
