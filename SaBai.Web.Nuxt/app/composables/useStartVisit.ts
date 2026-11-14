import { useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsVisitsHasRecentGetEndpoint } from '@sabai/sdk/clients'
import { newStartVisitVars, startVisit, type PatientDetail, type PatientVisit, type PatientWithLatestVisitItem } from '~/offline/mutations/patients'
import { isRecentVisit } from '~/utils/patient'

/**
 * Starts a visit for a patient in the selected village, then opens their
 * vitals. Works without a connection: the visit is saved on this device and
 * sent later. If the patient already had a visit opened recently, `duplicate`
 * is set instead and the caller asks for confirmation (`confirm()` / `cancel()`).
 */
export function useStartVisit() {
  const { selectedVillageCodeId, selectedVillage, isLoading: villageLoading } = useVillageCode()
  const queryClient = useQueryClient()
  const { mutateAsync, isPending } = useOfflineMutation(startVisit)
  const duplicate = ref<{ patientId: string, existingVisitDate: Date | null } | null>(null)
  const isChecking = ref(false)

  const busy = computed(() => isChecking.value || isPending.value)

  async function submit(patientId: string) {
    const village = selectedVillage.value
    if (!village) {
      toast.error('Please select a village code before starting a visit.')
      return
    }
    try {
      const vars = newStartVisitVars(patientId, patientNameOf(patientId), {
        villageCodeId: village.id,
        code: village.code,
        name: village.name,
        colorHex: village.colorHex,
      })
      notifySaved(await mutateAsync(vars), 'Visit started.')
      duplicate.value = null
      // Open vitals for the visit we just created, not whichever visit was cached.
      await navigateTo(paths.vitalsDetail(patientId, vars.id))
    }
    catch (error) {
      toast.error(getApiErrorMessage(error, 'Failed to start a visit for this patient.'))
    }
  }

  /** The cached name (shown in the pending-changes list and on the optimistic visit). */
  function patientNameOf(patientId: string): string {
    return queryClient.getQueryData<PatientDetail>(queryKeys.patient(patientId))?.name
      ?? queryClient.getQueryData<{ patients: PatientWithLatestVisitItem[] }>(queryKeys.patientsWithLatestVisit())
        ?.patients.find(p => p.id === patientId)?.name
      ?? ''
  }

  /** Latest visit date known on this device, including visits still waiting to be sent. */
  function cachedLatestVisitDate(patientId: string): string | null {
    const visits = queryClient.getQueryData<{ visits: PatientVisit[] }>(queryKeys.patientVisits(patientId))?.visits
    if (visits?.length)
      return visits.reduce((latest, v) => (new Date(v.date) > new Date(latest) ? v.date : latest), visits[0]!.date)
    return queryClient.getQueryData<{ patients: PatientWithLatestVisitItem[] }>(queryKeys.patientsWithLatestVisit())
      ?.patients.find(p => p.id === patientId)?.lastVisitDate ?? null
  }

  async function start(patientId: string) {
    if (!selectedVillageCodeId.value) {
      toast.error('Please select a village code before starting a visit.')
      return
    }

    isChecking.value = true
    let recent: { hasRecentVisit: boolean, existingVisitDate?: string | null } | null
    try {
      // Visits queued on this device are not on the server yet, so look at the cache first.
      const cached = cachedLatestVisitDate(patientId)
      recent = cached && isRecentVisit(cached)
        ? { hasRecentVisit: true, existingVisitDate: cached }
        : await saBaiAPIEndpointsVisitsHasRecentGetEndpoint({
          ...apiConfig,
          query: { patientId },
          throwOnError: true,
        }).unwrap()
    }
    catch {
      // Don't block visit creation if the duplicate check itself fails (e.g. offline).
      recent = null
    }
    finally {
      isChecking.value = false
    }

    if (recent?.hasRecentVisit) {
      duplicate.value = {
        patientId,
        existingVisitDate: recent.existingVisitDate ? new Date(recent.existingVisitDate) : null,
      }
      return
    }

    await submit(patientId)
  }

  return {
    start,
    busy,
    villageLoading,
    creating: isPending,
    duplicate,
    confirm: () => duplicate.value && submit(duplicate.value.patientId),
    cancel: () => (duplicate.value = null),
  }
}
