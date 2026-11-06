import { useQuery } from "@tanstack/vue-query";
import type { MaybeRefOrGetter } from "vue";
import { saBaiAPIEndpointsPatientsPhotoGetEndpoint } from "@sabai/sdk/clients";

/**
 * Loads a patient's profile photo as an object URL. Photos sit behind bearer
 * auth, so an `<img src>` cannot fetch them directly.
 */
export function usePatientPhoto(
  patientId: MaybeRefOrGetter<number>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const { data } = useQuery({
    queryKey: computed(() => queryKeys.patientPhoto(toValue(patientId))),
    enabled: computed(() => toValue(enabled) && toValue(patientId) > 0),
    staleTime: 5 * 60 * 1000,
    retry: false,
    queryFn: async () => {
      const result = (await saBaiAPIEndpointsPatientsPhotoGetEndpoint({
        ...apiConfig,
        path: { id: toValue(patientId) },
        responseType: "blob",
        throwOnError: false,
      })) as unknown as { status: number; data?: Blob | null };
      if (result.status !== 200) return null;
      return result.data ?? null;
    },
  });

  const url = ref<string | null>(null);
  watch(
    data,
    (blob, _old, onCleanup) => {
      if (!blob) {
        url.value = null;
        return;
      }
      const objectUrl = URL.createObjectURL(blob);
      url.value = objectUrl;
      onCleanup(() => URL.revokeObjectURL(objectUrl));
    },
    { immediate: true },
  );

  return url;
}
