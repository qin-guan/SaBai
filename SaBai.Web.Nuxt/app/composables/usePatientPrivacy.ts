import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import {
  consentsKey,
  disclosuresKey,
  recordConsent,
  recordDisclosure,
} from '~/offline/mutations/patients'
import {
  saBaiAPIEndpointsPatientsConsentsGetEndpoint,
  saBaiAPIEndpointsPatientsDisclosuresGetEndpoint,
} from '@sabai/sdk/clients'
import { uuidv7 } from '~/utils/uuid'

export type ConsentType = 'NoticeOfPrivacyPractices' | 'BiometricFaceRecognition' | 'ClinicalDataSharing'

export const CONSENT_LABELS: Record<ConsentType, string> = {
  NoticeOfPrivacyPractices: 'Notice of Privacy Practices',
  BiometricFaceRecognition: 'Face recognition (biometric)',
  ClinicalDataSharing: 'Clinical data sharing',
}

export interface PatientConsent {
  id: string
  consentType: ConsentType
  granted: boolean
  recordedAt: string
  revokedAt: string | null
  recordedByUserId: string
  notes: string | null
}

export interface PatientDisclosure {
  id: string
  recipient: string
  purpose: string
  description: string | null
  disclosedAt: string
  disclosedByUserId: string
}

/** Roles the API lets read the accounting of disclosures (HIPAA §164.528). */
const DISCLOSURE_ROLES = ['admin', 'member', 'doctor', 'auditor']
/** Roles the API lets record a disclosure. */
const DISCLOSURE_WRITE_ROLES = ['admin', 'member', 'doctor']

export function canViewDisclosures(roles: readonly string[] | undefined) {
  return !!roles?.some(r => DISCLOSURE_ROLES.includes(r))
}
export function canRecordDisclosures(roles: readonly string[] | undefined) {
  return !!roles?.some(r => DISCLOSURE_WRITE_ROLES.includes(r))
}

/**
 * The latest decision per consent type. The API returns every recorded change;
 * a patient's current state is the most recent one for each type.
 */
export function currentConsents(consents: readonly PatientConsent[]) {
  const latest = new Map<ConsentType, PatientConsent>()
  for (const c of consents) {
    const prev = latest.get(c.consentType)
    const newer = !prev || new Date(c.recordedAt) > new Date(prev.recordedAt)
      || (c.recordedAt === prev.recordedAt && c.id > prev.id)
    if (newer)
      latest.set(c.consentType, c)
  }
  return latest
}

export function usePatientConsents(patientId: MaybeRefOrGetter<string | null>) {
  return useQuery({
    queryKey: computed(() => consentsKey(toValue(patientId) ?? '')),
    enabled: computed(() => !!toValue(patientId)),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsConsentsGetEndpoint({ ...apiConfig, path: { patientId: toValue(patientId)! }, signal })
        .unwrap()
        .then(x => x.consents as PatientConsent[]),
  })
}

/** Records a consent decision; saved on this device when offline. */
export function useRecordConsent(patientId: MaybeRefOrGetter<string | null>) {
  const { user } = useCurrentUser()
  const { mutateAsync, isPending } = useOfflineMutation(recordConsent)
  return {
    isPending,
    mutateAsync: (input: { consentType: ConsentType, granted: boolean, notes?: string }) =>
      mutateAsync({
        id: uuidv7(),
        patientId: toValue(patientId)!,
        consentType: input.consentType,
        granted: input.granted,
        notes: input.notes?.trim() || null,
        recordedByUserId: user.value?.id ?? '',
        recordedAt: new Date().toISOString(),
      }),
  }
}

export function usePatientDisclosures(patientId: MaybeRefOrGetter<string | null>, enabled: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: computed(() => disclosuresKey(toValue(patientId) ?? '')),
    enabled: computed(() => !!toValue(patientId) && toValue(enabled)),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsDisclosuresGetEndpoint({ ...apiConfig, path: { patientId: toValue(patientId)! }, signal })
        .unwrap()
        .then(x => x.disclosures as PatientDisclosure[]),
  })
}

/** Records a disclosure; saved on this device when offline. */
export function useRecordDisclosure(patientId: MaybeRefOrGetter<string | null>) {
  const { user } = useCurrentUser()
  const { mutateAsync, isPending } = useOfflineMutation(recordDisclosure)
  return {
    isPending,
    mutateAsync: (input: { recipient: string, purpose: string, description?: string }) =>
      mutateAsync({
        id: uuidv7(),
        patientId: toValue(patientId)!,
        recipient: input.recipient.trim(),
        purpose: input.purpose.trim(),
        description: input.description?.trim() || null,
        disclosedByUserId: user.value?.id ?? '',
        disclosedAt: new Date().toISOString(),
      }),
  }
}
