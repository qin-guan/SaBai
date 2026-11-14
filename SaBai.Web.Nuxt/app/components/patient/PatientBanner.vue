<script setup lang="ts">
import type { PatientDetail } from '~/composables/usePatientData'
import { Badge } from '@/components/ui/badge'

/**
 * Patient banner ("storyboard"): who you are charting on, always at the top of
 * a patient's screens — photo, name, ID, age/sex, DOB and the drug allergy
 * flagged in red.
 */
const props = defineProps<{
  patient: PatientDetail
  /** Link the name here (the patient record, from station screens). */
  to?: string
}>()

const cards = computed(() =>
  [
    props.patient.hasPoorCard && 'POOR',
    props.patient.hasBS2Card && 'BS2',
    props.patient.hasSabaiCard && 'Sabai',
  ].filter(Boolean) as string[],
)
</script>

<template>
  <section aria-label="Patient" class="mb-3 border border-band-border bg-banner">
    <div class="flex flex-col gap-2 px-3 py-2 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-4">
    <div class="flex min-w-0 items-center gap-3 sm:flex-1">
    <PatientPhoto :patient-id="props.patient.id" :has-photo="props.patient.hasPhoto" :size="48" alt="" zoomable :name="props.patient.name" />
    <div class="min-w-0 flex-1">
      <div class="flex flex-wrap items-center gap-2">
        <p class="text-base font-bold leading-snug break-words sm:text-lg">
          <NuxtLink v-if="props.to" :to="props.to" class="hover:text-primary hover:underline" :title="`Open ${props.patient.name}'s record`">
            {{ props.patient.name }}
          </NuxtLink>
          <template v-else>
            {{ props.patient.name }}
          </template>
        </p>
        <Badge v-for="tag in props.patient.tags ?? []" :key="tag" variant="secondary">
          {{ tag }}
        </Badge>
      </div>
      <dl class="mt-0.5 flex flex-wrap gap-x-4 gap-y-0.5 text-xs touch:text-sm">
        <div class="flex gap-1">
          <dt class="text-muted-foreground">
            ID
          </dt>
          <dd class="font-mono font-semibold">
            {{ formatPatientCode(props.patient.villageCode, props.patient.id) }}
          </dd>
        </div>
        <div class="flex gap-1">
          <dt class="sr-only">
            Age and sex
          </dt>
          <dd>
            {{ calculateAge(props.patient.dateOfBirth) }} y · <span class="capitalize">{{ props.patient.gender }}</span>
          </dd>
        </div>
        <div class="flex gap-1">
          <dt class="text-muted-foreground">
            DOB
          </dt>
          <dd>{{ new Date(props.patient.dateOfBirth).toLocaleDateString() }}</dd>
        </div>
        <div v-if="props.patient.identificationNumber" class="flex gap-1">
          <dt class="text-muted-foreground">
            ID No.
          </dt>
          <dd>{{ props.patient.identificationNumber }}</dd>
        </div>
        <div v-if="cards.length" class="flex gap-1">
          <dt class="text-muted-foreground">
            Cards
          </dt>
          <dd>{{ cards.join(', ') }}</dd>
        </div>
      </dl>
    </div>
    </div>
    <div v-if="$slots.actions" class="flex flex-wrap gap-2 max-sm:[&>*]:flex-1">
      <slot name="actions" />
    </div>
    </div>
    <!-- Like a red allergy wristband: always attached to the patient, full width. -->
    <AllergyFlag :allergy="props.patient.drugAllergy" variant="full" />
  </section>
</template>
