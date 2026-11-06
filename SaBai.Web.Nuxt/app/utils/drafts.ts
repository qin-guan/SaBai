/**
 * Device-local storage for unsaved form input ("drafts").
 *
 * Keys are namespaced `sabai:draft:<form>:<id>`; entries expire after
 * DRAFT_TTL_MS. Drafts can hold patient data, so they are wiped on sign-out
 * (shared clinic devices) — see clearAllFormDrafts().
 */
export const DRAFT_PREFIX = 'sabai:draft:'
export const DRAFT_TTL_MS = 24 * 60 * 60 * 1000

export interface StoredDraft<T> {
  values: T
  savedAt: number
}

export function readDraft<T>(key: string): StoredDraft<T> | null {
  try {
    const raw = window.localStorage.getItem(DRAFT_PREFIX + key)
    if (!raw)
      return null
    const draft = JSON.parse(raw) as StoredDraft<T>
    if (!draft || typeof draft.savedAt !== 'number' || Date.now() - draft.savedAt > DRAFT_TTL_MS) {
      window.localStorage.removeItem(DRAFT_PREFIX + key)
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
    window.localStorage.setItem(DRAFT_PREFIX + key, JSON.stringify({ values, savedAt: Date.now() }))
  }
  catch {
    // Storage full / unavailable: drafts are best-effort.
  }
}

export function removeDraft(key: string) {
  try {
    window.localStorage.removeItem(DRAFT_PREFIX + key)
  }
  catch {}
}

export function clearAllFormDrafts() {
  try {
    for (const key of Object.keys(window.localStorage)) {
      if (key.startsWith(DRAFT_PREFIX))
        window.localStorage.removeItem(key)
    }
  }
  catch {}
}

export function clearRecentPatients() {
  try {
    window.localStorage.removeItem('sabai:recent-patients')
  }
  catch {}
}
