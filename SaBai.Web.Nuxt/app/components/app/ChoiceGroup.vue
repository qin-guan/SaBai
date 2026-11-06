<script setup lang="ts">
import type { Option } from '~/utils/options'
import { ToggleGroup, ToggleGroupItem } from '@/components/ui/toggle-group'

/**
 * Single-choice button group (clinical "chip" answers). One tab stop; arrow
 * keys move between options, Space/Enter selects, selecting the chosen option
 * again clears it. Model is "" when unanswered.
 */
const model = defineModel<string>({ default: '' })
const props = defineProps<{
  options: readonly Option[]
  label?: string
  labelledby?: string
  invalid?: boolean
  size?: 'sm' | 'default'
}>()

const value = computed({
  get: () => model.value || undefined,
  set: (v: unknown) => {
    model.value = typeof v === 'string' ? v : ''
  },
})
</script>

<template>
  <ToggleGroup
    v-model="value"
    type="single"
    variant="outline"
    :size="props.size ?? 'sm'"
    :spacing="1"
    :aria-label="props.labelledby ? undefined : props.label"
    :aria-labelledby="props.labelledby"
    class="flex-wrap"
    :class="props.invalid && 'outline outline-1 outline-destructive'"
  >
    <ToggleGroupItem
      v-for="option in props.options"
      :key="option.value"
      :value="option.value"
      class="data-[state=on]:border-primary data-[state=on]:bg-primary data-[state=on]:text-primary-foreground"
    >
      {{ option.label }}
    </ToggleGroupItem>
  </ToggleGroup>
</template>
