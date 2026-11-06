<script setup lang="ts">
definePageMeta({ patientLink: 'vitals' })
const { patient, visits, selectedVisitId, selectedVisit, isLoading } = usePatientStation('id', 'latest')
</script>

<template>
  <StationPage
    v-model:visit-id="selectedVisitId"
    :station="{ label: 'Vitals', to: paths.vitals() }"
    title="Patient Vitals"
    allow-edit
    :patient="patient"
    :visits="visits"
    :selected-visit="selectedVisit"
    :is-loading="isLoading"
    summary-prefix="Filling up vitals form for visit on"
    empty-message="No visits found for patient."
    choose-message="Please choose a visit to view vitals."
  >
    <template #default="{ visit, patient: p }">
      <VitalsForm :key="visit.id" :visit-id="visit.id" :visit-date="visit.date" :patient="p" />
    </template>
  </StationPage>
</template>
