<script setup lang="ts">
import { Badge } from '@/components/ui/badge'
import { hasTagSelected, toggleTagSelection } from '~/utils/patient'

/**
 * Displays a patient's or staff member's tags in a table cell or row.
 * Clicking a tag badge toggles that tag in `v-model:selected-tags` for in-table filtering.
 */
const selectedTags = defineModel<string[]>('selectedTags', { default: () => [] })

const props = withDefaults(
  defineProps<{
    tags?: readonly string[] | null
    showEmpty?: boolean
    emptyText?: string
  }>(),
  {
    tags: () => [],
    showEmpty: false,
    emptyText: '—',
  },
)

const normalizedTags = computed(() => (props.tags ?? []).filter(Boolean))

function isSelected(tag: string) {
  return hasTagSelected(selectedTags.value, tag)
}

function toggle(tag: string) {
  selectedTags.value = toggleTagSelection(selectedTags.value, tag)
}
</script>

<template>
  <span v-if="normalizedTags.length > 0" class="relative z-[1] inline-flex flex-wrap items-center gap-1">
    <button
      v-for="tag in normalizedTags"
      :key="tag"
      type="button"
      class="inline-flex focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
      :aria-pressed="isSelected(tag)"
      :title="isSelected(tag) ? `Remove tag filter: ${tag}` : `Filter by tag: ${tag}`"
      @click.stop="toggle(tag)"
    >
      <Badge
        :variant="isSelected(tag) ? 'default' : 'secondary'"
        class="cursor-pointer transition-colors hover:bg-accent hover:text-accent-foreground"
      >
        {{ tag }}
      </Badge>
    </button>
  </span>
  <span v-else-if="props.showEmpty" class="text-xs text-muted-foreground">{{ props.emptyText }}</span>
</template>
