import type { MaybeRefOrGetter } from 'vue'
import type { LocalVillageCode } from '~/db/types'

export type VillageCode = LocalVillageCode

const STORAGE_KEY = 'villageCodeState'

function readStoredVillageId(): string | null {
  try {
    const stored = window.localStorage.getItem(STORAGE_KEY)
    if (!stored) return null
    const parsed = JSON.parse(stored) as { selectedVillageCodeId?: string | number | null }
    return typeof parsed.selectedVillageCodeId === 'string' ? parsed.selectedVillageCodeId : null
  } catch {
    return null
  }
}

export function useVillageCodesList(includeHidden: MaybeRefOrGetter<boolean>) {
  const query = useLocalQuery<{ villageCodes: LocalVillageCode[] }>(
    ['village_codes'],
    async () => {
      const db = getPowerSyncInstance()
      const showAll = toValue(includeHidden)
      const rows = await db.getAll<{
        id: string
        version: number
        code: string
        name: string
        color_hex: string
        is_visible: number
        created_at: string
      }>(
        showAll
          ? 'SELECT id, version, code, name, color_hex, is_visible, created_at FROM village_codes ORDER BY code ASC'
          : 'SELECT id, version, code, name, color_hex, is_visible, created_at FROM village_codes WHERE is_visible = 1 ORDER BY code ASC',
      )
      return {
        villageCodes: rows.map(r => ({
          id: r.id,
          version: Number(r.version ?? 1),
          code: r.code,
          name: r.name,
          colorHex: r.color_hex ?? '#1f3a5f',
          isVisible: Boolean(r.is_visible),
          createdAt: r.created_at,
        })),
      }
    },
    { watchDeps: [() => toValue(includeHidden)] },
  )
  return query
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
  const villageCodes = computed(() => query.data.value?.villageCodes)

  function setSelectedVillageCodeId(id: string | null) {
    selectedVillageCodeId.value = id
    try {
      window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ selectedVillageCodeId: id }))
    } catch {
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
    isLoading: query.isLoading,
    isError: query.isError,
  }
}
