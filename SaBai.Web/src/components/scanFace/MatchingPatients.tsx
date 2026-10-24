import { useState } from "react";
import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/router";
import toast from "react-hot-toast";
import { MdPersonSearch } from "react-icons/md";
import {
  saBaiAPIEndpointsPatientsSearchByFacePostEndpoint,
  saBaiAPIEndpointsVisitsCreatePostEndpoint,
  saBaiAPIEndpointsVisitsHasRecentGetEndpoint,
} from "@sabai/sdk/clients";
import { Mode } from "@/types/scan";
import { Button } from "@/components/interactive/Button/Button";
import Modal from "@/components/interactive/Modal";
import { PatientPhoto } from "@/components/PatientPhoto";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { dataUrlToFile } from "@/lib/api/patientPhoto";
import { useVillageCode } from "@/lib/context/VillageCodeContext";
import { paths } from "@/utils/paths";

/**
 * Searches the Qdrant face index for patients matching the captured photo and
 * lets the user open a visit for the best match. Asks for confirmation when
 * the patient already had a visit opened within the last minute.
 */
export default function MatchingPatients({
  imgDetails,
  setMode,
}: {
  imgDetails: string;
  setMode: React.Dispatch<React.SetStateAction<Mode>>;
}) {
  const router = useRouter();
  const { selectedVillageCodeId, isLoading: villageLoading } = useVillageCode();
  const [duplicateVisitConfirmation, setDuplicateVisitConfirmation] = useState<{
    patientId: number;
    existingVisitDate: Date | null;
  } | null>(null);
  const [isCheckingDuplicate, setIsCheckingDuplicate] = useState(false);

  const search = useQuery({
    queryKey: ["patient-face-search", imgDetails],
    retry: false,
    queryFn: async () => {
      const image = await dataUrlToFile(imgDetails, "face-search");
      const result = await saBaiAPIEndpointsPatientsSearchByFacePostEndpoint({
        ...apiConfig,
        body: { image },
        query: { maxMatches: 5 },
        throwOnError: true,
      }).unwrap();
      return result.matches;
    },
  });

  const createVisit = useMutation({
    mutationFn: async (patientId: number) => {
      if (!selectedVillageCodeId) {
        throw new Error("Please select a village code first.");
      }

      return saBaiAPIEndpointsVisitsCreatePostEndpoint({
        ...apiConfig,
        body: { patientId, villageCodeId: selectedVillageCodeId },
        throwOnError: true,
      }).unwrap();
    },
  });

  const submitCreateVisit = async (patientId: number) => {
    try {
      await createVisit.mutateAsync(patientId);
      setDuplicateVisitConfirmation(null);
      router.push(paths.vitalsDetail(patientId));
    } catch (error) {
      toast.error(
        getApiErrorMessage(error, "Failed to start a visit for this patient."),
      );
    }
  };

  const handleSelect = async (patientId: number) => {
    if (!selectedVillageCodeId) {
      toast.error("Please select a village code before starting a visit.");
      return;
    }

    setIsCheckingDuplicate(true);
    let recent;
    try {
      recent = await saBaiAPIEndpointsVisitsHasRecentGetEndpoint({
        ...apiConfig,
        query: { patientId },
        throwOnError: true,
      }).unwrap();
    } catch {
      // Don't block visit creation if the duplicate check itself fails.
      recent = null;
    } finally {
      setIsCheckingDuplicate(false);
    }

    if (recent?.hasRecentVisit) {
      setDuplicateVisitConfirmation({
        patientId,
        existingVisitDate: recent.existingVisitDate
          ? new Date(recent.existingVisitDate)
          : null,
      });
      return;
    }

    await submitCreateVisit(patientId);
  };

  const matches = search.data ?? [];

  return (
    <div className="mt-6">
      <div className="flex items-center justify-between">
        <h2 className="text-lg font-semibold text-slate-800">Face matches</h2>
        <Button
          onClick={() => setMode(Mode.REGISTERING)}
          colour="indigo"
          title="Register New Patient"
        />
      </div>

      {search.isLoading && (
        <p className="mt-4 text-sm text-slate-500">Searching for matches…</p>
      )}

      {search.isError && (
        <p className="mt-4 text-sm text-red-500">
          {getApiErrorMessage(
            search.error,
            "Face search failed. Try again or register the patient manually.",
          )}
        </p>
      )}

      {!search.isLoading && !search.isError && matches.length === 0 && (
        <div className="flex flex-col items-center gap-3 py-8 text-center">
          <MdPersonSearch className="text-gray-300" size={64} />
          <p className="text-sm text-gray-500">No matching patients found.</p>
        </div>
      )}

      {matches.length > 0 && (
        <ul className="mt-4 divide-y divide-slate-100 overflow-hidden rounded-lg border border-slate-200">
          {matches.map((match) => (
            <li key={match.id} className="flex items-center gap-3 p-3">
              <PatientPhoto
                patientId={match.id}
                hasPhoto={match.hasPhoto}
                height={48}
                width={48}
                className="h-12 w-12 rounded-lg border border-slate-200 object-cover"
              />
              <div className="min-w-0 flex-1">
                <p className="truncate text-sm font-medium text-slate-900">
                  {match.name}
                </p>
                <p className="text-xs text-slate-500">
                  {match.displayId}
                  {match.villageCode ? ` • ${match.villageCode}` : ""} •{" "}
                  {Math.round(match.similarity * 100)}% match
                </p>
              </div>
              <Button
                onClick={() => handleSelect(match.id)}
                colour="emerald"
                title={
                  isCheckingDuplicate || createVisit.isPending
                    ? "Starting…"
                    : "Select"
                }
                disabled={
                  isCheckingDuplicate || createVisit.isPending || villageLoading
                }
              />
            </li>
          ))}
        </ul>
      )}

      {duplicateVisitConfirmation !== null && (
        <Modal
          title="Duplicate Visit"
          onClose={() => setDuplicateVisitConfirmation(null)}
        >
          <p className="mb-6 text-slate-700">
            A visit already exists for this patient
            {duplicateVisitConfirmation.existingVisitDate && (
              <>
                {" "}
                at{" "}
                {duplicateVisitConfirmation.existingVisitDate.toLocaleTimeString()}
              </>
            )}
            . Are you sure you want to create another?
          </p>
          <div className="flex justify-end gap-3">
            <Button
              colour="red"
              title="Cancel"
              onClick={() => setDuplicateVisitConfirmation(null)}
            />
            <Button
              colour="emerald"
              title={createVisit.isPending ? "Creating..." : "Confirm"}
              disabled={createVisit.isPending}
              onClick={() =>
                submitCreateVisit(duplicateVisitConfirmation.patientId)
              }
            />
          </div>
        </Modal>
      )}
    </div>
  );
}
