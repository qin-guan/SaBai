<script setup lang="ts">
import { PhTooth } from '@phosphor-icons/vue'

definePageMeta({ patientLink: 'dental' })
useHead({ title: "Dental · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader
      title="Dental"
      description="Record dental consultations for patients at the dental station."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Dental' }]"
    />
    <PatientWorklist
      action-label="Dental Consultation"
      :action-icon="PhTooth"
      :link-for="paths.dentalDetail"
      empty-message="No patients found. Register patients to record dental consultations."
    />
  </div>
</template>
