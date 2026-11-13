<script setup lang="ts">
import { PhCaretRight } from '@phosphor-icons/vue'

/**
 * Bordered section with a blue header band — the basic building block of every
 * screen (EHR "activity section" style).
 *
 * `collapsible` panels hold reference or good-to-have content (history, photo,
 * growth chart, optional notes). The header becomes a toggle and the body is
 * hidden (not unmounted, so typed input survives) until opened; the header's
 * description stays visible as a one-line summary. With `collapseKey` the
 * choice is remembered on this device.
 */
const props = withDefaults(defineProps<{
  title?: string
  description?: string
  /** Optional step number shown before the title. */
  step?: number
  id?: string
  /** Remove body padding (for tables). */
  flush?: boolean
  collapsible?: boolean
  /** Whether a collapsible panel starts open. */
  defaultOpen?: boolean
  /** Remember the open/closed choice on this device under this key. */
  collapseKey?: string
  /** Enable two-tier header layout (row 1: title + headerActions, row 2: description). */
  twoTier?: boolean
}>(), { defaultOpen: false })

const autoId = useId()
const headingId = computed(() => `${props.id ?? `panel-${autoId}`}-title`)
const bodyId = computed(() => `${props.id ?? `panel-${autoId}`}-body`)

const storageKey = computed(() => (props.collapseKey ? `sabai.panel.${props.collapseKey}` : null))
function readStored(): boolean | null {
  if (!storageKey.value)
    return null
  try {
    const value = localStorage.getItem(storageKey.value)
    return value === null ? null : value === '1'
  }
  catch {
    return null
  }
}
const open = ref(!props.collapsible || (readStored() ?? props.defaultOpen))

// Content that arrives later (e.g. saved remarks) can ask a closed panel to open.
watch(() => props.defaultOpen, (value) => {
  if (value && props.collapsible && readStored() === null)
    open.value = true
})

function toggle() {
  open.value = !open.value
  if (storageKey.value) {
    try {
      localStorage.setItem(storageKey.value, open.value ? '1' : '0')
    }
    catch {}
  }
}
</script>

<template>
  <section :id="props.id" :aria-labelledby="headingId" class="scroll-mt-28 border border-border bg-card">
    <header
      v-if="props.title || $slots.title || $slots.actions || $slots.headerActions"
      class="bg-band px-3 py-1.5"
      :class="open && 'border-b border-band-border'"
    >
      <!-- Two-tier layout: used when twoTier prop is true or #headerActions slot is provided -->
      <div v-if="props.twoTier || $slots.headerActions" class="flex flex-col gap-1">
        <div class="flex items-center justify-between gap-3">
          <h2
            v-if="props.title || $slots.title"
            :id="headingId"
            class="flex min-w-0 shrink-0 items-center gap-2 text-[0.8125rem] font-bold text-chrome touch:text-sm"
          >
            <component
              :is="props.collapsible ? 'button' : 'span'"
              :type="props.collapsible ? 'button' : undefined"
              class="flex items-center gap-2 text-left"
              :class="props.collapsible && 'cursor-pointer hover:underline touch:min-h-11'"
              :aria-expanded="props.collapsible ? open : undefined"
              :aria-controls="props.collapsible ? bodyId : undefined"
              @click="props.collapsible && toggle()"
            >
              <PhCaretRight
                v-if="props.collapsible"
                weight="bold"
                class="size-3.5 shrink-0 transition-transform motion-reduce:transition-none"
                :class="open && 'rotate-90'"
                aria-hidden="true"
              />
              <span
                v-if="props.step"
                class="inline-flex size-5 shrink-0 items-center justify-center bg-chrome text-xs text-white"
                aria-hidden="true"
              >{{ props.step }}</span>
              <slot name="title">
                <span class="whitespace-nowrap">{{ props.title }}</span>
              </slot>
            </component>
          </h2>
          <div v-if="$slots.headerActions" class="flex shrink-0 items-center gap-2">
            <slot name="headerActions" />
          </div>
        </div>
        <div
          v-if="props.description || $slots.description"
          class="text-xs text-muted-foreground touch:text-sm"
          :class="[
            props.collapsible && 'cursor-pointer',
            props.collapsible ? 'pl-[22px]' : props.step ? 'pl-7' : 'pl-0'
          ]"
          @click="props.collapsible && toggle()"
        >
          <slot name="description">
            <p>{{ props.description }}</p>
          </slot>
        </div>
        <div v-if="$slots.actions && open" class="mt-1 flex flex-wrap items-center gap-2 max-sm:has-[>.w-full]:w-full">
          <slot name="actions" />
        </div>
      </div>

      <!-- Standard single-row / wrapping layout for standard panels -->
      <div v-else class="flex flex-wrap items-center justify-between gap-x-3 gap-y-1.5">
        <div v-if="props.title || $slots.title || props.description || $slots.description" class="flex min-w-0 flex-1 flex-wrap items-center gap-x-3 gap-y-1">
          <h2
            v-if="props.title || $slots.title"
            :id="headingId"
            class="flex shrink-0 items-center gap-2 text-[0.8125rem] font-bold text-chrome touch:text-sm"
          >
            <component
              :is="props.collapsible ? 'button' : 'span'"
              :type="props.collapsible ? 'button' : undefined"
              class="flex items-center gap-2 text-left"
              :class="props.collapsible && 'cursor-pointer hover:underline touch:min-h-11'"
              :aria-expanded="props.collapsible ? open : undefined"
              :aria-controls="props.collapsible ? bodyId : undefined"
              @click="props.collapsible && toggle()"
            >
              <PhCaretRight
                v-if="props.collapsible"
                weight="bold"
                class="size-3.5 shrink-0 transition-transform motion-reduce:transition-none"
                :class="open && 'rotate-90'"
                aria-hidden="true"
              />
              <span
                v-if="props.step"
                class="inline-flex size-5 shrink-0 items-center justify-center bg-chrome text-xs text-white"
                aria-hidden="true"
              >{{ props.step }}</span>
              <slot name="title">
                <span class="whitespace-nowrap">{{ props.title }}</span>
              </slot>
            </component>
          </h2>
          <div v-if="props.description || $slots.description" class="min-w-0 text-xs text-muted-foreground touch:text-sm">
            <slot name="description">
              <p>{{ props.description }}</p>
            </slot>
          </div>
        </div>
        <div v-if="$slots.actions && open" class="flex flex-wrap items-center gap-2 max-sm:has-[>.w-full]:w-full">
          <slot name="actions" />
        </div>
      </div>
    </header>
    <div v-show="open" :id="bodyId" :class="props.flush ? '' : 'p-3'">
      <slot />
    </div>
  </section>
</template>
