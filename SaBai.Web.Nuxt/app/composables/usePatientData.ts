import type { MaybeRefOrGetter } from 'vue'
import type { LocalPatientSummary, LocalPatientWithLatestVisit, LocalVisitSummary } from '~/db/types'
import type { Journey } from '~/utils/journey'

export type PatientDetail = LocalPatientSummary
export type PatientListItem = LocalPatientSummary
export type PatientWithLatestVisitItem = LocalPatientWithLatestVisit

interface RawPatientRow {
  id: string
  number: number | null
  village_code_id: string | null
  village_code: string | null
  village_color_hex: string | null
  name: string
  identification_number: string | null
  contact_no: string | null
  gender: string | null
  date_of_birth: string
  drug_allergy: string
  has_poor_card: number
  has_bs2_card: number
  has_sabai_card: number
  to_get_report: number
  patient_image_public_id: string | null
  rekognition_face_id: string | null
  last_visit_id?: string | null
  last_visit_date?: string | null
  created_at: string
}

interface RawVisitJourneyRow {
  id: string
  patient_id: string
  patient_name: string | null
  village_code_id: string
  village_code: string | null
  village_name: string | null
  village_color_hex: string | null
  date: string
  status: string
  vitals_at: string | null
  vision_at: string | null
  consult_at: string | null
  dental_at: string | null
  dental_referred: number
  pharmacy_orders: number
  pharmacy_pending: number
  pharmacy_at: string | null
}

function formatDisplayId(villageCode: string | null | undefined, num: number | null | undefined): string {
  if (!num || num <= 0) return `${villageCode ?? ''}NEW`
  return `${villageCode ?? ''}${String(num).padStart(4, '0')}`
}

function normalizeGender(raw: string | null | undefined): 'male' | 'female' | 'other' {
  const g = (raw ?? '').toLowerCase()
  if (g === 'male') return 'male'
  if (g === 'other') return 'other'
  return 'female'
}

function mapPatient(r: RawPatientRow, tags: string[] = []): LocalPatientSummary {
  const num = Number(r.number ?? 0)
  const displayId = formatDisplayId(r.village_code, num)
  return {
    id: r.id,
    number: num,
    displayId,
    filterString: `${displayId} ${r.name} ${r.identification_number ?? ''} ${r.contact_no ?? ''} ${tags.join(' ')}`.toLowerCase(),
    villageCodeId: r.village_code_id,
    villageCode: r.village_code,
    villageColorHex: r.village_color_hex ?? undefined,
    name: r.name,
    identificationNumber: r.identification_number,
    contactNo: r.contact_no,
    gender: normalizeGender(r.gender),
    dateOfBirth: r.date_of_birth,
    drugAllergy: r.drug_allergy ?? 'None',
    hasPoorCard: Boolean(r.has_poor_card),
    hasBS2Card: Boolean(r.has_bs2_card),
    hasSabaiCard: Boolean(r.has_sabai_card),
    toGetReport: Boolean(r.to_get_report),
    hasPhoto: Boolean(r.patient_image_public_id),
    hasFace: Boolean(r.rekognition_face_id),
    patientImagePublicId: r.patient_image_public_id,
    lastVisitId: r.last_visit_id ?? null,
    lastVisitDate: r.last_visit_date ?? null,
    createdAt: r.created_at,
    tags,
  }
}

async function loadTagsByPatient(): Promise<Map<string, string[]>> {
  const db = getPowerSyncInstance()
  const rows = await db.getAll<{ patient_id: string, name: string | null }>(
    `SELECT pt.patient_id, COALESCE(t.name, pt.tag_name) AS name
     FROM patient_tags pt
     LEFT JOIN tags t ON t.id = pt.tag_id
     ORDER BY COALESCE(t.name, pt.tag_name) ASC`,
  )
  const map = new Map<string, string[]>()
  for (const r of rows) {
    if (!r.name) continue
    let list = map.get(r.patient_id)
    if (!list) {
      list = []
      map.set(r.patient_id, list)
    }
    if (!list.includes(r.name)) {
      list.push(r.name)
    }
  }
  return map
}

function mapVisit(v: RawVisitJourneyRow): LocalVisitSummary {
  const journey: Journey = {
    vitals: Boolean(v.vitals_at),
    vision: Boolean(v.vision_at),
    consult: Boolean(v.consult_at),
    dental: Boolean(v.dental_at),
    dentalReferred: Boolean(v.dental_referred),
    pharmacyOrders: Number(v.pharmacy_orders ?? 0),
    pharmacyPending: Number(v.pharmacy_pending ?? 0),
    vitalsAt: v.vitals_at,
    visionAt: v.vision_at,
    consultAt: v.consult_at,
    dentalAt: v.dental_at,
    pharmacyAt: v.pharmacy_at,
  }
  return {
    id: v.id,
    patientId: v.patient_id,
    patientName: v.patient_name ?? '',
    villageCodeId: v.village_code_id,
    villageCode: v.village_code,
    villageName: v.village_name,
    villageColorHex: v.village_color_hex ?? undefined,
    date: v.date,
    status: v.status,
    journey,
  }
}

const VISIT_JOURNEY_SQL = `
  SELECT
    v.id,
    v.patient_id,
    p.name AS patient_name,
    v.village_code_id,
    vc.code AS village_code,
    vc.name AS village_name,
    vc.color_hex AS village_color_hex,
    v.date,
    v.status,
    (SELECT COALESCE(vt.recorded_at, vt.updated_at) FROM vitals vt WHERE vt.visit_id = v.id LIMIT 1) AS vitals_at,
    (SELECT COALESCE(ey.recorded_at, ey.updated_at) FROM eyesights ey WHERE ey.visit_id = v.id LIMIT 1) AS vision_at,
    (SELECT MAX(c.date) FROM consults c WHERE c.visit_id = v.id) AS consult_at,
    (SELECT COALESCE(dc.recorded_at, dc.updated_at) FROM dental_consults dc WHERE dc.visit_id = v.id LIMIT 1) AS dental_at,
    EXISTS(SELECT 1 FROM consults c WHERE c.visit_id = v.id AND c.referred_for IS NOT NULL AND TRIM(c.referred_for) <> '') AS dental_referred,
    (SELECT COUNT(*) FROM orders o JOIN consults c ON c.id = o.consult_id WHERE c.visit_id = v.id) AS pharmacy_orders,
    (SELECT COUNT(*) FROM orders o JOIN consults c ON c.id = o.consult_id WHERE c.visit_id = v.id AND LOWER(COALESCE(o.status, 'pending')) = 'pending') AS pharmacy_pending,
    (SELECT MAX(o.approved_at) FROM orders o JOIN consults c ON c.id = o.consult_id WHERE c.visit_id = v.id) AS pharmacy_at
  FROM visits v
  LEFT JOIN patients p ON p.id = v.patient_id
  LEFT JOIN village_codes vc ON vc.id = v.village_code_id
`

/** All patients (used by the patients page and the global patient search). */
export function usePatientsList() {
  return useLocalQuery<{ patients: LocalPatientSummary[] }>(
    ['patients', 'village_codes', 'patient_tags', 'tags'],
    async () => {
      const db = getPowerSyncInstance()
      const [rows, tagsByPatient] = await Promise.all([
        db.getAll<RawPatientRow>(
          `SELECT p.*, vc.code AS village_code, vc.color_hex AS village_color_hex
           FROM patients p
           LEFT JOIN village_codes vc ON vc.id = p.village_code_id
           ORDER BY p.created_at DESC`,
        ),
        loadTagsByPatient(),
      ])
      return { patients: rows.map(r => mapPatient(r, tagsByPatient.get(r.id) ?? [])) }
    },
  )
}

/** Patients with their latest visit's village & journey (station worklists). */
export function usePatientsWithLatestVisit() {
  return useLocalQuery<{ patients: LocalPatientWithLatestVisit[] }>(
    ['patients', 'visits', 'village_codes', 'vitals', 'eyesights', 'dental_consults', 'consults', 'orders', 'patient_tags', 'tags'],
    async () => {
      const db = getPowerSyncInstance()
      const [rows, visitRows, tagsByPatient] = await Promise.all([
        db.getAll<RawPatientRow>(
          `SELECT p.*, vc.code AS village_code, vc.color_hex AS village_color_hex
           FROM patients p
           LEFT JOIN village_codes vc ON vc.id = p.village_code_id
           ORDER BY p.created_at DESC`,
        ),
        db.getAll<RawVisitJourneyRow>(
          `${VISIT_JOURNEY_SQL} ORDER BY v.date DESC`,
        ),
        loadTagsByPatient(),
      ])
      const latestByPatient = new Map<string, LocalVisitSummary>()
      for (const row of visitRows) {
        if (!latestByPatient.has(row.patient_id)) {
          latestByPatient.set(row.patient_id, mapVisit(row))
        }
      }
      return {
        patients: rows.map((r) => {
          const patient = mapPatient(r, tagsByPatient.get(r.id) ?? [])
          const latestVisit = latestByPatient.get(r.id) ?? null
          return {
            ...patient,
            latestVisit,
            lastVisitId: latestVisit?.id ?? null,
            lastVisitDate: latestVisit?.date ?? null,
            lastVisitJourney: latestVisit?.journey ?? null,
          }
        }),
      }
    },
  )
}

export function usePatient(id: MaybeRefOrGetter<string | null>) {
  return useLocalQuery<LocalPatientSummary | null>(
    ['patients', 'visits', 'village_codes', 'patient_tags', 'tags'],
    async () => {
      const patientId = toValue(id)
      if (!patientId) return null
      const db = getPowerSyncInstance()
      const [row, tagRows] = await Promise.all([
        db.getOptional<RawPatientRow>(
          `SELECT p.*, vc.code AS village_code, vc.color_hex AS village_color_hex,
                  (SELECT v.id FROM visits v WHERE v.patient_id = p.id ORDER BY v.date DESC LIMIT 1) AS last_visit_id,
                  (SELECT v.date FROM visits v WHERE v.patient_id = p.id ORDER BY v.date DESC LIMIT 1) AS last_visit_date
           FROM patients p
           LEFT JOIN village_codes vc ON vc.id = p.village_code_id
           WHERE p.id = ?`,
          [patientId],
        ),
        db.getAll<{ name: string | null }>(
          `SELECT COALESCE(t.name, pt.tag_name) AS name
           FROM patient_tags pt
           LEFT JOIN tags t ON t.id = pt.tag_id
           WHERE pt.patient_id = ?
           ORDER BY COALESCE(t.name, pt.tag_name) ASC`,
          [patientId],
        ),
      ])
      if (!row) return null
      const tags = tagRows.map(t => t.name).filter((n): n is string => Boolean(n))
      return mapPatient(row, tags)
    },
    {
      enabled: computed(() => Boolean(toValue(id))),
      watchDeps: [() => toValue(id)],
    },
  )
}

export function usePatientVisits(id: MaybeRefOrGetter<string | null>) {
  return useLocalQuery<{ visits: LocalVisitSummary[] }>(
    ['visits', 'patients', 'village_codes', 'vitals', 'eyesights', 'dental_consults', 'consults', 'orders'],
    async () => {
      const patientId = toValue(id)
      if (!patientId) return { visits: [] }
      const db = getPowerSyncInstance()
      const rows = await db.getAll<RawVisitJourneyRow>(
        `${VISIT_JOURNEY_SQL} WHERE v.patient_id = ? ORDER BY v.date DESC`,
        [patientId],
      )
      return { visits: rows.map(mapVisit) }
    },
    {
      enabled: computed(() => Boolean(toValue(id))),
      watchDeps: [() => toValue(id)],
    },
  )
}

/** Extracts a UUID route param, returning null if missing. */
export function useRouteId(param = 'id') {
  const route = useRoute()
  return computed(() => {
    const raw = String(route.params[param] ?? '').trim()
    return raw.length > 0 ? raw : null
  })
}
