<script setup lang="ts">
import { PhWarning } from '@phosphor-icons/vue'
import type { SaBaiAPIEndpointsPatientsGetResponse } from '@sabai/sdk/types'

/**
 * Patient banner ("storyboard"): who you are charting on, always at the top of
 * a patient's screens — photo, name, ID, age/sex, DOB and the drug allergy
 * flagged in red.
 */
const props = defineProps<{ patient: SaBaiAPIEndpointsPatientsGetResponse }>()

const allergy = computed(() => props.patient.drugAllergy?.trim())
const hasAllergy = computed(
  () => !!allergy.value && !/^(nil|nkda|none|no|-|n\/a|na)$/i.test(allergy.value),
)
const cards = computed(() =>
  [
    props.patient.hasPoorCard && 'POOR',
    props.patient.hasBS2Card && 'BS2',
    props.patient.hasSabaiCard && 'Sabai',
  ].filter(Boolean) as string[],
)
</script>

<template>
  <section
    aria-label="Patient"
    class="mb-3 flex flex-col gap-2 border border-band-border bg-banner px-3 py-2 sm:flex-row sm:flex-wrap sm:items-center sm:gap-x-4"
  >
    <div class="flex min-w-0 items-center gap-3 sm:flex-1">
    <PatientPhoto :patient-id="props.patient.id" :has-photo="props.patient.hasPhoto" :size="56" alt="" />
    <div class="min-w-0 flex-1">
      <p class="text-base font-bold leading-tight break-words">
        {{ props.patient.name }}
      </p>
      <dl class="mt-0.5 flex flex-wrap gap-x-4 gap-y-0.5 text-xs touch:text-sm">
        <div class="flex gap-1">
          <dt class="text-muted-foreground">
            ID
          </dt>
          <dd class="font-mono font-semibold">
            {{ formatPatientId(props.patient.id) }}
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
    <div
      class="flex items-center gap-1.5 self-start border sm:self-auto px-2 py-1 text-xs font-semibold touch:text-sm"
      :class="hasAllergy ? 'border-destructive bg-destructive-subtle text-destructive' : 'border-border bg-card text-muted-foreground'"
    >
      <PhWarning v-if="hasAllergy" class="size-4" aria-hidden="true" />
      <span>Allergies: {{ allergy || 'Not recorded' }}</span>
    </div>
    <div v-if="$slots.actions" class="flex flex-wrap gap-2">
      <slot name="actions" />
    </div>
  </section>
</template>
