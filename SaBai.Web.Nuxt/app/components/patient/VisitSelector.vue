<script setup lang="ts">
import type { LocalVisitSummary } from '~/db/types'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/** Visit picker shown above every station form. */
const model = defineModel<string>({ required: true })
const props = defineProps<{
  visits: LocalVisitSummary[] | undefined
  /** e.g. "Filling up vitals form for visit on" */
  summaryPrefix?: string
  /** Shown when the patient has no visits. */
  emptyMessage: string
}>()
const id = useId()
const selected = computed(() => props.visits?.find(v => v.id.toString() === model.value))
</script>

<template>
  <div class="mb-3 border border-border bg-card px-3 py-2">
    <StateMessage v-if="!props.visits || props.visits.length === 0" kind="warning" :message="props.emptyMessage" />
    <div v-else class="flex flex-col gap-2 lg:flex-row lg:items-center lg:gap-4">
      <div class="flex min-w-0 flex-col gap-1 sm:flex-row sm:items-center sm:gap-3">
        <label :for="id" class="text-xs font-semibold whitespace-nowrap touch:text-sm">Visit</label>
        <div class="w-full sm:w-72">
          <NativeSelect :id="id" v-model="model">
            <NativeSelectOption value="">
              Select a visit
            </NativeSelectOption>
            <NativeSelectOption v-for="visit in props.visits" :key="visit.id" :value="visit.id.toString()">
              #{{ props.visits.length - props.visits.indexOf(visit) }} · {{ formatVisitDate(visit.date) }} · {{ visit.villageName }}
            </NativeSelectOption>
          </NativeSelect>
        </div>
      </div>
      <!-- The visit's journey sits beside the picker (one strip instead of two). -->
      <div v-if="$slots.default" class="min-w-0 border-border lg:flex-1 lg:border-l lg:pl-4">
        <slot />
      </div>
      <p v-if="props.summaryPrefix" class="sr-only" aria-live="polite">
        {{ props.summaryPrefix }} {{ selected ? formatVisitDate(selected.date) : '-' }}
      </p>
    </div>
  </div>
</template>
