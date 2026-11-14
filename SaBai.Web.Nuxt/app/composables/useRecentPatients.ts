/**
 * Recently opened patients (most recent first), shown when the search box is
 * empty. Device-local; cleared on sign-out.
 */
const KEY = 'sabai:recent-patients'
const MAX = 8

function read(): string[] {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(KEY) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter((n): n is string => typeof n === 'string') : []
  } catch {
    return []
  }
}

export function useRecentPatients() {
  const ids = useState<string[]>('recent-patients', () => (import.meta.client ? read() : []))

  function remember(id: string | number | null | undefined) {
    if (!id) return
    const strId = String(id)
    ids.value = [strId, ...ids.value.filter(x => x !== strId)].slice(0, MAX)
    try {
      window.localStorage.setItem(KEY, JSON.stringify(ids.value))
    } catch {}
  }

  return { recentIds: readonly(ids), remember }
}

/** Remembers the patient whose page is open. */
export function useRememberPatient(id: MaybeRefOrGetter<string | number | null>) {
  const { remember } = useRecentPatients()
  watch(() => toValue(id), value => remember(value), { immediate: true })
}
