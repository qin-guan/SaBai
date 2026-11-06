<script setup lang="ts">
import { PhEyeglasses } from '@phosphor-icons/vue'

definePageMeta({ patientLink: 'vision' })
useHead({ title: "Vision · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader
      title="Vision"
      description="Manage vision prescriptions and eye care records for all patients."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Vision' }]"
    />
    <PatientWorklist
      action-label="Update Glasses"
      :action-icon="PhEyeglasses"
      :link-for="paths.visionUpdateGlasses"
      empty-message="No patients found. Add patients to manage their vision records."
    />
  </div>
</template>
