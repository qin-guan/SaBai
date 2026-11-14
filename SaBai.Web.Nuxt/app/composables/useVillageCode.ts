import type { MaybeRefOrGetter } from 'vue'
import { useQuery } from '@tanstack/vue-query'
import { fetchVillageCodes, type VillageCode } from '~/offline/mutations/villageCodes'

export type { VillageCode } from '~/offline/mutations/villageCodes'

const STORAGE_KEY = 'villageCodeState'

function readStoredVillageId(): string | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (!stored) return null
    const parsed = JSON.parse(stored) as { selectedVillageCodeId?: string | number | null }
    // Ids were numbers before UUID keys; a stored number no longer matches anything.
    return typeof parsed.selectedVillageCodeId === 'string' ? parsed.selectedVillageCodeId : null
  }
  catch {
    return null
  }
}

/** Village codes, newest first. Served from the cache (also offline) and refreshed from the API. */
export function useVillageCodesList(includeHidden: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: computed(() => queryKeys.villageCodesList(toValue(includeHidden))),
    queryFn: ({ signal }) => fetchVillageCodes(toValue(includeHidden), signal),
  })
}

/**
 * The village new patients and visits are recorded against. Persisted in
 * localStorage so it survives reloads and offline restarts.
 */
export function useVillageCode() {
  const selectedVillageCodeId = useState<string | null>('selected-village', () =>
    import.meta.client ? readStoredVillageId() : null,
  )

  const query = useVillageCodesList(false)
  const villageCodes = computed<VillageCode[] | undefined>(() => query.data.value?.villageCodes)

  function setSelectedVillageCodeId(id: string | null) {
    selectedVillageCodeId.value = id
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ selectedVillageCodeId: id }))
    }
    catch {
      // Storage unavailable (private mode); selection still works for this session.
    }
  }

  // A hidden/deleted village drops out of the (visible-only) list, so clear it.
  watch(
    [villageCodes, selectedVillageCodeId],
    ([codes, selected]) => {
      if (codes && selected !== null && !codes.some(v => v.id === selected)) {
        setSelectedVillageCodeId(null)
      }
    },
    { immediate: true },
  )

  const selectedVillage = computed(() =>
    villageCodes.value?.find(v => v.id === selectedVillageCodeId.value),
  )

  return {
    selectedVillageCodeId: readonly(selectedVillageCodeId),
    selectedVillage,
    setSelectedVillageCodeId,
    villageCodes,
    // Loading only while there is nothing to show; cached data renders straight away.
    isLoading: computed(() => query.isLoading.value && !query.data.value),
    isError: computed(() => query.isError.value && !query.data.value),
  }
}
