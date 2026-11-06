<script setup lang="ts">
/**
 * Form action bar. Sticks to the bottom of the viewport so the primary action
 * is always one tap away on phones, and shows the keyboard shortcut on desktop.
 */
</script>

<template>
  <div
    class="sticky bottom-0 z-10 mt-3 flex flex-wrap items-center justify-end gap-2 border border-border bg-card/95 px-3 py-2 pb-[max(0.5rem,env(safe-area-inset-bottom))] backdrop-blur"
  >
    <span class="mr-auto hidden text-xs text-muted-foreground [@media(pointer:fine)]:inline">
      <slot name="hint">
        <ShortcutHint keys="Ctrl+S" /> to save
      </slot>
    </span>
    <slot />
  </div>
</template>
