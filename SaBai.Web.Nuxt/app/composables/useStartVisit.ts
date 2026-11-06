import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import {
  saBaiAPIEndpointsVisitsCreatePostEndpoint,
  saBaiAPIEndpointsVisitsHasRecentGetEndpoint,
} from '@sabai/sdk/clients'

/**
 * Starts a visit for a patient in the selected village, then opens their
 * vitals. If the patient already had a visit opened recently, `duplicate` is
 * set instead and the caller asks for confirmation (`confirm()` / `cancel()`).
 */
export function useStartVisit() {
  const { selectedVillageCodeId, isLoading: villageLoading } = useVillageCode()
  const queryClient = useQueryClient()
  const duplicate = ref<{ patientId: number, existingVisitDate: Date | null } | null>(null)
  const isChecking = ref(false)

  const create = useMutation({
    mutationFn: (patientId: number) =>
      saBaiAPIEndpointsVisitsCreatePostEndpoint({
        ...apiConfig,
        body: { patientId, villageCodeId: selectedVillageCodeId.value! },
        throwOnError: true,
      }).unwrap(),
  })

  const busy = computed(() => isChecking.value || create.isPending.value)

  async function submit(patientId: number) {
    try {
      const visit = await create.mutateAsync(patientId)
      duplicate.value = null
      // Refresh visit lists/counters so the new visit is visible everywhere.
      await queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
      // Open vitals for the visit we just created, not whichever visit was cached.
      await navigateTo(paths.vitalsDetail(patientId, visit.id))
    }
    catch (error) {
      toast.error(getApiErrorMessage(error, 'Failed to start a visit for this patient.'))
    }
  }

  async function start(patientId: number) {
    if (!selectedVillageCodeId.value) {
      toast.error('Please select a village code before starting a visit.')
      return
    }

    isChecking.value = true
    let recent: { hasRecentVisit: boolean, existingVisitDate?: string | null } | null
    try {
      recent = await saBaiAPIEndpointsVisitsHasRecentGetEndpoint({
        ...apiConfig,
        query: { patientId },
        throwOnError: true,
      }).unwrap()
    }
    catch {
      // Don't block visit creation if the duplicate check itself fails.
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
    creating: create.isPending,
    duplicate,
    confirm: () => duplicate.value && submit(duplicate.value.patientId),
    cancel: () => (duplicate.value = null),
  }
}
