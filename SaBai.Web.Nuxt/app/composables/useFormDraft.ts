import { useEventListener } from "@vueuse/core"
import type { Ref } from 'vue'

/**
 * Keeps a form's unsaved input in localStorage so a reload (or a dropped
 * connection, or a closed tab) doesn't lose it.
 *
 *   const draft = useFormDraft(() => `vitals:${visitId}`, form)
 *   // after loading saved values from the API:
 *   form.value = draft.resume(serverValues)   // draft wins if one exists
 *   // after a successful save:
 *   draft.clear()
 *
 * While the form differs from the last loaded/saved baseline it is written
 * (debounced) to storage; when it matches again the draft is removed.
 * `restoredAt` is set when a draft was applied, for <DraftNotice>.
 */
export function useFormDraft<T>(key: MaybeRefOrGetter<string>, form: Ref<T>) {
  const restoredAt = ref<Date | null>(null)
  let baseline = ''
  let ready = false
  let timer: number | undefined

  /** Returns the stored draft for this form if there is one, else `fresh`. Also sets the baseline. */
  function resume(fresh: T): T {
    baseline = JSON.stringify(fresh)
    ready = true
    const draft = readDraft<T>(toValue(key))
    if (draft && JSON.stringify(draft.values) !== baseline) {
      restoredAt.value = new Date(draft.savedAt)
      return draft.values
    }
    restoredAt.value = null
    return fresh
  }

  /** Marks `values` as saved: the new baseline, and no draft. */
  function clear(values?: T) {
    window.clearTimeout(timer)
    if (values !== undefined)
      baseline = JSON.stringify(values)
    removeDraft(toValue(key))
    restoredAt.value = null
  }

  /** Drops the draft and returns the baseline values to put back in the form. */
  function discard(): T {
    clear()
    return JSON.parse(baseline) as T
  }

  function persist() {
    if (!ready)
      return
    const json = JSON.stringify(form.value)
    if (json === baseline)
      removeDraft(toValue(key))
    else writeDraft(toValue(key), form.value)
  }

  watch(form, () => {
    window.clearTimeout(timer)
    timer = window.setTimeout(persist, 400)
  }, { deep: true })

  // Flush on tab close / reload so the last keystrokes aren't lost.
  useEventListener(window, 'pagehide', persist)
  onBeforeUnmount(() => {
    window.clearTimeout(timer)
    persist()
  })

  return { resume, clear, discard, restoredAt }
}
