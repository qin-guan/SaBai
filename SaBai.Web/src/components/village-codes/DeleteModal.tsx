import { useMutation, useQueryClient } from "@tanstack/react-query";
import toast from "react-hot-toast";
import Modal from "@/components/interactive/Modal";
import {
  saBaiAPIEndpointsVillageCodesDeleteDeleteEndpointMutationOptions,
  saBaiAPIEndpointsVillageCodesListGetEndpointQueryKey,
} from "@sabai/sdk/hooks";
import { apiConfig } from "@/lib/api/config";
import { getApiErrorMessage } from "@/lib/api/errors";
import { FormFields } from "@/types/village-codes";
import { Button } from "@/components/interactive/Button/Button";

export default function DeleteModal({
  activeForm,
  onClose,
}: {
  onClose: () => void;
  activeForm: FormFields;
}) {
  const queryClient = useQueryClient();

  const invalidateVillageCodes = () => {
    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsVillageCodesListGetEndpointQueryKey({
        query: { includeHidden: false },
      }),
    });
    queryClient.invalidateQueries({
      queryKey: saBaiAPIEndpointsVillageCodesListGetEndpointQueryKey({
        query: { includeHidden: true },
      }),
    });
  };

  const deleteMutation = useMutation({
    ...saBaiAPIEndpointsVillageCodesDeleteDeleteEndpointMutationOptions(apiConfig),
    onSuccess: () => {
      invalidateVillageCodes();
      toast.success("Village code deleted!");
      onClose();
    },
    onError: (err) => {
      console.error(err);
      toast.error(
        getApiErrorMessage(
          err,
          "Unable to delete village code. Check that there are no visits with this village code before deleting.",
        ),
      );
    },
  });

  return (
    <Modal onClose={onClose}>
      <h2 className="text-xl font-bold tracking-tight text-slate-900">
        Confirm Deletion
      </h2>
      <p>Code: {activeForm?.code}</p>
      <p>Name: {activeForm?.name}</p>

      <div className="flex gap-2 justify-center">
        <Button
          onClick={onClose}
          colour="red"
          title="Cancel"
          className="w-full"
        />
        <Button
          onClick={() =>
            deleteMutation.mutate({ path: { id: activeForm!.id! } })
          }
          disabled={deleteMutation.isPending}
          colour="emerald"
          title={deleteMutation.isPending ? "Deleting..." : "Confirm"}
          className="w-full"
        />
      </div>
    </Modal>
  );
}
