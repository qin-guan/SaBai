<script setup lang="ts">
import { Input } from '@/components/ui/input'

/**
 * Number input: decimal keypad on phones, exponent characters (e/E) blocked,
 * Khmer numerals (០–៩) converted to ASCII digits, and an optional unit
 * rendered inside the field (e.g. "cm", "mmHg").
 */
defineOptions({ inheritAttrs: false })
const model = defineModel<string>({ default: '' })
const props = defineProps<{ unit?: string, step?: string }>()

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'e' || event.key === 'E')
    event.preventDefault()
}

function onBeforeInput(event: InputEvent) {
  if (event.data && /[\u17E0-\u17E9]/.test(event.data)) {
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
  if (/[\u17E0-\u17E9]/.test(text)) {
    event.preventDefault()
    const normalized = normalizeKhmerDigits(cleanKhmerText(text))
    if (/^-?\d*(?:\.\d*)?$/.test(normalized))
      model.value = normalized
  }
}
</script>

<template>
  <div class="relative">
    <Input
      v-bind="$attrs"
      v-model="model"
      type="number"
      inputmode="decimal"
      :step="props.step ?? 'any'"
      class="tabular-nums"
      :class="props.unit && 'pr-14'"
      @keydown="onKeydown"
      @beforeinput="onBeforeInput"
      @paste="onPaste"
    />
    <span
      v-if="props.unit"
      class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 text-xs text-muted-foreground touch:text-sm"
      aria-hidden="true"
    >{{ props.unit }}</span>
  </div>
</template>
