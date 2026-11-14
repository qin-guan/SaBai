<script setup lang="ts">
import { PhCaretDown, PhCheck, PhFunnel, PhX } from '@phosphor-icons/vue'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'

export interface StatusFilterOption {
  value: string
  label: string
}

/**
 * Inline and in-table multiselect dropdown for filtering rows by status.
 */
const model = defineModel<string[]>({ default: () => [] })

const props = withDefaults(
  defineProps<{
    options: readonly StatusFilterOption[]
    /** `inline` for filter bars/toolbars; `header` for table column headers. */
    variant?: 'inline' | 'header'
    label?: string
    align?: 'start' | 'center' | 'end'
    showSelectedBadges?: boolean
  }>(),
  {
    variant: 'inline',
    label: 'Status',
    align: 'start',
    showSelectedBadges: undefined,
  },
)

const shouldShowSelectedBadges = computed(() =>
  props.showSelectedBadges ?? props.variant === 'inline',
)

const labelMap = computed(() => {
  const map = new Map<string, string>()
  for (const opt of props.options)
    map.set(opt.value, opt.label)
  return map
})

function isSelected(value: string) {
  return model.value.includes(value)
}

function toggle(value: string) {
  if (model.value.includes(value)) {
    model.value = model.value.filter(v => v !== value)
  }
  else {
    model.value = [...model.value, value]
  }
}

function clear() {
  model.value = []
}

function labelFor(value: string) {
  return labelMap.value.get(value) ?? value
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
          <PhFunnel
            :weight="model.length > 0 ? 'fill' : 'regular'"
            class="size-3.5 shrink-0"
            :class="model.length > 0 ? 'text-primary' : 'text-muted-foreground'"
            aria-hidden="true"
          />
          <span class="font-medium">{{ props.label }}</span>
          <Badge
            v-if="model.length > 0"
            variant="default"
            class="h-4 px-1 text-[0.6875rem] leading-none"
          >
            {{ model.length }}
          </Badge>
          <PhCaretDown class="size-3 shrink-0 text-muted-foreground" aria-hidden="true" />
        </Button>
      </PopoverTrigger>

      <PopoverContent :align="props.align" class="w-60 p-2">
        <div class="flex items-center justify-between gap-2">
          <span class="text-xs font-bold text-chrome">Filter by status</span>
          <button
            v-if="model.length > 0"
            type="button"
            class="text-xs font-semibold text-primary hover:underline focus-visible:outline-none"
            @click="clear"
          >
            Clear
          </button>
        </div>

        <ul
          class="max-h-56 divide-y divide-border/40 overflow-y-auto border border-border"
          role="listbox"
          aria-multiselectable="true"
          :aria-label="props.label"
        >
          <li v-for="opt in props.options" :key="opt.value" role="none">
            <button
              type="button"
              role="option"
              :aria-selected="isSelected(opt.value)"
              class="flex w-full items-center justify-between gap-2 px-2 py-1.5 text-left text-xs hover:bg-accent focus-visible:bg-accent focus-visible:outline-none touch:py-2"
              @click="toggle(opt.value)"
            >
              <span class="flex min-w-0 items-center gap-2">
                <span
                  class="flex size-3.5 shrink-0 items-center justify-center border"
                  :class="isSelected(opt.value) ? 'border-primary bg-primary text-primary-foreground' : 'border-input bg-card'"
                  aria-hidden="true"
                >
                  <PhCheck v-if="isSelected(opt.value)" weight="bold" class="size-2.5" />
                </span>
                <span class="truncate">{{ opt.label }}</span>
              </span>
            </button>
          </li>
        </ul>
      </PopoverContent>
    </Popover>

    <template v-if="shouldShowSelectedBadges && model.length > 0">
      <Badge
        v-for="val in model"
        :key="val"
        variant="secondary"
        class="gap-1 pr-1"
      >
        <span>{{ labelFor(val) }}</span>
        <button
          type="button"
          class="rounded-sm p-0.5 text-muted-foreground hover:bg-muted-foreground/15 hover:text-foreground focus:outline-none focus-visible:ring-1 focus-visible:ring-ring"
          :aria-label="`Remove status filter ${labelFor(val)}`"
          @click="toggle(val)"
        >
          <PhX class="size-3" aria-hidden="true" />
        </button>
      </Badge>
    </template>
  </div>
</template>
