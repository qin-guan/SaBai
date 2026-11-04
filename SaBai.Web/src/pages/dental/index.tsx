import { useRouter } from "next/router";
import { paths } from "@/utils/paths";
import withDefaultLayout from "@/components/layouts/withDefaultLayout";
import PatientTopMenuLayout from "@/components/layouts/PatientTopMenuLayout";
import Breadcrumbs from "@/components/Breadcrumbs";
import { useQuery } from "@tanstack/react-query";
import { saBaiAPIEndpointsPatientsWithLatestVisitGetEndpointQueryOptions } from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsPatientsWithLatestVisitGetItem } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";
import { PatientPhoto } from "@/components/PatientPhoto";
import { FaTooth } from "react-icons/fa";
import LoadingSpinner from "@/components/LoadingSpinner";
import TableHeader from "@/components/TableHeader";
import TableRow from "@/components/TableRow";
import TableCell from "@/components/TableCell";
import { ReactNode } from "react";
import { PatientCode } from "@/components/PatientCode";

function DentalButton({ onClick }: { onClick: () => void }) {
  return (
    <button
      type="button"
      onClick={onClick}
      title="Dental Consultation"
      className="inline-flex cursor-pointer items-center gap-2 whitespace-nowrap rounded-lg bg-emerald-600 px-3 py-2 text-sm text-white transition hover:bg-emerald-700"
    >
      <FaTooth className="h-4 w-4" />
      Dental Consultation
    </button>
  );
}

/**
 * Stacked patient card used on small screens (e.g mobile)
 */
function PatientCard({
  patient,
  onOpenDental,
}: {
  patient: SaBaiAPIEndpointsPatientsWithLatestVisitGetItem;
  onOpenDental: (patientId: number) => void;
}) {
  return (
    <div className="flex gap-10 p-4">
      <PatientPhoto
        patientId={patient.id}
        hasPhoto={patient.hasPhoto}
        className="h-14 w-14 rounded-lg border border-slate-200 object-cover"
      />
      <div className="flex min-w-0 flex-1 flex-col items-start gap-2">
        <PatientCode
          villageCode={patient.villageCode}
          villageColorHex={patient.villageColorHex}
          id={patient.id}
          className="text-xs font-medium text-slate-500"
        />
        <div className="break-words text-sm font-medium text-slate-900">
          {patient.name}
        </div>
        <DentalButton onClick={() => onOpenDental(patient.id)} />
      </div>
    </div>
  );
}

/**
 * Full patient table shown on tablet and desktop screens.
 */
function PatientTable({
  patients,
  onOpenDental,
}: {
  patients: SaBaiAPIEndpointsPatientsWithLatestVisitGetItem[];
  onOpenDental: (patientId: number) => void;
}) {
  return (
    <div className="hidden overflow-x-auto md:block">
      <table className="min-w-full divide-y divide-slate-200">
        <TableHeader headers={["ID", "Photo", "Name", "Actions"]} />
        <tbody className="bg-white divide-y divide-slate-200">
          {patients.map((patient) => (
            <TableRow key={patient.id}>
              <TableCell>
                <PatientCode
                  villageCode={patient.villageCode}
                  villageColorHex={patient.villageColorHex}
                  id={patient.id}
                  className="text-sm font-medium text-slate-900"
                />
              </TableCell>
              <TableCell>
                <PatientPhoto
                  patientId={patient.id}
                  hasPhoto={patient.hasPhoto}
                  className="h-12 w-12 rounded-lg border border-slate-200 object-cover"
                />
              </TableCell>
              <TableCell>
                <span className="text-sm font-medium text-slate-900">
                  {patient.name}
                </span>
              </TableCell>
              <TableCell>
                <DentalButton onClick={() => onOpenDental(patient.id)} />
              </TableCell>
            </TableRow>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Wrapper({ children }: { children: ReactNode }) {
  return (
    <div className="min-h-screen flex-1 p-4 sm:p-6">
      <Breadcrumbs
        items={[{ label: "Home", href: "/" }, { label: "Dental" }]}
      />
      <div className="mb-8">
        <h1 className="text-3xl font-bold text-slate-900">Dental</h1>
        <p className="mt-2 text-slate-600">
          Record dental consultations for patients at the dental station.
        </p>
      </div>
      <div className="bg-white rounded-xl shadow-sm border border-slate-200 overflow-hidden">
        {children}
      </div>
    </div>
  );
}

function DentalPage() {
  const router = useRouter();
  const { data, isLoading, isError } = useQuery(
    saBaiAPIEndpointsPatientsWithLatestVisitGetEndpointQueryOptions(apiConfig),
  );
  const patients = data?.patients;

  const handleOpenDental = (patientId: number) => {
    router.push(paths.dentalDetail(patientId));
  };

  function renderContent() {
    if (isError) {
      return <h1 className="text-red-500">An error has occurred!</h1>;
    }

    if (isLoading) {
      return <LoadingSpinner message="Loading patients..." className="p-12" />;
    }

    if (!patients || patients.length === 0) {
      return (
        <div className="p-12 text-center text-slate-500">
          No patients found. Register patients to record dental consultations.
        </div>
      );
    }

    return (
      <>
        {/* Mobile screens: stacked cards */}
        <div className="divide-y divide-slate-200 md:hidden">
          {patients.map((patient) => (
            <PatientCard
              key={patient.id}
              patient={patient}
              onOpenDental={handleOpenDental}
            />
          ))}
        </div>

        {/* Tablet and desktop screens: full table */}
        <PatientTable patients={patients} onOpenDental={handleOpenDental} />
      </>
    );
  }

  return <Wrapper>{renderContent()}</Wrapper>;
}

DentalPage.getLayout = (page: ReactNode) =>
  withDefaultLayout(
    <PatientTopMenuLayout getPatientLink={paths.dentalDetail}>
      {page}
    </PatientTopMenuLayout>,
  );

export default DentalPage;
