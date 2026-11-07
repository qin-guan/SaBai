/**
 * Recently opened patients (most recent first), shown when the search box is
 * empty. Device-local; cleared on sign-out.
 */
const KEY = 'sabai:recent-patients'
const MAX = 8

function read(): number[] {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(KEY) ?? '[]')
    return Array.isArray(parsed) ? parsed.filter(n => typeof n === 'number') : []
  }
  catch {
    return []
  }
}

export function useRecentPatients() {
  const ids = useState<number[]>('recent-patients', () => (import.meta.client ? read() : []))

  function save(next: number[]) {
    ids.value = next
    try {
      window.localStorage.setItem(KEY, JSON.stringify(next))
    }
    catch {}
  }

  function remember(id: number | null | undefined) {
    if (!id)
      return
    save([id, ...ids.value.filter(x => x !== id)].slice(0, MAX))
  }

  /** Drops a patient from the list, e.g. after they are deleted. */
  function forget(id: number) {
    save(ids.value.filter(x => x !== id))
  }

  return { recentIds: readonly(ids), remember, forget }
}

/** Remembers the patient whose page is open. */
export function useRememberPatient(id: MaybeRefOrGetter<number | null>) {
  const { remember } = useRecentPatients()
  watch(() => toValue(id), value => remember(value), { immediate: true })
}
