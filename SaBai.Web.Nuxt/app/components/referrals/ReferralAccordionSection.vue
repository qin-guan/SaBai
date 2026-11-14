<script setup lang="ts">
import { PhCaretDown } from '@phosphor-icons/vue'

const props = withDefaults(
  defineProps<{
    title: string
    defaultOpen?: boolean
  }>(),
  {
    defaultOpen: false,
  },
)

const isOpen = ref(props.defaultOpen)
const uid = useId()
const buttonId = `${uid}-button`
const panelId = `${uid}-panel`
</script>

<template>
  <section>
    <h2>
      <button
        :id="buttonId"
        type="button"
        :aria-expanded="isOpen"
        :aria-controls="panelId"
        class="flex w-full items-center justify-between gap-4 border-l-4 px-4 py-3 text-left text-sm font-bold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-inset focus-visible:ring-ring touch:py-3.5"
        :class="
          isOpen
            ? 'border-chrome bg-band/80 text-chrome hover:bg-band'
            : 'border-transparent bg-muted/40 text-foreground hover:bg-muted/70'
        "
        @click="isOpen = !isOpen"
      >
        <span>{{ title }}</span>
        <PhCaretDown
          aria-hidden="true"
          class="size-4 shrink-0 transition-transform duration-150"
          :class="isOpen ? 'rotate-180 text-chrome' : 'text-muted-foreground'"
        />
      </button>
    </h2>
    <div
      :id="panelId"
      role="region"
      :aria-labelledby="buttonId"
      :hidden="!isOpen"
      class="border-t border-border bg-card px-4 py-4"
    >
      <slot />
    </div>
  </section>
</template>
