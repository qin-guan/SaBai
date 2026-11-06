import type { StationKey } from './journey'

/**
 * Rough per-station service time, timed in the browser: from the first
 * keystroke or tap in a station form to its save. A partly filled form is
 * scaled up to a full form (a save at 50% filled after 4 minutes counts as
 * ~8 minutes), so quick partial saves don't make a station look faster than
 * it is. Kept on this device only (no API or database changes): it is a
 * guesstimate used when there isn't enough measured throughput yet.
 */

export interface ServiceSample {
  /** When it was saved (ms since epoch). */
  at: number
  /** Estimated minutes for a complete form. */
  minutes: number
}

const KEY = 'sabai:service-times'
const MAX_SAMPLES = 20
const MAX_AGE_MS = 24 * 60 * 60_000
/** Ignore accidental instant saves and forms left open over a break. */
export const MIN_SAMPLE_MINUTES = 0.25
export const MAX_SAMPLE_MINUTES = 60
/** Never scale up by more than 4×, even for a nearly empty form. */
const MIN_COMPLETENESS = 0.25
/** At this fill level the form counts as finished. */
export const COMPLETE_AT = 0.95

/** Share of fillable fields (strings, numbers, list items) that have a value. Booleans are ignored. */
export function formCompleteness(values: unknown): number {
  let filled = 0
  let total = 0
  const visit = (v: unknown) => {
    if (typeof v === 'boolean')
      return
    if (Array.isArray(v)) {
      if (v.length === 0)
        total++
      v.forEach(visit)
      return
    }
    if (v && typeof v === 'object') {
      Object.values(v).forEach(visit)
      return
    }
    total++
    if (v !== null && v !== undefined && String(v).trim() !== '')
      filled++
  }
  visit(values)
  return total ? filled / total : 1
}

/** A sample from one timed form, or null if it should be ignored. */
export function serviceSample(startedAt: number, savedAt: number, completeness: number): ServiceSample | null {
  const elapsed = (savedAt - startedAt) / 60_000
  if (!(elapsed >= MIN_SAMPLE_MINUTES && elapsed <= MAX_SAMPLE_MINUTES))
    return null
  const scale = completeness >= COMPLETE_AT ? 1 : Math.max(completeness, MIN_COMPLETENESS)
  return { at: savedAt, minutes: elapsed / scale }
}

export function median(values: number[]): number | null {
  if (!values.length)
    return null
  const sorted = [...values].sort((a, b) => a - b)
  const mid = Math.floor(sorted.length / 2)
  return sorted.length % 2 ? sorted[mid]! : (sorted[mid - 1]! + sorted[mid]!) / 2
}

type Store = Partial<Record<StationKey, ServiceSample[]>>

function read(): Store {
  try {
    const parsed = JSON.parse(window.localStorage.getItem(KEY) ?? '{}')
    return parsed && typeof parsed === 'object' ? parsed : {}
  }
  catch {
    return {}
  }
}

export function recordServiceSample(station: StationKey, sample: ServiceSample) {
  const store = read()
  const fresh = (store[station] ?? []).filter(s => sample.at - s.at < MAX_AGE_MS)
  store[station] = [...fresh, sample].slice(-MAX_SAMPLES)
  try {
    window.localStorage.setItem(KEY, JSON.stringify(store))
  }
  catch {}
}

/** Median service minutes for a station from this device's recent samples, or null. */
export function typicalServiceMinutes(station: StationKey, now = Date.now()): { minutes: number | null, samples: number } {
  const samples = (read()[station] ?? []).filter(s => now - s.at < MAX_AGE_MS)
  return { minutes: samples.length >= 2 ? median(samples.map(s => s.minutes)) : null, samples: samples.length }
}
