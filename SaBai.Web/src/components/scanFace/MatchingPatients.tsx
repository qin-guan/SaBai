import { useMutation, useQuery } from "@tanstack/react-query";
import { useRouter } from "next/router";
import toast from "react-hot-toast";
import { MdPersonSearch } from "react-icons/md";
import {
  saBaiAPIEndpointsPatientsSearchByFacePostEndpoint,
  saBaiAPIEndpointsVisitsCreatePostEndpoint,
} from "@sabai/sdk/clients";
import { Mode } from "@/types/scan";
import { Button } from "@/components/interactive/Button/Button";
import { PatientPhoto } from "@/components/PatientPhoto";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { dataUrlToFile } from "@/lib/api/patientPhoto";
import { useVillageCode } from "@/lib/context/VillageCodeContext";
import { paths } from "@/utils/paths";

/**
 * Searches the Qdrant face index for patients matching the captured photo and
 * lets the user open a visit for the best match.
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

  const handleSelect = async (patientId: number) => {
    if (!selectedVillageCodeId) {
      toast.error("Please select a village code before starting a visit.");
      return;
    }

    try {
      await createVisit.mutateAsync(patientId);
      router.push(paths.vitalsDetail(patientId));
    } catch (error) {
      toast.error(
        getApiErrorMessage(error, "Failed to start a visit for this patient."),
      );
    }
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
                title={createVisit.isPending ? "Starting…" : "Select"}
                disabled={createVisit.isPending || villageLoading}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
