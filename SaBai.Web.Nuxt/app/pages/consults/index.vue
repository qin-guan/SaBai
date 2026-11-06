<script setup lang="ts">
import { PhStethoscope } from '@phosphor-icons/vue'

definePageMeta({ patientLink: 'consults' })
useHead({ title: "Consults · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader
      title="Consults"
      description="Select a patient to record a consultation."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Consults' }]"
    />
    <PatientWorklist
      action-label="Start Consult"
      :action-icon="PhStethoscope"
      :link-for="paths.consultsDetail"
      empty-message="No patients found. Add patients to record consults."
    />
  </div>
</template>
