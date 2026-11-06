<script setup lang="ts">
definePageMeta({ patientLink: 'dental' })
// Defaults to the most recent visit, which is where the dental station records.
const { patient, visits, selectedVisitId, selectedVisit, isLoading } = usePatientStation('patientId', 'latest')
</script>

<template>
  <StationPage
    v-model:visit-id="selectedVisitId"
    :station="{ label: 'Dental', to: paths.dental() }"
    title="Dental Consultation"
    :patient="patient"
    :visits="visits"
    :selected-visit="selectedVisit"
    :is-loading="isLoading"
    summary-prefix="Recording dental consultation for visit on"
    empty-message="No visits found for this patient. Please create a visit first to record a dental consultation."
    choose-message="Please choose a visit to record a dental consultation."
  >
    <template #default="{ visit }">
      <DentalForm :key="visit.id" :visit-id="visit.id" />
    </template>
  </StationPage>
</template>
