<script setup lang="ts">
import { PhCaretDown, PhCheck, PhFunnel, PhTag, PhX } from '@phosphor-icons/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import {
  collectUniqueTags,
  hasTagSelected,
  toggleTagSelection,
  type TagMatchMode,
} from '~/utils/patient'

/**
 * Inline and in-table multiselect dropdown for filtering rows by clinic tags.
 * Supports choosing between matching any selected tag (OR) or all selected tags (AND).
 */
const model = defineModel<string[]>({ default: () => [] })
const matchMode = defineModel<TagMatchMode>('matchMode', { default: 'any' })

const props = withDefaults(
  defineProps<{
    /** Extra tags from the current dataset to merge with `/api/tags`. */
    availableTags?: readonly string[]
    /** `inline` for filter bars/toolbars; `header` for table column headers. */
    variant?: 'inline' | 'header'
    label?: string
    align?: 'start' | 'center' | 'end'
    showSelectedBadges?: boolean
  }>(),
  {
    availableTags: () => [],
    variant: 'inline',
    label: 'Tags',
    align: 'start',
    showSelectedBadges: undefined,
  },
)

const { data: knownTags } = useTags()
const search = ref('')

const allTags = computed(() =>
  collectUniqueTags([knownTags.value, props.availableTags, model.value]),
)

const filteredTags = computed(() => {
  const q = search.value.trim().toLowerCase()
  if (!q)
    return allTags.value
  return allTags.value.filter(t => t.toLowerCase().includes(q))
})

const shouldShowSelectedBadges = computed(() =>
  props.showSelectedBadges ?? props.variant === 'inline',
)

function isSelected(tag: string) {
  return hasTagSelected(model.value, tag)
}

function toggle(tag: string) {
  model.value = toggleTagSelection(model.value, tag)
}

function clear() {
  model.value = []
}
</script>

<template>
  <div class="inline-flex flex-wrap items-center gap-1.5">
    <Popover>
      <PopoverTrigger as-child>
        <button
          v-if="props.variant === 'header'"
          type="button"
          class="inline-flex items-center gap-1 font-semibold text-chrome hover:text-primary focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          :aria-label="`Filter by ${props.label.toLowerCase()}${model.length ? ` (${model.length} active)` : ''}`"
        >
          <span>{{ props.label }}</span>
          <PhFunnel
            :weight="model.length > 0 ? 'fill' : 'regular'"
            class="size-3.5 shrink-0"
            :class="model.length > 0 ? 'text-primary' : 'text-muted-foreground'"
            aria-hidden="true"
          />
          <Badge
            v-if="model.length > 0"
            variant="default"
            class="h-4 min-w-4 px-1 text-[0.6875rem] leading-none"
          >
            {{ model.length }}
          </Badge>
        </button>

        <Button
          v-else
          type="button"
          variant="outline"
          size="sm"
          class="h-8 gap-1.5 border-input bg-card text-xs font-normal touch:h-11 touch:text-sm"
          :class="model.length > 0 && 'border-primary/60 text-foreground'"
          :aria-label="`Filter by ${props.label.toLowerCase()}${model.length ? ` (${model.length} selected)` : ''}`"
        >
          <PhTag class="size-3.5 shrink-0 text-muted-foreground" aria-hidden="true" />
          <span class="font-medium">{{ props.label }}</span>
          <Badge
            v-if="model.length > 0"
            variant="default"
            class="h-4 px-1 text-[0.6875rem] leading-none"
          >
            {{ model.length }}{{ model.length > 1 ? ` · ${matchMode === 'all' ? 'All' : 'Any'}` : '' }}
          </Badge>
          <PhCaretDown class="size-3 shrink-0 text-muted-foreground" aria-hidden="true" />
        </Button>
      </PopoverTrigger>

      <PopoverContent :align="props.align" class="w-64 p-2">
        <div class="flex items-center justify-between gap-2">
          <span class="text-xs font-bold text-chrome">Filter by tag</span>
          <div
            class="inline-flex border border-border bg-muted/40 p-0.5 text-[0.6875rem]"
            role="group"
            aria-label="Tag match mode"
          >
            <button
              type="button"
              class="px-1.5 py-0.5 font-semibold transition-colors"
              :class="matchMode === 'any' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
              :aria-pressed="matchMode === 'any'"
              title="Match rows with any selected tag (OR)"
              @click="matchMode = 'any'"
            >
              Match any
            </button>
            <button
              type="button"
              class="px-1.5 py-0.5 font-semibold transition-colors"
              :class="matchMode === 'all' ? 'bg-primary text-primary-foreground' : 'text-muted-foreground hover:text-foreground'"
              :aria-pressed="matchMode === 'all'"
              title="Match rows with all selected tags (AND)"
              @click="matchMode = 'all'"
            >
              Match all
            </button>
          </div>
        </div>

        <Input
          v-if="allTags.length > 0"
          v-model="search"
          type="search"
          placeholder="Search tags…"
          aria-label="Search tags"
          class="h-7 text-xs"
          autocomplete="off"
        />

        <ul
          class="max-h-48 divide-y divide-border/40 overflow-y-auto border border-border"
          role="listbox"
          aria-multiselectable="true"
          aria-label="Available tags"
        >
          <li v-for="tag in filteredTags" :key="tag" role="none">
            <button
              type="button"
              role="option"
              :aria-selected="isSelected(tag)"
              class="flex w-full items-center justify-between gap-2 px-2 py-1.5 text-left text-xs hover:bg-accent focus-visible:bg-accent focus-visible:outline-none touch:py-2"
              @click="toggle(tag)"
            >
              <span class="flex min-w-0 items-center gap-2">
                <span
                  class="flex size-3.5 shrink-0 items-center justify-center border"
                  :class="isSelected(tag) ? 'border-primary bg-primary text-primary-foreground' : 'border-input bg-card'"
                  aria-hidden="true"
                >
                  <PhCheck v-if="isSelected(tag)" weight="bold" class="size-2.5" />
                </span>
                <span class="truncate">{{ tag }}</span>
              </span>
            </button>
          </li>
          <li
            v-if="filteredTags.length === 0"
            class="px-2 py-3 text-center text-xs text-muted-foreground"
          >
            {{ allTags.length === 0 ? 'No tags available yet.' : 'No matching tags.' }}
          </li>
        </ul>

        <div v-if="model.length > 0" class="flex items-center justify-between border-t border-border pt-1.5">
          <span class="text-[0.6875rem] text-muted-foreground">
            {{ model.length }} selected ({{ matchMode === 'all' ? 'all' : 'any' }})
          </span>
          <button
            type="button"
            class="text-xs font-semibold text-primary hover:underline focus-visible:outline-none"
            @click="clear"
          >
            Clear
          </button>
        </div>
      </PopoverContent>
    </Popover>

    <template v-if="shouldShowSelectedBadges && model.length > 0">
      <Badge
        v-for="tag in model"
        :key="tag"
        variant="secondary"
        class="gap-1 pr-1"
      >
        <span>{{ tag }}</span>
        <button
          type="button"
          class="rounded-sm p-0.5 text-muted-foreground hover:bg-muted-foreground/15 hover:text-foreground focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          :aria-label="`Remove tag filter ${tag}`"
          @click="toggle(tag)"
        >
          <PhX class="size-3" aria-hidden="true" />
        </button>
      </Badge>
    </template>
  </div>
</template>
