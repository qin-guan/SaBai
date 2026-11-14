import { toast } from 'vue-sonner'
import { isRecentVisit } from '~/utils/patient'
import { uuidv7 } from '~/utils/uuid'

/**
 * Starts a visit for a patient in the selected village (locally in PowerSync SQLite,
 * syncing automatically when online), then opens their vitals.
 */
export function useStartVisit() {
  const { selectedVillageCodeId, isLoading: villageLoading } = useVillageCode()
  const duplicate = ref<{ patientId: string, existingVisitDate: Date | null } | null>(null)
  const isChecking = ref(false)
  const creating = ref(false)

  const busy = computed(() => isChecking.value || creating.value)

  async function submit(patientId: string) {
    creating.value = true
    try {
      const db = getPowerSyncInstance()
      const visitId = uuidv7()
      const now = new Date().toISOString()
      await db.execute(
        `INSERT INTO visits (id, version, updated_at, patient_id, village_code_id, date, status)
         VALUES (?, 1, ?, ?, ?, ?, 'started')`,
        [visitId, now, patientId, selectedVillageCodeId.value!, now],
      )
      await refreshPendingCrudCount()
      duplicate.value = null
      await navigateTo(paths.vitalsDetail(patientId, visitId))
    } catch (error) {
      toast.error(error instanceof Error ? error.message : 'Failed to start a visit for this patient.')
    } finally {
      creating.value = false
    }
  }

  async function start(patientId: string) {
    if (!selectedVillageCodeId.value) {
      toast.error('Please select a village code before starting a visit.')
      return
    }

    isChecking.value = true
    try {
      const db = getPowerSyncInstance()
      const latest = await db.getOptional<{ date: string }>(
        'SELECT date FROM visits WHERE patient_id = ? ORDER BY date DESC LIMIT 1',
        [patientId],
      )
      if (latest?.date && isRecentVisit(latest.date)) {
        duplicate.value = {
          patientId,
          existingVisitDate: new Date(latest.date),
        }
        return
      }
    } catch {
      // Don't block visit creation if the duplicate check fails.
    } finally {
      isChecking.value = false
    }

    await submit(patientId)
  }

  return {
    start,
    busy,
    villageLoading,
    creating: readonly(creating),
    duplicate,
    confirm: () => duplicate.value && submit(duplicate.value.patientId),
    cancel: () => (duplicate.value = null),
  }
}
