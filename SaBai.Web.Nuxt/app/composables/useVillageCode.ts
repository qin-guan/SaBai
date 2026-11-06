import { useQuery } from "@tanstack/vue-query";
import { saBaiAPIEndpointsVillageCodesListGetEndpoint } from "@sabai/sdk/clients";
import type { SaBaiAPIEndpointsVillageCodesListGetItem } from "@sabai/sdk/types";

export type VillageCode = SaBaiAPIEndpointsVillageCodesListGetItem;

const STORAGE_KEY = "villageCodeState";

function readStoredVillageId(): number | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (!stored) return null;
    const parsed = JSON.parse(stored) as { selectedVillageCodeId?: number | null };
    return parsed.selectedVillageCodeId ?? null;
  } catch {
    return null;
  }
}

export function useVillageCodesList(includeHidden: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: computed(() => queryKeys.villageCodesList(toValue(includeHidden))),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsVillageCodesListGetEndpoint({
        ...apiConfig,
        query: { includeHidden: toValue(includeHidden) },
        signal,
      }).unwrap(),
  });
}

/**
 * The village new patients and visits are recorded against. Persisted in
 * localStorage (same key and shape as SaBai.Web) so it survives reloads.
 */
export function useVillageCode() {
  const selectedVillageCodeId = useState<number | null>("selected-village", () =>
    import.meta.client ? readStoredVillageId() : null,
  );

  const query = useVillageCodesList(false);
  const villageCodes = computed(() => query.data.value?.villageCodes);

  function setSelectedVillageCodeId(id: number | null) {
    selectedVillageCodeId.value = id;
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ selectedVillageCodeId: id }));
    } catch {
      // Storage unavailable (private mode); selection still works for this session.
    }
  }

  // A hidden/deleted village drops out of the (visible-only) list, so clear it.
  watch(
    [villageCodes, selectedVillageCodeId],
    ([codes, selected]) => {
      if (codes && selected !== null && !codes.some((v) => v.id === selected)) {
        setSelectedVillageCodeId(null);
      }
    },
    { immediate: true },
  );

  const selectedVillage = computed(() =>
    villageCodes.value?.find((v) => v.id === selectedVillageCodeId.value),
  );

  return {
    selectedVillageCodeId: readonly(selectedVillageCodeId),
    selectedVillage,
    setSelectedVillageCodeId,
    villageCodes,
    isLoading: query.isLoading,
    isError: query.isError,
  };
}
