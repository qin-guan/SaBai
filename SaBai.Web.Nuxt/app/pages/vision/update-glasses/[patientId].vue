<script setup lang="ts">
definePageMeta({ patientLink: 'vision' })
const { patient, visits, selectedVisitId, selectedVisit, isLoading } = usePatientStation('patientId', 'single')
</script>

<template>
  <StationPage
    v-model:visit-id="selectedVisitId"
    :station="{ label: 'Vision', to: paths.vision() }"
    title="Update Glasses"
    :patient="patient"
    :visits="visits"
    :selected-visit="selectedVisit"
    :is-loading="isLoading"
    summary-prefix="Filling up vision form for visit on"
    empty-message="No visits found for this patient. Please create a visit first to update glasses prescription."
    choose-message="Please choose a visit to view vision data."
  >
    <template #default="{ visit }">
      <!-- Edits the full eyesight record, so it uses the eyesight endpoints. -->
      <EyesightForm :key="visit.id" :visit-id="visit.id" />
    </template>
  </StationPage>
</template>
