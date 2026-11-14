<script setup lang="ts">
import { cn } from 'cn'

/**
 * Number input: decimal keypad on phones, exponent characters (e/E) blocked,
 * Khmer numerals (០–៩) converted to ASCII digits, and an optional unit
 * rendered inside the field (e.g. "cm", "mmHg").
 *
 * The model is always the exact text in the field (a string). This is a
 * native <input> on purpose: Vue's `v-model` on `type="number"` casts the
 * value to a JS number, which broke every caller that treats it as text
 * (string helpers threw mid-keystroke and the form stopped updating), and
 * rewrote in-progress input such as "1." or "0.0" under the cursor.
 *
 * The browser's spin buttons are hidden and the mouse wheel never changes the
 * value: in narrow fields with a unit the spinner sat where people click to
 * focus, so a click silently entered the step ("0.1") before typing, and
 * scrolling the page over a focused field changed a recorded reading.
 */
defineOptions({ inheritAttrs: false })
const model = defineModel<string>({ default: '' })
const props = defineProps<{ unit?: string, step?: string }>()

// `class` is merged into the field's own classes (so e.g. `h-11` replaces `h-8`); everything else goes on the <input>.
const attrs = useAttrs()
const inputAttrs = computed(() => {
  const { class: _class, ...rest } = attrs
  return rest
})

function onInput(event: Event) {
  model.value = (event.target as HTMLInputElement).value
}

/** Let the wheel scroll the page instead of stepping the focused value. */
function onWheel(event: WheelEvent) {
  const input = event.currentTarget as HTMLInputElement
  if (document.activeElement === input)
    input.blur()
}

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'e' || event.key === 'E')
    event.preventDefault()
}

function onBeforeInput(event: InputEvent) {
  if (event.data && /[០-៩]/.test(event.data)) {
    event.preventDefault()
    model.value = `${model.value ?? ''}${normalizeKhmerDigits(event.data)}`
  }
}

function onPaste(event: ClipboardEvent) {
  const text = event.clipboardData?.getData('text') ?? ''
  if (/[eE]/.test(text)) {
    event.preventDefault()
    return
  }
  if (/[០-៩]/.test(text)) {
    event.preventDefault()
    const normalized = normalizeKhmerDigits(cleanKhmerText(text))
    if (/^-?\d*(?:\.\d*)?$/.test(normalized))
      model.value = normalized
  }
}
</script>

<template>
  <div class="relative">
    <input
      v-bind="inputAttrs"
      :value="model"
      type="number"
      inputmode="decimal"
      :step="props.step ?? 'any'"
      data-slot="input"
      :class="cn(
        'dark:bg-input/30 border-input focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive dark:aria-invalid:border-destructive/50 disabled:bg-input/50 dark:disabled:bg-input/80 h-8 rounded-none border bg-transparent px-2.5 py-1 text-xs transition-colors focus-visible:ring-1 aria-invalid:ring-1 md:text-xs w-full min-w-0 outline-none placeholder:text-muted-foreground disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 tabular-nums [appearance:textfield] [&::-webkit-inner-spin-button]:appearance-none [&::-webkit-inner-spin-button]:m-0 [&::-webkit-outer-spin-button]:appearance-none [&::-webkit-outer-spin-button]:m-0',
        props.unit && 'pr-14',
        attrs.class as string,
      )"
      @input="onInput"
      @wheel="onWheel"
      @keydown="onKeydown"
      @beforeinput="onBeforeInput"
      @paste="onPaste"
    >
    <span
      v-if="props.unit"
      class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 text-xs text-muted-foreground touch:text-sm"
      aria-hidden="true"
    >{{ props.unit }}</span>
  </div>
</template>
