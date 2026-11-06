<script setup lang="ts">
import { PhMagnifyingGlass } from '@phosphor-icons/vue'

/** Header search box: looks like an input, opens the universal search. */
const emit = defineEmits<{ open: [] }>()
</script>

<template>
  <button
    type="button"
    class="flex h-8 w-full max-w-xl cursor-pointer items-center gap-2 border border-input bg-card px-2.5 text-left text-xs text-muted-foreground hover:border-ring touch:h-11 touch:text-base"
    aria-haspopup="dialog"
    aria-keyshortcuts="/ Control+K Meta+K"
    @click="emit('open')"
  >
    <PhMagnifyingGlass class="size-4 shrink-0" aria-hidden="true" />
    <span class="min-w-0 flex-1 truncate">Search patients, sections…</span>
    <span class="hidden items-center gap-1 kb:flex" aria-hidden="true">
      <ShortcutHint keys="/" />
      <ShortcutHint keys="Ctrl+K" />
    </span>
  </button>
</template>
