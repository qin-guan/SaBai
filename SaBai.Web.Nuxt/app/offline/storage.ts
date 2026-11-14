import { clear, createStore, del, entries, get, set } from 'idb-keyval'

/** The small key-value interface the offline core persists through (IndexedDB in the app, memory in tests). */
export interface KeyValueStore<T> {
  get: (key: string) => Promise<T | undefined>
  set: (key: string, value: T) => Promise<void>
  del: (key: string) => Promise<void>
  entries: () => Promise<Array<[string, T]>>
  clear: () => Promise<void>
}

/** An IndexedDB object store (one database per store, as idb-keyval expects). */
export function idbStore<T>(dbName: string, storeName: string): KeyValueStore<T> {
  const store = createStore(dbName, storeName)
  return {
    get: key => get<T>(key, store),
    set: (key, value) => set(key, value, store),
    del: key => del(key, store),
    entries: () => entries<string, T>(store),
    clear: () => clear(store),
  }
}

/** An in-memory store, for tests and for browsers where IndexedDB is unavailable. */
export function memoryStore<T>(): KeyValueStore<T> {
  const map = new Map<string, T>()
  return {
    get: async key => structuredClone(map.get(key)),
    set: async (key, value) => {
      map.set(key, structuredClone(value))
    },
    del: async (key) => {
      map.delete(key)
    },
    entries: async () => [...map.entries()].map(([k, v]) => [k, structuredClone(v)] as [string, T]),
    clear: async () => {
      map.clear()
    },
  }
}

/** Reads a small JSON value from localStorage; undefined when absent or storage is blocked. */
export function readLocal<T>(key: string): T | undefined {
  try {
    const raw = globalThis.localStorage?.getItem(key)
    return raw ? (JSON.parse(raw) as T) : undefined
  }
  catch {
    return undefined
  }
}

/** Writes (or with `undefined`, removes) a small JSON value in localStorage, ignoring storage errors. */
export function writeLocal(key: string, value: unknown): void {
  try {
    if (value === undefined) globalThis.localStorage?.removeItem(key)
    else globalThis.localStorage?.setItem(key, JSON.stringify(value))
  }
  catch {
    // Storage full or blocked (private mode): keep working in memory.
  }
}
