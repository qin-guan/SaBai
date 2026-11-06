<script setup lang="ts">
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/dialog'

/**
 * Modal dialog. Mount with v-if; it opens on mount and emits `close` on Esc,
 * the close button or an outside click. Focus is trapped and restored. On
 * phones it fills the screen with the footer pinned to the bottom.
 */
const props = withDefaults(
  defineProps<{ title: string, description?: string, size?: 'sm' | 'md' | 'lg' | 'xl' }>(),
  { size: 'md' },
)
const emit = defineEmits<{ close: [] }>()

const widths = { sm: 'sm:max-w-md', md: 'sm:max-w-xl', lg: 'sm:max-w-3xl', xl: 'sm:max-w-5xl' }
</script>

<template>
  <Dialog :open="true" @update:open="(open) => !open && emit('close')">
    <DialogContent
      class="flex h-dvh max-h-dvh max-w-full flex-col gap-0 p-0 sm:h-auto sm:max-h-[90dvh]"
      :class="widths[props.size]"
    >
      <DialogHeader class="shrink-0 border-b border-band-border bg-band px-4 py-2.5 pr-12 text-left">
        <DialogTitle class="text-sm font-bold text-chrome">
          {{ props.title }}
        </DialogTitle>
        <DialogDescription :class="props.description ? 'text-xs' : 'sr-only'">
          {{ props.description ?? 'Press Escape to close.' }}
        </DialogDescription>
      </DialogHeader>
      <div class="min-h-0 flex-1 overflow-y-auto p-4 text-sm">
        <slot />
      </div>
      <DialogFooter
        v-if="$slots.footer"
        class="shrink-0 flex-row flex-wrap justify-end gap-2 border-t border-border bg-stripe px-4 py-2 pb-[max(0.5rem,env(safe-area-inset-bottom))]"
      >
        <slot name="footer" />
      </DialogFooter>
    </DialogContent>
  </Dialog>
</template>
