<script setup lang="ts">
import { PhCheck, PhQuestion, PhWarning } from '@phosphor-icons/vue'

/**
 * Drug allergy flag, styled after a hospital's red allergy wristband: one
 * solid red signal with plain white text, so it reads at a glance and never
 * looks like the rest of the screen.
 *
 * - `compact` (lists): a small red tag with the first allergy and a "+N"
 *   count; the whole list is in the tooltip and read to screen readers.
 * - `full` (bottom of the patient banner): a full-width band with every
 *   allergy as a plain comma-separated list. Nothing is hidden behind a click.
 *   No allergies is a quiet neutral line; never recorded is amber, because
 *   someone should ask.
 */
const props = withDefaults(defineProps<{ allergy: string | null | undefined, variant?: 'compact' | 'full' }>(), { variant: 'compact' })

const items = computed(() => parseDrugAllergies(props.allergy))
const recorded = computed(() => !!props.allergy?.trim())
const heading = computed(() => (items.value.length === 1 ? 'Drug allergy' : `${items.value.length} drug allergies`))
const summary = computed(() => `${heading.value}: ${items.value.join(', ')}`)
</script>

<template>
  <span
    v-if="props.variant === 'compact' && items.length"
    class="inline-flex max-w-full min-w-0 items-center gap-1 bg-destructive pr-1.5 pl-1 text-xs leading-5 font-semibold text-white"
    :title="summary"
  >
    <PhWarning class="size-3.5 shrink-0" weight="fill" aria-hidden="true" />
    <span class="sr-only">{{ summary }}</span>
    <span class="truncate" aria-hidden="true">{{ items[0] }}</span>
    <span v-if="items.length > 1" class="shrink-0 font-normal text-white/80" aria-hidden="true">+{{ items.length - 1 }}</span>
  </span>

  <div
    v-else-if="props.variant === 'full' && items.length"
    role="group"
    :aria-label="heading"
    class="grid grid-cols-1 gap-x-4 gap-y-0.5 bg-destructive px-3 py-1.5 text-white sm:grid-cols-[auto_minmax(0,1fr)] sm:items-baseline"
  >
    <p class="text-sm leading-snug whitespace-nowrap text-white/85">
      <PhWarning class="mr-1.5 inline size-4 -translate-y-px align-middle text-white" weight="fill" aria-hidden="true" />{{ heading }}
    </p>
    <p class="text-sm leading-snug font-bold">
      <template v-for="(item, index) in items" :key="item">
        <span class="whitespace-nowrap">{{ item }}</span><span v-if="index < items.length - 1" class="font-normal text-white/70">, </span>
      </template>
    </p>
  </div>

  <p
    v-else-if="props.variant === 'full'"
    class="flex items-center gap-1.5 border-t px-3 py-1 text-xs touch:text-sm"
    :class="recorded ? 'border-band-border text-muted-foreground' : 'border-warning-border bg-warning-subtle font-semibold text-warning'"
  >
    <component :is="recorded ? PhCheck : PhQuestion" class="size-3.5 shrink-0" weight="bold" aria-hidden="true" />
    {{ recorded ? 'No known drug allergies' : 'Drug allergies not recorded — ask the patient and add them to their details' }}
  </p>
</template>
