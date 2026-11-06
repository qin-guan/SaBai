<script setup lang="ts">
/**
 * Bordered section with a blue header band — the basic building block of every
 * screen (EHR "activity section" style).
 */
const props = defineProps<{
  title?: string
  description?: string
  /** Optional step number shown before the title. */
  step?: number
  id?: string
  /** Remove body padding (for tables). */
  flush?: boolean
}>()
const headingId = computed(() => (props.id ? `${props.id}-title` : undefined))
</script>

<template>
  <section :id="props.id" :aria-labelledby="headingId" class="scroll-mt-28 border border-border bg-card">
    <header
      v-if="props.title || $slots.actions"
      class="flex flex-wrap items-center gap-x-3 gap-y-1 border-b border-band-border bg-band px-3 py-1.5"
    >
      <h2 :id="headingId" class="flex items-center gap-2 text-xs font-bold uppercase tracking-wide text-chrome">
        <span
          v-if="props.step"
          class="inline-flex size-5 items-center justify-center bg-chrome text-[0.6875rem] text-white"
          aria-hidden="true"
        >{{ props.step }}</span>
        {{ props.title }}
      </h2>
      <p v-if="props.description" class="text-xs text-muted-foreground">
        {{ props.description }}
      </p>
      <div v-if="$slots.actions" class="ml-auto flex flex-wrap items-center gap-2">
        <slot name="actions" />
      </div>
    </header>
    <div :class="props.flush ? '' : 'p-3'">
      <slot />
    </div>
  </section>
</template>
