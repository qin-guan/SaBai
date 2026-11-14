<script setup lang="ts">
definePageMeta({ patientLink: 'consults' })
const { patient, visits, selectedVisitId, selectedVisit, isLoading } = usePatientStation('patientId', 'latest')

const route = useRoute()
const requestedConsultId = computed(() => {
  const raw = typeof route.query.consult === 'string' ? Number(route.query.consult) : Number.NaN
  return Number.isInteger(raw) && raw > 0 ? raw : null
})
const { data: requestedConsult, isLoading: isConsultLoading } = useConsult(requestedConsultId)

const appliedConsultId = ref<number | null>(null)
watch(
  [requestedConsult, visits],
  ([consult, visitList]) => {
    if (!consult || !visitList || appliedConsultId.value === consult.id)
      return
    const targetId = String(consult.visitId)
    if (visitList.some(v => v.id.toString() === targetId)) {
      selectedVisitId.value = targetId
      appliedConsultId.value = consult.id
    }
  },
  { immediate: true },
)
</script>

<template>
  <StationPage
    v-model:visit-id="selectedVisitId"
    station="consults"
    title="Consult"
    :patient="patient"
    :visits="visits"
    :selected-visit="selectedVisit"
    :is-loading="isLoading || (requestedConsultId !== null && isConsultLoading)"
    empty-message="No visits found for this patient. Please create a visit first to record a consult."
    choose-message="Please choose a visit to record a consult."
  >
    <template #default="{ visit, patient: p }">
      <div class="grid grid-cols-1 gap-3 lg:grid-cols-2 lg:items-start">
        <aside class="flex min-w-0 flex-col gap-3" aria-label="This visit's vitals, vision and previous consults">
          <VisitSummaryPanel :patient-id="p.id" :visit-id="visit.id" />
          <PreviousConsults :visit-id="visit.id" :highlight-consult-id="requestedConsultId" />
        </aside>
        <ConsultForm :key="visit.id" :visit-id="visit.id" />
      </div>
    </template>
  </StationPage>
</template>
