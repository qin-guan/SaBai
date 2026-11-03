import { useRouter } from "next/router";
import { FormProvider, useForm, useWatch } from "react-hook-form";
import { ReactNode, useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  saBaiAPIEndpointsPatientsGetEndpointQueryOptions,
  saBaiAPIEndpointsPatientsVisitsGetEndpointQueryOptions,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import withDefaultLayout from "@/components/layouts/withDefaultLayout";
import PatientTopMenuLayout from "@/components/layouts/PatientTopMenuLayout";
import { paths } from "@/utils/paths";
import Breadcrumbs from "@/components/Breadcrumbs";
import LoadingSpinner from "@/components/LoadingSpinner";
import { RHFDropdown } from "@/components/interactive/RHF/RHFDropdown";
import { DentalForm } from "@/components/dental/DentalForm";
import { formatVisitDate } from "@/lib/utils/visit";
import { formatPatientId } from "@/lib/utils/patient";

function DentalConsultationPage() {
  const router = useRouter();
  const { patientId } = router.query;
  const visitMethods = useForm<{ visitSelect: string }>();
  const { setValue } = visitMethods;

  const { data: patient, isLoading: patientLoading } = useQuery({
    ...saBaiAPIEndpointsPatientsGetEndpointQueryOptions(
      { path: { id: Number(patientId) } },
      apiConfig,
    ),
    enabled: !!patientId,
  });

  const { data: visitsData, isLoading: visitsLoading } = useQuery({
    ...saBaiAPIEndpointsPatientsVisitsGetEndpointQueryOptions(
      { path: { patientId: Number(patientId) } },
      apiConfig,
    ),
    enabled: !!patientId,
  });
  const visits = visitsData?.visits;

  const selectedVisitValue = useWatch({
    control: visitMethods.control,
    name: "visitSelect",
  });

  // Default to the most recent visit, which is where the dental station records.
  useEffect(() => {
    if (visits && visits.length > 0 && !visitMethods.getValues("visitSelect")) {
      const latest = [...visits].sort(
        (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime(),
      )[0];
      setValue("visitSelect", latest.id.toString());
    }
  }, [visits, setValue, visitMethods]);

  if (!router.isReady || patientLoading || visitsLoading) {
    return (
      <LoadingSpinner
        message="Loading patient and visits..."
        className="p-12"
      />
    );
  }

  if (!patient) {
    return (
      <div className="p-8 text-center font-semibold text-red-500">
        Patient not found
      </div>
    );
  }

  const selectedVisit = selectedVisitValue
    ? visits?.find((v) => v.id.toString() === selectedVisitValue)
    : null;

  return (
    <div className="min-h-screen flex-1 bg-slate-50 p-4 sm:p-8">
      <div className="mx-auto w-full max-w-5xl">
        <Breadcrumbs
          items={[
            { label: "Home", href: "/" },
            { label: "Dental", href: paths.dental() },
            { label: patient.name },
          ]}
        />

        <div className="mb-6">
          <h1 className="text-3xl font-bold text-indigo-950">
            Dental Consultation
          </h1>
          <p className="mt-2 text-slate-600">
            {patient.name} · Patient ID: {formatPatientId(patient.id)}
          </p>
        </div>

        <FormProvider {...visitMethods}>
          <div className="mb-6 rounded-xl border border-slate-200 bg-white p-5 shadow-sm">
            {visits && visits.length > 0 ? (
              <div className="grid grid-cols-1 gap-4 md:grid-cols-2 md:items-center">
                <div className="w-full max-w-md">
                  <RHFDropdown
                    name="visitSelect"
                    label="Choose visit"
                    dropdownOptions={visits.map((visit) => ({
                      label: formatVisitDate(visit.date),
                      value: visit.id.toString(),
                    }))}
                  />
                </div>
                <div className="text-sm text-slate-600">
                  Recording dental consultation for visit on{" "}
                  <span className="font-semibold text-slate-900">
                    {selectedVisit ? formatVisitDate(selectedVisit.date) : "-"}
                  </span>
                </div>
              </div>
            ) : (
              <div className="rounded-lg border border-amber-200 bg-amber-50 p-4 text-amber-700">
                No visits found for this patient. Please create a visit first to
                record a dental consultation.
              </div>
            )}
          </div>
        </FormProvider>

        {selectedVisit && (
          <DentalForm key={selectedVisit.id} visitId={selectedVisit.id} />
        )}
      </div>
    </div>
  );
}

DentalConsultationPage.getLayout = (page: ReactNode) =>
  withDefaultLayout(
    <PatientTopMenuLayout getPatientLink={paths.dentalDetail}>
      {page}
    </PatientTopMenuLayout>,
  );

export default DentalConsultationPage;
