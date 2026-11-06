<script setup lang="ts">
import type { PatientLinkTarget } from '~/utils/paths'

/**
 * Jump-to links for one patient: record, vitals, vision, consults, dental.
 * Stations open on the patient's latest visit. `active` highlights the
 * station the user is currently working in.
 */
const props = defineProps<{ patientId: number, active?: PatientLinkTarget }>()
const emit = defineEmits<{ navigate: [target: PatientLinkTarget] }>()

const LINKS: { label: string, target: PatientLinkTarget }[] = [
  { label: 'Record', target: 'patient' },
  { label: 'Vitals', target: 'vitals' },
  { label: 'Vision', target: 'vision' },
  { label: 'Consult', target: 'consults' },
  { label: 'Dental', target: 'dental' },
]
</script>

<template>
  <nav class="flex flex-wrap gap-1" aria-label="Open for this patient">
    <NuxtLink
      v-for="link in LINKS"
      :key="link.target"
      :to="patientLinkFor(link.target, props.patientId)"
      class="border border-border bg-card px-2 py-0.5 text-xs font-semibold hover:border-primary hover:text-primary touch:px-3 touch:py-1.5 touch:text-sm"
      :class="props.active === link.target && 'border-primary text-primary'"
      @click="emit('navigate', link.target)"
    >
      {{ link.label }}
    </NuxtLink>
  </nav>
</template>
