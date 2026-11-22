<script setup lang="ts">
import { PhWarningOctagon } from '@phosphor-icons/vue'

/** A server's refusal, shown inside the form or dialog it belongs to (announced at once). */
const props = defineProps<{ message: string }>()
</script>

<template>
  <div
    class="flex items-start gap-1.5 border border-destructive/40 bg-destructive-subtle px-2 py-1.5 text-xs font-medium text-destructive touch:text-sm"
    role="alert"
    data-form-error
  >
    <PhWarningOctagon class="mt-px size-3.5 shrink-0" aria-hidden="true" />
    <span>{{ props.message }}</span>
  </div>
</template>
