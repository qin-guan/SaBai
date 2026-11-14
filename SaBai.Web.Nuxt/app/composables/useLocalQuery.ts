import type { MaybeRefOrGetter } from 'vue'

export interface LocalQueryResult<T> {
  data: Ref<T | undefined>
  isLoading: Ref<boolean>
  isError: Ref<boolean>
  error: Ref<Error | null>
  refetch: () => Promise<void>
}

/**
 * Runs a reactive PowerSync SQLite query that automatically re-evaluates whenever
 * any of the listed `tables` change locally or via sync, or when reactive query
 * parameters change. Matches the `{ data, isLoading, isError, error, refetch }`
 * shape of TanStack Query so existing Nuxt pages work seamlessly.
 */
export function useLocalQuery<T>(
  tables: string[],
  fetcher: () => Promise<T>,
  options?: {
    enabled?: MaybeRefOrGetter<boolean>
    watchDeps?: MaybeRefOrGetter<unknown>[]
  },
): LocalQueryResult<T> {
  const data = ref<T | undefined>(undefined) as Ref<T | undefined>
  const isLoading = ref(true)
  const isError = ref(false)
  const error = ref<Error | null>(null)

  let runId = 0

  async function execute() {
    const enabled = options?.enabled === undefined ? true : Boolean(toValue(options.enabled))
    if (!enabled) {
      isLoading.value = false
      return
    }

    const currentRun = ++runId
    try {
      const result = await fetcher()
      if (currentRun !== runId) return
      data.value = result
      isError.value = false
      error.value = null
    } catch (err) {
      if (currentRun !== runId) return
      isError.value = true
      error.value = err instanceof Error ? err : new Error(String(err))
    } finally {
      if (currentRun === runId) {
        isLoading.value = false
      }
    }
  }

  onMounted(() => {
    void execute()

    let disposeWatch: (() => void) | undefined
    try {
      const db = getPowerSyncInstance()
      const controller = new AbortController()
      const tableSet = new Set(tables)
      // PowerSync's onChange emits whenever any of the specified tables are mutated.
      const unsubscribe = db.onChange(
        {
          onChange: (event) => {
            if (event.changedTables.some(t => tableSet.has(t))) {
              void execute()
            }
          },
        },
        { tables, signal: controller.signal },
      )
      disposeWatch = () => {
        controller.abort()
        unsubscribe?.()
      }
    } catch {
      // PowerSync may not be initialized in unit tests
    }

    onUnmounted(() => {
      disposeWatch?.()
    })
  })

  if (options?.enabled !== undefined || options?.watchDeps?.length) {
    watch(
      [
        () => (options?.enabled === undefined ? true : Boolean(toValue(options.enabled))),
        ...(options?.watchDeps ?? []).map(dep => () => toValue(dep)),
      ],
      () => {
        void execute()
      },
    )
  }

  return {
    data,
    isLoading,
    isError,
    error,
    refetch: execute,
  }
}
