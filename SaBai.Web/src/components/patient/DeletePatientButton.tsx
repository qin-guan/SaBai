import { useState } from "react";
import { useRouter } from "next/router";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import {
  saBaiAPIEndpointsAuthWhoAmIGetEndpointQueryOptions,
  saBaiAPIEndpointsPatientsDeleteDeleteEndpointMutationOptions,
} from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsPatientsGetResponse } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { paths } from "@/utils/paths";
import Modal from "@/components/interactive/Modal";
import { Button } from "@/components/interactive/Button/Button";

const ADMIN_ROLE = "admin";

/**
 * Admin-only permanent deletion of a patient and everything recorded for
 * them. The patient's code must be typed to confirm, since it cannot be undone.
 */
export default function DeletePatientButton({
  patient,
  visitCount,
}: {
  patient: SaBaiAPIEndpointsPatientsGetResponse;
  visitCount: number;
}) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const [isOpen, setIsOpen] = useState(false);
  const [confirmation, setConfirmation] = useState("");

  const { data: whoAmI } = useQuery({
    ...saBaiAPIEndpointsAuthWhoAmIGetEndpointQueryOptions(apiConfig),
    staleTime: 5 * 60 * 1000,
  });
  const isAdmin = !!whoAmI?.user.roles.includes(ADMIN_ROLE);

  const matches =
    confirmation.trim().toUpperCase() === patient.displayId.toUpperCase();

  const deleteMutation = useMutation({
    ...saBaiAPIEndpointsPatientsDeleteDeleteEndpointMutationOptions(apiConfig),
    onSuccess: async () => {
      toast.success(`Deleted ${patient.name}.`);
      setIsOpen(false);
      // Leave the record first so its queries are not refetched (and 404) while still mounted.
      await router.push(paths.patient());
      queryClient.removeQueries({
        predicate: (query) => {
          const url = (query.queryKey[0] as { url?: string } | undefined)?.url;
          return url?.startsWith("/patients/{") ?? false;
        },
      });
      queryClient.invalidateQueries({
        predicate: (query) => {
          const url = (query.queryKey[0] as { url?: string } | undefined)?.url;
          return url?.startsWith("/patients") ?? false;
        },
      });
    },
    onError: (err) => {
      console.error(err);
      toast.error(getApiErrorMessage(err, "Could not delete the patient."));
    },
  });

  if (!isAdmin) {
    return null;
  }

  return (
    <>
      <Button
        title="Delete Patient"
        colour="red"
        onClick={() => {
          setConfirmation("");
          setIsOpen(true);
        }}
        className="shrink-0"
      />
      {isOpen && (
        <Modal
          title="Delete patient permanently?"
          onClose={() => setIsOpen(false)}
        >
          <form
            className="flex flex-col gap-3"
            onSubmit={(e) => {
              e.preventDefault();
              if (matches) {
                deleteMutation.mutate({ path: { id: patient.id } });
              }
            }}
          >
            <p className="text-slate-700">
              This removes <strong>{patient.name}</strong> and everything
              recorded for them: {visitCount}{" "}
              {visitCount === 1 ? "visit" : "visits"} with their vitals,
              vision, consults, dental records and medication orders, plus
              uploaded files, the profile photo and enrolled faces.
            </p>
            <p className="font-semibold text-red-600">This cannot be undone.</p>
            <label className="flex flex-col gap-1 text-sm font-medium text-slate-700">
              Type {patient.displayId} to confirm
              <input
                value={confirmation}
                onChange={(e) => setConfirmation(e.target.value)}
                autoComplete="off"
                spellCheck={false}
                className="rounded-md border border-slate-300 px-3 py-2 font-mono"
              />
            </label>
            <div className="flex gap-2">
              <Button
                title="Cancel"
                onClick={() => setIsOpen(false)}
                className="w-full"
              />
              <Button
                type="submit"
                title={deleteMutation.isPending ? "Deleting..." : "Delete Patient"}
                colour="red"
                disabled={!matches || deleteMutation.isPending}
                className="w-full"
              />
            </div>
          </form>
        </Modal>
      )}
    </>
  );
}
