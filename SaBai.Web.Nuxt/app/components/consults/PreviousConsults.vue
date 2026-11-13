<script setup lang="ts">
/**
 * Read-only consults already saved for this visit (a visit can accumulate
 * several), shown alongside the form for recording a new one.
 */
const props = defineProps<{ visitId: number }>()
const { data: consults, isLoading } = useVisitConsults(() => props.visitId)

const fields = [
  { key: 'pastMedicalHistory', label: 'Past Medical History' },
  { key: 'consultation', label: 'Consultation' },
] as const
const trailing = [
  { key: 'treatmentPlan', label: 'Plan' },
  { key: 'remarks', label: 'Remarks' },
] as const
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading previous consults..." />
  <AppPanel
    v-else-if="consults && consults.length > 0"
    :title="`Previous consults from this visit (${consults.length})`"
    description="Earlier notes for this visit"
    collapsible
    collapse-key="previous-consults"
    flush
  >
    <article v-for="consult in consults" :key="consult.id" class="flex flex-col gap-2 border-b border-border px-3 py-2 last:border-b-0">
      <div class="flex items-center justify-between gap-2">
        <div>
          <p class="text-xs font-semibold text-muted-foreground touch:text-sm">
            {{ formatVisitDate(consult.date) }}
          </p>
          <p v-if="consult.doctorNickname" class="text-xs text-muted-foreground">
            Dr. {{ consult.doctorNickname }}
          </p>
        </div>
        <ConsultPdfLink :consult-id="consult.id" size="sm" />
      </div>
      <template v-for="f in fields" :key="f.key">
        <div v-if="consult[f.key]">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ f.label }}
          </p>
          <p class="whitespace-pre-wrap break-words">
            {{ consult[f.key] }}
          </p>
        </div>
      </template>
      <div v-if="consult.diagnoses.length > 0">
        <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
          Diagnoses
        </p>
        <ul class="mt-0.5 flex flex-col gap-0.5">
          <li v-for="d in consult.diagnoses" :key="d.id" class="break-words">
            <span class="font-semibold">{{ d.category ?? '-' }}:</span> {{ d.details }}
          </li>
        </ul>
      </div>
      <div v-if="consult.referredFor" class="border border-band-border bg-accent/30 p-2 text-foreground">
        <p class="text-xs font-semibold text-chrome">
          Referred for: {{ consult.referredFor }}
        </p>
        <p v-if="consult.referralNotes" class="mt-0.5 text-xs text-muted-foreground whitespace-pre-wrap break-words">
          {{ consult.referralNotes }}
        </p>
      </div>
      <template v-for="f in trailing" :key="f.key">
        <div v-if="consult[f.key]">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ f.label }}
          </p>
          <p class="whitespace-pre-wrap break-words">
            {{ consult[f.key] }}
          </p>
        </div>
      </template>
    </article>
  </AppPanel>
</template>
