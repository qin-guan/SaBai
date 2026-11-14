import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'

export type ConsentType = 'NoticeOfPrivacyPractices' | 'BiometricFaceRecognition' | 'ClinicalDataSharing'

export const CONSENT_LABELS: Record<ConsentType, string> = {
  NoticeOfPrivacyPractices: 'Notice of Privacy Practices',
  BiometricFaceRecognition: 'Face recognition (biometric)',
  ClinicalDataSharing: 'Clinical data sharing',
}

export interface PatientConsent {
  id: string | number
  consentType: ConsentType
  granted: boolean
  recordedAt: string
  revokedAt: string | null
  recordedByUserId: string
  notes: string | null
}

export interface PatientDisclosure {
  id: string | number
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
      || (c.recordedAt === prev.recordedAt && String(c.id) > String(prev.id))
    if (newer)
      latest.set(c.consentType, c)
  }
  return latest
}

export function usePatientConsents(patientId: MaybeRefOrGetter<string | null>) {
  return useQuery({
    queryKey: computed(() => ['patients', 'consents', toValue(patientId) ?? ''] as const),
    enabled: computed(() => !!toValue(patientId)),
    queryFn: ({ signal }) =>
      $fetch<{ consents: PatientConsent[] }>(`/api/patients/${toValue(patientId)}/consents`, {
        credentials: 'same-origin',
        signal,
      }).then(x => x.consents),
  })
}

export function useRecordConsent(patientId: MaybeRefOrGetter<string | null>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { consentType: ConsentType, granted: boolean, notes?: string }) =>
      $fetch(`/api/patients/${toValue(patientId)}/consents`, {
        method: 'POST',
        body: {
          patientId: toValue(patientId),
          consentType: input.consentType,
          granted: input.granted,
          notes: input.notes?.trim() || null,
        },
        credentials: 'same-origin',
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['patients', 'consents', toValue(patientId) ?? ''] }),
  })
}

export function usePatientDisclosures(patientId: MaybeRefOrGetter<string | null>, enabled: MaybeRefOrGetter<boolean>) {
  return useQuery({
    queryKey: computed(() => ['patients', 'disclosures', toValue(patientId) ?? ''] as const),
    enabled: computed(() => !!toValue(patientId) && toValue(enabled)),
    queryFn: ({ signal }) =>
      $fetch<{ disclosures: PatientDisclosure[] }>(`/api/patients/${toValue(patientId)}/disclosures`, {
        credentials: 'same-origin',
        signal,
      }).then(x => x.disclosures),
  })
}

export function useRecordDisclosure(patientId: MaybeRefOrGetter<string | null>) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (input: { recipient: string, purpose: string, description?: string }) =>
      $fetch(`/api/patients/${toValue(patientId)}/disclosures`, {
        method: 'POST',
        body: {
          patientId: toValue(patientId),
          recipient: input.recipient.trim(),
          purpose: input.purpose.trim(),
          description: input.description?.trim() || null,
        },
        credentials: 'same-origin',
      }),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['patients', 'disclosures', toValue(patientId) ?? 0] }),
  })
}
