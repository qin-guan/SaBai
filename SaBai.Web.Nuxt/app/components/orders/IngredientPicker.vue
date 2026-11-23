<script setup lang="ts">
import { PhCaretUpDown, PhMagnifyingGlass } from '@phosphor-icons/vue'
import { cn } from 'cn'
import { ListboxFilter } from 'reka-ui'
import { Command, CommandGroup, CommandItem, CommandList } from '@/components/ui/command'
import { Popover, PopoverContent, PopoverTrigger } from '@/components/ui/popover'
import type { OrderableIngredient } from '~/utils/ordering'

/**
 * Picks the active ingredient to order: a button showing the choice that
 * opens a searchable list (type part of the ingredient name, or of a brand
 * name when `options` carry them; ↑/↓ move, Enter chooses, Esc closes).
 *
 * Every ingredient is listed with what can still be ordered, in its unit; one
 * with nothing left is listed under "Out of stock" and cannot be chosen
 * (unless it is already the selection, so the error next to it can say why).
 *
 * `id`, `aria-*` and `data-*` attributes go on the button, so a `FormField`
 * label and error are announced with it.
 */
defineOptions({ inheritAttrs: false })
const model = defineModel<string>({ default: '' })
const props = defineProps<{
  options: readonly OrderableIngredient[]
  placeholder?: string
  disabled?: boolean
}>()

const open = ref(false)
const query = ref('')
const listId = useId()

const selected = computed(() => props.options.find(o => o.ingredientId === model.value))
const matches = computed(() => searchOrderableIngredients(props.options, query.value))
const groups = computed(() => [
  { heading: 'Available', items: matches.value.filter(o => !o.outOfStock) },
  { heading: 'Out of stock — cannot be ordered', items: matches.value.filter(o => o.outOfStock) },
].filter(group => group.items.length > 0))

const canChoose = (option: OrderableIngredient) => !option.outOfStock || option.ingredientId === model.value

function choose(option: OrderableIngredient) {
  if (!canChoose(option))
    return
  model.value = option.ingredientId
  open.value = false
}

// Each opening starts from the whole list.
watch(open, (isOpen) => {
  if (isOpen)
    query.value = ''
})
</script>

<template>
  <Popover v-model:open="open">
    <PopoverTrigger as-child>
      <button
        v-bind="$attrs"
        type="button"
        role="combobox"
        aria-haspopup="listbox"
        :aria-expanded="open"
        :aria-controls="open ? listId : undefined"
        :disabled="props.disabled"
        data-slot="ingredient-picker"
        :class="cn(
          'border-input dark:bg-input/30 dark:hover:bg-input/50 focus-visible:border-ring focus-visible:ring-ring/50 aria-invalid:ring-destructive/20 dark:aria-invalid:ring-destructive/40 aria-invalid:border-destructive dark:aria-invalid:border-destructive/50 flex h-8 w-full min-w-0 items-center gap-2 rounded-none border bg-transparent py-1 pr-2 pl-2.5 text-left text-xs transition-colors outline-none focus-visible:ring-1 aria-invalid:ring-1 disabled:pointer-events-none disabled:cursor-not-allowed disabled:opacity-50 touch:h-11 touch:text-base',
          $attrs.class as string,
        )"
      >
        <template v-if="selected">
          <span class="min-w-0 flex-1 truncate">{{ selected.name }}</span>
          <span
            class="shrink-0 tabular-nums"
            :class="selected.outOfStock ? 'font-semibold text-destructive' : 'text-muted-foreground'"
          >{{ orderableAvailability(selected) }}</span>
        </template>
        <span v-else class="min-w-0 flex-1 truncate text-muted-foreground">{{ props.placeholder ?? 'Search for a medication…' }}</span>
        <PhCaretUpDown class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
      </button>
    </PopoverTrigger>
    <PopoverContent
      align="start"
      class="w-(--reka-popover-trigger-width) min-w-64 gap-0 p-0"
    >
      <!-- Not bound to the model: `choose` sets it, so choosing the current option again just closes. -->
      <Command :model-value="model" :highlight-on-hover="true">
        <div class="flex items-center gap-2 border-b border-border px-2">
          <PhMagnifyingGlass class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          <ListboxFilter
            v-model="query"
            auto-focus
            role="combobox"
            aria-autocomplete="list"
            aria-expanded="true"
            :aria-controls="listId"
            aria-label="Search medication by ingredient or brand name"
            placeholder="Type to search…"
            data-slot="command-input"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            class="h-8 w-full min-w-0 bg-transparent text-xs outline-hidden placeholder:text-muted-foreground touch:h-11"
          />
        </div>
        <CommandList :id="listId" aria-label="Medications">
          <CommandGroup v-for="group in groups" :key="group.heading" :heading="group.heading">
            <CommandItem
              v-for="option in group.items"
              :key="option.ingredientId"
              :value="option.ingredientId"
              :disabled="!canChoose(option)"
              class="items-start py-1.5 data-disabled:pointer-events-none data-disabled:opacity-60 touch:py-2.5 touch:text-sm"
              @select.prevent="choose(option)"
            >
              <span class="flex min-w-0 flex-1 flex-col">
                <span class="truncate font-medium">{{ option.name }}</span>
                <span v-if="matchingBrandNames(option, query).length" class="truncate text-muted-foreground">
                  Brands: {{ matchingBrandNames(option, query).join(', ') }}
                </span>
              </span>
              <span
                class="shrink-0 tabular-nums"
                :class="option.outOfStock ? 'text-destructive' : 'text-muted-foreground'"
              >{{ orderableAvailability(option) }}</span>
            </CommandItem>
          </CommandGroup>
          <p v-if="groups.length === 0" class="px-2 py-4 text-center text-xs text-muted-foreground touch:text-sm" role="status">
            {{ props.options.length === 0 ? 'No medication to choose from.' : `No medication matches “${query.trim()}”.` }}
          </p>
        </CommandList>
      </Command>
    </PopoverContent>
  </Popover>
</template>
