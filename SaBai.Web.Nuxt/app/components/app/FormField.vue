<script setup lang="ts">
import { PhWarningOctagon } from '@phosphor-icons/vue'
import { Label } from '@/components/ui/label'

/**
 * Label + control + hint/error. The default slot receives `id`,
 * `describedby` and `invalid` to wire into the control so the label and any
 * error are announced together.
 */
const props = defineProps<{
  label?: string
  required?: boolean
  error?: string | null
  hint?: string
  hideLabel?: boolean
  id?: string
}>()

const autoId = useId()
const fieldId = computed(() => props.id ?? `f-${autoId}`)
const describedby = computed(
  () =>
    [props.error && `${fieldId.value}-err`, props.hint && `${fieldId.value}-hint`]
      .filter(Boolean)
      .join(' ') || undefined,
)
</script>

<template>
  <div class="flex min-w-0 flex-col gap-1">
    <Label v-if="props.label" :for="fieldId" class="font-semibold" :class="props.hideLabel && 'sr-only'">
      {{ props.label }}
      <span v-if="props.required" class="text-destructive" aria-hidden="true">*</span>
      <span v-if="props.required" class="sr-only">(required)</span>
    </Label>
    <slot :id="fieldId" :describedby="describedby" :invalid="!!props.error" />
    <p v-if="props.hint && !props.error" :id="`${fieldId}-hint`" class="text-xs text-muted-foreground">
      {{ props.hint }}
    </p>
    <p v-if="props.error" :id="`${fieldId}-err`" class="flex items-start gap-1 text-xs font-medium text-destructive" role="alert">
      <PhWarningOctagon class="mt-px size-3.5 shrink-0" aria-hidden="true" />
      {{ props.error }}
    </p>
  </div>
</template>
