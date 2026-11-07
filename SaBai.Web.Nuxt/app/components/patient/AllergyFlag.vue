<script setup lang="ts">
import { PhWarning } from '@phosphor-icons/vue'

/**
 * Drug allergy flag.
 *
 * - `compact` (lists): one chip that never grows — the first allergy and a
 *   "+N" count, the whole list in the tooltip and for screen readers.
 * - `full` (patient banner): every allergy as its own chip under a counted
 *   heading. Nothing is hidden behind a click, since this is the safety check
 *   before anything is given; very long lists scroll inside the box.
 */
const props = withDefaults(defineProps<{ allergy: string | null | undefined, variant?: 'compact' | 'full' }>(), { variant: 'compact' })

const items = computed(() => parseDrugAllergies(props.allergy))
const summary = computed(() => `Drug ${items.value.length === 1 ? 'allergy' : 'allergies'}: ${items.value.join(', ')}`)
</script>

<template>
  <span
    v-if="props.variant === 'compact' && items.length"
    class="inline-flex max-w-full min-w-0 items-center gap-1 border border-destructive bg-destructive-subtle px-1 text-xs leading-5 font-semibold text-destructive"
    :title="summary"
  >
    <PhWarning class="size-3.5 shrink-0" weight="bold" aria-hidden="true" />
    <span class="sr-only">{{ summary }}</span>
    <span class="truncate" aria-hidden="true">{{ items[0] }}</span>
    <span v-if="items.length > 1" class="shrink-0 bg-destructive px-1 text-white" aria-hidden="true">+{{ items.length - 1 }}</span>
    </span>

  <div
    v-else-if="props.variant === 'full'"
    class="min-w-0 border px-2 py-1.5"
    :class="items.length ? 'border-destructive bg-destructive-subtle text-destructive' : 'border-border bg-card text-muted-foreground'"
    role="group"
    aria-label="Drug allergies"
  >
    <p class="flex items-center gap-1.5 text-xs font-bold touch:text-sm">
      <PhWarning v-if="items.length" class="size-4 shrink-0" weight="bold" aria-hidden="true" />
      <template v-if="items.length">
        {{ items.length === 1 ? 'Drug allergy' : `${items.length} drug allergies` }}
      </template>
      <template v-else>
        {{ props.allergy?.trim() ? 'No known drug allergies' : 'Drug allergies not recorded' }}
      </template>
    </p>
    <ul v-if="items.length" class="mt-1 flex max-h-24 flex-wrap gap-1 overflow-y-auto">
      <li
        v-for="item in items"
        :key="item"
        class="border border-destructive/50 bg-card px-1.5 text-xs leading-5 font-semibold touch:text-sm touch:leading-6"
      >
        {{ item }}
      </li>
    </ul>
  </div>
</template>
