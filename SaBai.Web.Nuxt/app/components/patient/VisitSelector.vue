<script setup lang="ts">
import type { SaBaiAPIEndpointsPatientsVisitsGetItem } from '@sabai/sdk/types'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/** Visit picker shown above every station form. */
const model = defineModel<string>({ required: true })
const props = defineProps<{
  visits: SaBaiAPIEndpointsPatientsVisitsGetItem[] | undefined
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
    <div v-else class="flex flex-col gap-2 sm:flex-row sm:items-center sm:gap-4">
      <label :for="id" class="text-xs font-semibold whitespace-nowrap touch:text-sm">Choose visit</label>
      <div class="w-full sm:max-w-xs">
        <NativeSelect :id="id" v-model="model">
          <NativeSelectOption value="">
            Select a visit
          </NativeSelectOption>
          <NativeSelectOption v-for="visit in props.visits" :key="visit.id" :value="visit.id.toString()">
            {{ formatVisitDate(visit.date) }} — {{ visit.villageName }}
          </NativeSelectOption>
        </NativeSelect>
      </div>
      <p v-if="props.summaryPrefix" class="text-xs text-muted-foreground touch:text-sm" aria-live="polite">
        {{ props.summaryPrefix }}
        <span class="font-semibold text-foreground">{{ selected ? formatVisitDate(selected.date) : '-' }}</span>
      </p>
    </div>
  </div>
</template>
