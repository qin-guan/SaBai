import { useEventListener } from '@vueuse/core'
import type { StationKey } from '~/utils/journey'

/**
 * Times a station form for the wait estimates (see utils/serviceTime.ts):
 * starts at the first key press or tap inside `formEl`, and `saved()` —
 * called on a successful save, before the form is cleared — records the
 * sample and restarts the timer for the next patient.
 */
export function useServiceTimer(station: StationKey, formEl: Ref<HTMLElement | undefined>, values: () => unknown) {
  let startedAt: number | null = null
  const start = () => {
    startedAt ??= Date.now()
  }
  useEventListener(formEl, 'keydown', start, { passive: true })
  useEventListener(formEl, 'pointerdown', start, { passive: true })

  function saved() {
    if (startedAt === null)
      return
    const sample = serviceSample(startedAt, Date.now(), formCompleteness(values()))
    if (sample)
      recordServiceSample(station, sample)
    startedAt = null
  }

  return { saved }
}
