<script setup lang="ts">
import { Input } from '@/components/ui/input'

/**
 * Number input: decimal keypad on phones, exponent characters (e/E) blocked,
 * and an optional unit rendered inside the field (e.g. "cm", "mmHg").
 */
defineOptions({ inheritAttrs: false })
const model = defineModel<string>({ default: '' })
const props = defineProps<{ unit?: string, step?: string }>()

function onKeydown(event: KeyboardEvent) {
  if (event.key === 'e' || event.key === 'E')
    event.preventDefault()
}
function onPaste(event: ClipboardEvent) {
  if (/[eE]/.test(event.clipboardData?.getData('text') ?? ''))
    event.preventDefault()
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
      @paste="onPaste"
    />
    <span
      v-if="props.unit"
      class="pointer-events-none absolute top-1/2 right-2.5 -translate-y-1/2 text-xs text-muted-foreground"
      aria-hidden="true"
    >{{ props.unit }}</span>
  </div>
</template>
