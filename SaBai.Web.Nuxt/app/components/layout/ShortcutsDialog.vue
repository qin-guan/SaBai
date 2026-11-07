<script setup lang="ts">
const emit = defineEmits<{ close: [] }>()

const groups = [
  {
    title: 'Global',
    items: [
      { keys: 'Ctrl+K  or  /', label: 'Search patients, sections and actions' },
      { keys: 'F', label: 'Find a patient by face scan (opens the current station)' },
      { keys: 'Ctrl+B', label: 'Collapse / expand sidebar' },
      { keys: '?', label: 'Show this help' },
    ],
  },
  {
    title: 'Search',
    items: [
      { keys: '↑ / ↓', label: 'Move through results' },
      { keys: 'Enter', label: 'Open (patients open in the current station)' },
      { keys: 'Shift+Enter', label: 'Open the patient record' },
      { keys: 'Esc', label: 'Close' },
    ],
  },
  {
    title: 'Scan Face',
    items: [
      { keys: 'T', label: 'Open the camera' },
      { keys: 'C', label: 'Capture the photo' },
      { keys: 'R', label: 'Retake the photo' },
      { keys: 'N', label: 'Register a new patient' },
      { keys: 'M', label: 'Back to matching (from registration)' },
    ],
  },
  {
    title: 'Forms',
    items: [
      { keys: 'Ctrl+S', label: 'Save the current form' },
      { keys: 'Enter', label: 'Next field (never submits by accident)' },
      { keys: 'Tab / Shift+Tab', label: 'Next / previous field' },
      { keys: '← / →', label: 'Change answer in a choice group' },
      { keys: 'Esc', label: 'Close dialog' },
    ],
  },
]
</script>

<template>
  <AppDialog title="Keyboard shortcuts" size="md" @close="emit('close')">
    <div class="grid gap-4 sm:grid-cols-2">
      <section v-for="group in groups" :key="group.title">
        <h3 class="mb-1 border-b border-border pb-1 text-xs font-bold text-chrome">
          {{ group.title }}
        </h3>
        <dl class="grid grid-cols-[auto_1fr] items-center gap-x-3 gap-y-1.5 text-xs">
          <template v-for="item in group.items" :key="item.label">
            <dt class="font-mono font-semibold whitespace-nowrap">
              {{ item.keys.replace('Ctrl', isMac() ? '⌘' : 'Ctrl') }}
            </dt>
            <dd>{{ item.label }}</dd>
          </template>
        </dl>
      </section>
    </div>
  </AppDialog>
</template>
