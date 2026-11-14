<script setup lang="ts">
import { Badge } from '@/components/ui/badge'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import { hasTagSelected, partitionPatientTags, toggleTagSelection } from '~/utils/patient'

/**
 * Displays a patient's or staff member's tags in a table cell or row.
 * Clicking a tag badge toggles that tag in `v-model:selected-tags` for in-table filtering.
 * When `maxVisible` is set, extra tags collapse into an interactive `+N` popover.
 */
const selectedTags = defineModel<string[]>('selectedTags', { default: () => [] })

const props = withDefaults(
  defineProps<{
    tags?: readonly string[] | null
    showEmpty?: boolean
    emptyText?: string
    /** Cap visible badges and collapse the rest into a `+N` popover. */
    maxVisible?: number
    /** Keep badges on a single line and truncate long tag names to fit narrow table cells. */
    singleLine?: boolean
  }>(),
  {
    tags: () => [],
    showEmpty: false,
    emptyText: '—',
    maxVisible: undefined,
    singleLine: false,
  },
)

const partitioned = computed(() =>
  partitionPatientTags(props.tags, selectedTags.value, props.maxVisible),
)

function isSelected(tag: string) {
  return hasTagSelected(selectedTags.value, tag)
}

function toggle(tag: string) {
  selectedTags.value = toggleTagSelection(selectedTags.value, tag)
}
</script>

<template>
  <span
    v-if="partitioned.ordered.length > 0"
    class="relative z-[1] inline-flex max-w-full min-w-0 items-center gap-1"
    :class="props.singleLine ? 'flex-nowrap' : 'flex-wrap'"
  >
    <button
      v-for="tag in partitioned.visible"
      :key="tag"
      type="button"
      class="inline-flex max-w-full min-w-0 focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
      :aria-pressed="isSelected(tag)"
      :title="isSelected(tag) ? `Remove tag filter: ${tag}` : `Filter by tag: ${tag}`"
      @click.stop="toggle(tag)"
    >
      <Badge
        :variant="isSelected(tag) ? 'default' : 'secondary'"
        class="max-w-full min-w-0 shrink cursor-pointer transition-colors hover:bg-accent hover:text-accent-foreground"
      >
        <span class="truncate">{{ tag }}</span>
      </Badge>
    </button>

    <Popover v-if="partitioned.hidden.length > 0">
      <PopoverTrigger as-child>
        <button
          type="button"
          class="inline-flex shrink-0 focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          :aria-label="`Show ${partitioned.hidden.length} more tag${partitioned.hidden.length === 1 ? '' : 's'}: ${partitioned.hidden.join(', ')}`"
          :title="partitioned.hidden.join(', ')"
          @click.stop
        >
          <Badge
            :variant="partitioned.hasHiddenSelected ? 'default' : 'outline'"
            class="cursor-pointer px-1.5 font-mono text-[0.6875rem] tabular-nums transition-colors hover:bg-accent hover:text-accent-foreground"
          >
            +{{ partitioned.hidden.length }}
          </Badge>
        </button>
      </PopoverTrigger>
      <PopoverContent align="start" class="w-60 p-2" @click.stop>
        <div class="mb-1.5 flex items-center justify-between gap-2 text-xs font-semibold text-chrome">
          <span>Tags ({{ partitioned.ordered.length }})</span>
          <span class="text-[0.6875rem] font-normal text-muted-foreground">Click to filter</span>
        </div>
        <div class="flex flex-wrap gap-1">
          <button
            v-for="tag in partitioned.ordered"
            :key="tag"
            type="button"
            class="inline-flex max-w-full focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            :aria-pressed="isSelected(tag)"
            :title="isSelected(tag) ? `Remove tag filter: ${tag}` : `Filter by tag: ${tag}`"
            @click.stop="toggle(tag)"
          >
            <Badge
              :variant="isSelected(tag) ? 'default' : 'secondary'"
              class="max-w-full shrink cursor-pointer transition-colors hover:bg-accent hover:text-accent-foreground"
            >
              <span class="truncate">{{ tag }}</span>
            </Badge>
          </button>
        </div>
      </PopoverContent>
    </Popover>
  </span>
  <span v-else-if="props.showEmpty" class="text-xs text-muted-foreground">{{ props.emptyText }}</span>
</template>
