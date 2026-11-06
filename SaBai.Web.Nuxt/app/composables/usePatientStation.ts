/**
 * Shared state for a per-patient station page (vitals, vision, consults,
 * dental): the patient, their visits and the selected visit.
 *
 * `autoSelect`:
 *  - "single": pick the visit automatically only when there is exactly one.
 *  - "latest": default to the most recent visit, so scanning a face and
 *    starting a visit lands on the new visit even if the patient has several
 *    (all stations).
 */
export function usePatientStation(param: string, autoSelect: 'single' | 'latest') {
  const patientId = useRouteId(param)
  const patientQuery = usePatient(patientId)
  const visitsQuery = usePatientVisits(patientId)
  useRememberPatient(patientId)

  const visits = computed(() => visitsQuery.data.value?.visits)
  // `?visit=<id>` deep-links to a specific visit (e.g. from the patient page).
  const route = useRoute()
  const requestedVisit = typeof route.query.visit === 'string' ? route.query.visit : ''
  const selectedVisitId = ref(requestedVisit)

  watch(
    visits,
    (list) => {
      if (!list || list.length === 0)
        return
      if (selectedVisitId.value) {
        // Ignore a deep-linked visit that doesn't belong to this patient.
        if (list.some(v => v.id.toString() === selectedVisitId.value))
          return
        selectedVisitId.value = ''
      }
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
