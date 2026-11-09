import type { Journey, StationKey } from './journey'

/**
 * How long a station is backed up, from its measured throughput.
 *
 * A station's throughput is how many patients it finished per minute,
 * measured from the time each record was saved (vitals, vision, first
 * consult, dental, last dispense). It reflects however many staff are
 * working there, so no staffing input is needed:
 *
 *   rate = completions in the window / window length (minutes)
 *   wait for someone sent there now ≈ patients still waiting / rate
 *
 * The window is the last hour, so the estimate follows the clinic's current
 * pace. When the last hour has too few completions to be meaningful it falls
 * back to the last 12 hours (spanning overnight clinics without pulling in old
 * clinic days). With fewer than MIN_SAMPLES completions there is no estimate.
 */

export const WINDOW_MINUTES = 60
export const SESSION_MINUTES = 12 * 60
export const MIN_SAMPLES = 3
/** Shortest span to divide by, so two quick completions don't imply a huge rate. */
export const MIN_SPAN_MINUTES = 10

export interface StationThroughput {
  /** Patients finished per minute, or null without enough data. */
  perMinute: number | null
  /** Completions the rate is based on. */
  samples: number
  /** What the rate is based on: saved records (last hour / recent session) or this device's form timings. */
  basis: 'last hour' | 'recent' | 'form timing' | null
}

type TimestampKey = 'vitalsAt' | 'visionAt' | 'consultAt' | 'dentalAt' | 'pharmacyAt'

const TIMESTAMP: Record<StationKey, TimestampKey> = {
  vitals: 'vitalsAt',
  vision: 'visionAt',
  consults: 'consultAt',
  dental: 'dentalAt',
  pharmacy: 'pharmacyAt',
}

export function completionTimes(journeys: Journey[], station: StationKey): number[] {
  const key = TIMESTAMP[station]
  return journeys
    .map(j => j[key])
    .filter((v): v is string => typeof v === 'string')
    .map(v => new Date(v).getTime())
    .filter(t => Number.isFinite(t))
}

/** Throughput of one station, from completions that happened before `now`. */
export function stationThroughput(journeys: Journey[], station: StationKey, now = Date.now()): StationThroughput {
  const times = completionTimes(journeys, station).filter(t => t <= now)
  const sessionStart = now - SESSION_MINUTES * 60_000
  const session = times.filter(t => t >= sessionStart)
  const windowStart = now - WINDOW_MINUTES * 60_000
  const recent = session.filter(t => t >= windowStart)
  if (recent.length >= MIN_SAMPLES) {
    // Measure from the first completion in the window if the station started within it.
    const span = Math.max((now - Math.max(windowStart, Math.min(...session))) / 60_000, MIN_SPAN_MINUTES)
    return { perMinute: recent.length / span, samples: recent.length, basis: 'last hour' }
  }
  if (session.length >= MIN_SAMPLES) {
    const span = Math.max((now - Math.min(...session)) / 60_000, MIN_SPAN_MINUTES)
    return { perMinute: session.length / span, samples: session.length, basis: 'recent' }
  }
  return { perMinute: null, samples: session.length, basis: null }
}

/**
 * Measured throughput when there is enough of it; otherwise one patient per
 * typical form-fill time from this device (utils/serviceTime.ts).
 */
export function withServiceTimeFallback(measured: StationThroughput, serviceMinutes: number | null): StationThroughput {
  if (measured.perMinute || !serviceMinutes)
    return measured
  return { perMinute: 1 / serviceMinutes, samples: measured.samples, basis: 'form timing' }
}

/**
 * Roughly how many minutes someone sent to the station now would wait: the
 * time to see everyone already waiting there. Null without a rate.
 */
export function stationWaitMinutes(throughput: StationThroughput, waiting: number): number | null {
  if (!throughput.perMinute)
    return null
  return Math.max(waiting, 0) / throughput.perMinute
}

/** "<5 min", "~15 min", "~1 h 20 min". Rounded to 5 minutes: it is an estimate. */
export function formatWait(minutes: number | null): string {
  if (minutes === null || !Number.isFinite(minutes))
    return '—'
  if (minutes < 5)
    return '<5 min'
  const rounded = Math.round(minutes / 5) * 5
  if (rounded < 60)
    return `~${rounded} min`
  const h = Math.floor(rounded / 60)
  const m = rounded % 60
  return m ? `~${h} h ${m} min` : `~${h} h`
}

/** "12 / h" from a per-minute rate. */
export function formatRate(perMinute: number | null): string {
  if (!perMinute)
    return '—'
  const perHour = perMinute * 60
  return `${perHour < 10 ? perHour.toFixed(1) : Math.round(perHour)} / h`
}
