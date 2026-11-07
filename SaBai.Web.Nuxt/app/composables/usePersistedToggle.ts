/**
 * An open/closed toggle remembered on this device (e.g. a collapsed section).
 * Storage can be unavailable (private mode, blocked site data); it then simply
 * starts from `initial` each time.
 */
export function usePersistedToggle(key: string, initial: boolean) {
  const storageKey = `sabai.panel.${key}`
  let stored: boolean | null = null
  try {
    const value = localStorage.getItem(storageKey)
    stored = value === null ? null : value === '1'
  }
  catch {}
  const open = ref(stored ?? initial)
  function toggle() {
    open.value = !open.value
    try {
      localStorage.setItem(storageKey, open.value ? '1' : '0')
    }
    catch {}
  }
  return { open, toggle }
}
