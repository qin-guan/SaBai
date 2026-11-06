/**
 * Shared state for a per-patient station page (vitals, vision, consults,
 * dental): the patient, their visits and the selected visit.
 *
 * `autoSelect`:
 *  - "single": pick the visit automatically only when there is exactly one
 *    (vitals, vision, consults — same as SaBai.Web).
 *  - "latest": default to the most recent visit (dental).
 */
export function usePatientStation(param: string, autoSelect: 'single' | 'latest') {
  const patientId = useRouteId(param)
  const patientQuery = usePatient(patientId)
  const visitsQuery = usePatientVisits(patientId)
  useRememberPatient(patientId)

  const visits = computed(() => visitsQuery.data.value?.visits)
  const selectedVisitId = ref('')

  watch(
    visits,
    (list) => {
      if (!list || list.length === 0 || selectedVisitId.value)
        return
      if (autoSelect === 'single' && list.length === 1) {
        selectedVisitId.value = list[0]!.id.toString()
      }
      else if (autoSelect === 'latest') {
        const latest = [...list].sort(
          (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime(),
        )[0]!
        selectedVisitId.value = latest.id.toString()
      }
    },
    { immediate: true },
  )

  const selectedVisit = computed(() =>
    selectedVisitId.value
      ? (visits.value?.find(v => v.id.toString() === selectedVisitId.value) ?? null)
      : null,
  )

  const isLoading = computed(
    () => !patientId.value || patientQuery.isLoading.value || visitsQuery.isLoading.value,
  )

  return {
    patientId,
    patient: patientQuery.data,
    visits,
    selectedVisitId,
    selectedVisit,
    isLoading,
  }
}
