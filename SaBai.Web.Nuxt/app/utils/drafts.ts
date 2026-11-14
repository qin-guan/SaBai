/**
 * Device-local storage for unsaved form input ("drafts").
 *
 * Keys are namespaced `sabai:draft:<form>:<id>`; entries expire after
 * DRAFT_TTL_MS. Drafts can hold patient data, so they are wiped on sign-out
 * (shared clinic devices) — see clearAllFormDrafts().
 */
export const DRAFT_PREFIX = 'sabai:draft:'
export const DRAFT_TTL_MS = 15 * 60 * 1000

export interface StoredDraft<T> {
  values: T
  savedAt: number
}

export function readDraft<T>(key: string): StoredDraft<T> | null {
  try {
    const raw = window.sessionStorage.getItem(DRAFT_PREFIX + key)
    if (!raw)
      return null
    const draft = JSON.parse(raw) as StoredDraft<T>
    if (!draft || typeof draft.savedAt !== 'number' || Date.now() - draft.savedAt > DRAFT_TTL_MS) {
      window.sessionStorage.removeItem(DRAFT_PREFIX + key)
      return null
    }
    return draft
  }
  catch {
    return null
  }
}

export function writeDraft<T>(key: string, values: T) {
  try {
    window.sessionStorage.setItem(DRAFT_PREFIX + key, JSON.stringify({ values, savedAt: Date.now() }))
  }
  catch {
    // Storage full / unavailable: drafts are best-effort.
  }
}

export function removeDraft(key: string) {
  try {
    window.sessionStorage.removeItem(DRAFT_PREFIX + key)
  }
  catch {}
}

export function clearAllFormDrafts() {
  try {
    for (const key of Object.keys(window.sessionStorage)) {
      if (key.startsWith(DRAFT_PREFIX))
        window.sessionStorage.removeItem(key)
    }
    // Also purge any legacy localStorage drafts from earlier versions.
    for (const key of Object.keys(window.localStorage)) {
      if (key.startsWith(DRAFT_PREFIX))
        window.localStorage.removeItem(key)
    }
  }
  catch {}
}

export function clearRecentPatients() {
  try {
    window.sessionStorage.removeItem('sabai:recent-patients')
    window.localStorage.removeItem('sabai:recent-patients')
  }
  catch {}
}
