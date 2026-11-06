<script setup lang="ts">
import { PhClipboardText } from '@phosphor-icons/vue'

definePageMeta({ patientLink: 'vitals' })
useHead({ title: "Vitals · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader
      title="Vitals"
      description="Manage patient vital signs here."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Vitals' }]"
    />
    <PatientWorklist
      action-label="Vitals"
      :action-icon="PhClipboardText"
      :link-for="paths.vitalsDetail"
      empty-message="No patients found. Seed the database or add a new record."
    />
  </div>
</template>
