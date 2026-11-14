import { useQuery } from "@tanstack/vue-query";
import type { MaybeRefOrGetter } from "vue";

/**
 * Loads a patient's profile photo as an object URL. Photos sit behind cookie
 * auth, so an `<img src>` cannot fetch them directly; the Service Worker
 * caches `/api/patients/:id/photo` with StaleWhileRevalidate for offline viewing.
 */
export function usePatientPhoto(
  patientId: MaybeRefOrGetter<string | number>,
  enabled: MaybeRefOrGetter<boolean> = true,
) {
  const { data } = useQuery({
    queryKey: computed(() => queryKeys.patientPhoto(String(toValue(patientId)))),
    enabled: computed(() => Boolean(toValue(enabled)) && Boolean(toValue(patientId))),
    staleTime: 5 * 60 * 1000,
    retry: false,
    // A Blob is not JSON; the offline copy of the photo lives in the Service Worker cache.
    meta: { persist: false },
    queryFn: async () => {
      const id = String(toValue(patientId));
      const res = await fetch(`/api/patients/${id}/photo`, { credentials: "include" });
      if (!res.ok) return null;
      return await res.blob();
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
