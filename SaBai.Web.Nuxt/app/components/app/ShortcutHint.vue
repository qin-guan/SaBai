<script setup lang="ts">
import { Kbd, KbdGroup } from '@/components/ui/kbd'

/** Renders a key combination like "Ctrl+S" as keycaps (⌘ on Apple devices). */
const props = defineProps<{ keys: string }>()
const parts = computed(() =>
  props.keys.split('+').map(k => (k === 'Ctrl' && isMac() ? '⌘' : k)),
)
</script>

<template>
  <KbdGroup>
    <Kbd v-for="key in parts" :key="key">
      {{ key }}
    </Kbd>
  </KbdGroup>
</template>
