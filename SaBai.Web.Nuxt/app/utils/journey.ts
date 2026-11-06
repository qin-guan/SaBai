/**
 * A visit's journey through the clinic:
 *
 *   Vitals → Vision → Consult → Dental (optional) → Pharmacy (optional)
 *
 * Built from the API's per-visit `journey` flags so every screen (queue,
 * patient record, station pages) shows the same at-a-glance status.
 */

export type StationKey = 'vitals' | 'vision' | 'consults' | 'dental' | 'pharmacy'

/**
 * - `done`: recorded.
 * - `todo`: still needed (required step, a dental referral, or undispensed medication).
 * - `optional`: not needed unless asked for (no dental referral / nothing prescribed).
 */
export type StepState = 'done' | 'todo' | 'optional'

export interface Journey {
  vitals: boolean
  vision: boolean
  consult: boolean
  dental: boolean
  dentalReferred: boolean
  pharmacyOrders: number
  pharmacyPending: number
  /** When each step was recorded (ISO); used for throughput and wait estimates. */
  vitalsAt?: string | null
  visionAt?: string | null
  consultAt?: string | null
  dentalAt?: string | null
  pharmacyAt?: string | null
}

export interface JourneyStep {
  key: StationKey
  label: string
  state: StepState
  /** Short status for tooltips / screen readers, e.g. "Referred", "2 to dispense". */
  detail: string
  optional: boolean
}

export const STATIONS: { key: StationKey, label: string, optional: boolean }[] = [
  { key: 'vitals', label: 'Vitals', optional: false },
  { key: 'vision', label: 'Vision', optional: false },
  { key: 'consults', label: 'Consult', optional: false },
  { key: 'dental', label: 'Dental', optional: true },
  { key: 'pharmacy', label: 'Pharmacy', optional: true },
]

export function isStationKey(value: unknown): value is StationKey {
  return typeof value === 'string' && STATIONS.some(s => s.key === value)
}

export function stationLabel(key: StationKey) {
  return STATIONS.find(s => s.key === key)!.label
}

export function journeySteps(journey: Journey): JourneyStep[] {
  return STATIONS.map(({ key, label, optional }) => {
    const { state, detail } = stepState(journey, key)
    return { key, label, optional, state, detail }
  })
}

export function stepState(journey: Journey, key: StationKey): { state: StepState, detail: string } {
  switch (key) {
    case 'vitals':
      return journey.vitals ? { state: 'done', detail: 'Recorded' } : { state: 'todo', detail: 'Not yet' }
    case 'vision':
      return journey.vision ? { state: 'done', detail: 'Recorded' } : { state: 'todo', detail: 'Not yet' }
    case 'consults':
      return journey.consult ? { state: 'done', detail: 'Seen' } : { state: 'todo', detail: 'Not yet' }
    case 'dental':
      if (journey.dental)
        return { state: 'done', detail: 'Seen' }
      return journey.dentalReferred
        ? { state: 'todo', detail: 'Referred' }
        : { state: 'optional', detail: 'If needed' }
    case 'pharmacy':
      if (journey.pharmacyPending > 0)
        return { state: 'todo', detail: `${journey.pharmacyPending} to dispense` }
      return journey.pharmacyOrders > 0
        ? { state: 'done', detail: 'Dispensed' }
        : { state: 'optional', detail: 'Nothing prescribed' }
  }
}

/** The first step still to do, or null when the journey is complete. */
export function nextStep(journey: Journey): JourneyStep | null {
  return journeySteps(journey).find(s => s.state === 'todo') ?? null
}

/** How many required steps (vitals, vision, consult) are done. */
export function requiredProgress(journey: Journey) {
  const required = journeySteps(journey).filter(s => !s.optional)
  return { done: required.filter(s => s.state === 'done').length, total: required.length }
}

/**
 * Whether a patient belongs in a station's "waiting" list:
 *  - vitals / vision / consult: not recorded yet;
 *  - dental: not seen yet, and referred or done with their consult (it is
 *    optional, so it isn't waiting for patients still in the main flow);
 *  - pharmacy: has medication still to dispense.
 */
export function isWaitingFor(journey: Journey, station: StationKey) {
  const { state } = stepState(journey, station)
  if (station === 'dental')
    return state !== 'done' && (journey.dentalReferred || journey.consult)
  return state === 'todo'
}

/**
 * Sort key inside a station's queue: patients the station is most clearly
 * needed for first (a dental referral before walk-ins, a patient who finished
 * the earlier steps before one who skipped ahead).
 */
export function stationPriority(journey: Journey, station: StationKey) {
  if (station === 'dental')
    return journey.dentalReferred ? 0 : 1
  const index = STATIONS.findIndex(s => s.key === station)
  const earlier = STATIONS.slice(0, index).filter(s => !s.optional)
  return earlier.filter(s => stepState(journey, s.key).state !== 'done').length
}
