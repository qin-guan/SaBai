<script setup lang="ts">
import { PhFlask, PhMagnifyingGlass, PhPackage, PhTag, PhX } from '@phosphor-icons/vue'
import { Input } from '@/components/ui/input'

/**
 * The shared frame of the Stock screen. Batches, brands and active ingredients
 * keep their own routes (deep links, universal search) but read as one page:
 * the same header, a tab strip with how many of each there are, the search and
 * filter bar under it and the tab's primary action in the header.
 *
 * Slots: `actions` (the primary action, in the header), `filters` (controls
 * beside the search box) and the default slot (the tab's content).
 */
const props = defineProps<{
  tab: 'stock' | 'brands' | 'ingredients'
  searchPlaceholder: string
  /** Names what the search box filters, for assistive tech. */
  searchLabel: string
}>()
const search = defineModel<string>('search', { default: '' })

const stock = useMedicationStock()
const brands = useMedicationBrands()
const ingredients = useMedicationIngredients()

const tabs = computed(() => [
  { key: 'stock', label: 'Batches', to: paths.medicationStock(), icon: PhPackage, count: stock.data.value?.length },
  { key: 'brands', label: 'Brands', to: paths.medicationBrands(), icon: PhTag, count: brands.data.value?.length },
  { key: 'ingredients', label: 'Active ingredients', shortLabel: 'Ingredients', to: paths.medicationActiveIngredients(), icon: PhFlask, count: ingredients.data.value?.length },
] as const)

const descriptions = {
  stock: 'Every batch on the shelves, by active ingredient and brand, with what is reserved for pending orders.',
  brands: 'The brands each active ingredient is stocked as.',
  ingredients: 'What doctors order by: the unit it is counted in and the level below which it is flagged as low.',
}

/** Esc clears a typed search first; with an empty box it does what it does everywhere (go back). */
function onSearchEsc(event: KeyboardEvent) {
  if (!search.value)
    return
  event.stopPropagation()
  search.value = ''
}
const current = computed(() => tabs.value.find(t => t.key === props.tab)!)
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="Medication stock"
      :description="descriptions[props.tab]"
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Stock', to: props.tab === 'stock' ? undefined : paths.medicationStock() }, ...(props.tab === 'stock' ? [] : [{ label: current.label }])]"
      class="mb-0!"
    >
      <template v-if="$slots.actions" #actions>
        <slot name="actions" />
      </template>
    </PageHeader>

    <div class="border border-border bg-card">
      <nav aria-label="Stock sections" class="flex border-b border-band-border bg-band">
        <NuxtLink
          v-for="t in tabs"
          :key="t.key"
          :to="t.to"
          class="flex h-9 min-w-0 items-center justify-center gap-1.5 border-r border-band-border px-3 text-[0.8125rem] font-bold text-chrome hover:bg-accent focus-visible:bg-accent focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring max-sm:flex-1 max-sm:px-1.5 touch:h-11 touch:text-sm"
          :class="t.key === props.tab && 'bg-card shadow-[inset_0_3px_0_var(--color-primary)] hover:bg-card'"
          :aria-current="t.key === props.tab ? 'page' : undefined"
          :data-stock-tab="t.key"
        >
          <component :is="t.icon" class="size-4 shrink-0 max-sm:hidden" aria-hidden="true" />
          <span class="truncate" :class="'shortLabel' in t && 'max-sm:hidden'">{{ t.label }}</span>
          <span v-if="'shortLabel' in t" class="truncate sm:hidden" aria-hidden="true">{{ t.shortLabel }}</span>
          <span
            v-if="t.count !== undefined"
            class="shrink-0 text-xs font-semibold tabular-nums text-muted-foreground"
            :aria-label="`${t.count} in total`"
          >{{ t.count }}</span>
        </NuxtLink>
      </nav>
      <div class="flex flex-col gap-2 p-2 sm:flex-row sm:flex-wrap sm:items-center">
        <div class="relative sm:w-72">
          <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input
            v-model="search"
            type="search"
            :placeholder="props.searchPlaceholder"
            :aria-label="props.searchLabel"
            class="pr-8 pl-8"
            data-stock-search
            @keydown.esc="onSearchEsc"
          />
          <button
            v-if="search"
            type="button"
            class="absolute top-1/2 right-1.5 -translate-y-1/2 p-1 text-muted-foreground hover:text-foreground"
            aria-label="Clear search"
            @click="search = ''"
          >
            <PhX class="size-3.5" aria-hidden="true" />
          </button>
        </div>
        <slot name="filters" />
      </div>
    </div>

    <slot />
  </div>
</template>
