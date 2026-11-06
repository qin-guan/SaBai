<script setup lang="ts">
import { PhCircleNotch, PhInfo, PhWarning, PhWarningOctagon } from '@phosphor-icons/vue'

/** Loading / empty / error / warning message block, announced to assistive tech. */
const props = withDefaults(
  defineProps<{ kind?: 'loading' | 'empty' | 'error' | 'warning', message: string }>(),
  { kind: 'empty' },
)

const icon = computed(
  () => ({ loading: PhCircleNotch, empty: PhInfo, error: PhWarningOctagon, warning: PhWarning })[props.kind],
)
</script>

<template>
  <div
    :role="props.kind === 'error' ? 'alert' : 'status'"
    class="flex items-start gap-2 px-3 py-3 text-xs touch:text-sm"
    :class="{
      'text-muted-foreground': props.kind === 'loading' || props.kind === 'empty',
      'border border-destructive/40 bg-destructive-subtle text-destructive': props.kind === 'error',
      'border border-warning-border bg-warning-subtle text-warning': props.kind === 'warning',
    }"
    :data-testid="props.kind === 'loading' ? 'loading-spinner' : undefined"
  >
    <component
      :is="icon"
      class="mt-px size-4 shrink-0"
      :class="props.kind === 'loading' && 'animate-spin'"
      aria-hidden="true"
    />
    <span>{{ props.message }}</span>
  </div>
</template>
