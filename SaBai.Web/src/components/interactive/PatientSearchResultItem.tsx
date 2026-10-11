import Link from "next/link";
import { calculateAge } from "@/lib/utils/patient";
import { PatientPhoto } from "@/components/PatientPhoto";
import type { SaBaiAPIEndpointsPatientsListGetItem } from "@sabai/sdk/types";
import { paths } from "@/utils/paths";

interface PatientSearchResultItemProps {
  patient: SaBaiAPIEndpointsPatientsListGetItem;
  onSelect: () => void;
  linkPath?: string;
}

export default function PatientSearchResultItem({
  patient,
  onSelect,
  linkPath = paths.patientDetail(patient.id),
}: PatientSearchResultItemProps) {
  return (
    <Link
      href={linkPath}
      onClick={onSelect}
      className="flex items-center gap-3 text-sm text-neutral-700 px-2 py-2 rounded bg-white border border-neutral-100 hover:bg-neutral-75"
    >
      <PatientPhoto
        patientId={patient.id}
        hasPhoto={patient.hasPhoto}
        height={40}
        width={40}
        className="rounded-lg border border-slate-200"
      />
      <div className="flex min-w-0 flex-col">
        <span className="font-medium truncate">{patient.name}</span>
        <span className="text-xs text-neutral-500">
          ID: {patient.id} • {calculateAge(patient.dateOfBirth)} yrs •{" "}
          {patient.gender}
        </span>
      </div>
    </Link>
  );
}
