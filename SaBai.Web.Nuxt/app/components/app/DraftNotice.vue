<script setup lang="ts">
import { PhArrowCounterClockwise, PhClockCounterClockwise } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'

/** Banner shown when a form was refilled from an unsaved local draft. */
const props = defineProps<{ restoredAt: Date | null }>()
const emit = defineEmits<{ discard: [] }>()
</script>

<template>
  <div
    v-if="props.restoredAt"
    role="status"
    class="mb-3 flex flex-wrap items-center gap-2 border border-warning-border bg-warning-subtle px-3 py-2 text-xs text-warning touch:text-sm"
  >
    <PhClockCounterClockwise class="size-4 shrink-0" aria-hidden="true" />
    <span class="flex-1">
      Restored unsaved changes from {{ props.restoredAt.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' }) }}.
      Review, then save.
    </span>
    <Button size="sm" variant="outline" @click="emit('discard')">
      <PhArrowCounterClockwise aria-hidden="true" /> Discard draft
    </Button>
  </div>
</template>
