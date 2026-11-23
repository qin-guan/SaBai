<script setup lang="ts">
import { PhPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Active Ingredients · Sa'Bai" })

/**
 * The Active ingredients tab of the Stock screen: what doctors order by, the
 * unit each is counted in, its low-stock level and what is available now.
 */
const { data: ingredients, isPending, isError, error } = useMedicationIngredients()
const { data: brands } = useMedicationBrands()
const { data: stock } = useMedicationStock()
const adding = ref(false)
const search = ref('')
const lowOnly = ref(false)

const brandNames = computed(() => brandNamesByIngredient(brands.value ?? []))
/** Available units of each ingredient across its active batches. */
const available = computed(() => {
  if (!stock.value)
    return null
  const totals = new Map<string, number>()
  for (const row of stock.value) {
    if (row.stockStatus === 'active')
      totals.set(row.ingredientId, (totals.get(row.ingredientId) ?? 0) + row.availableQuantity)
  }
  return totals
})
const availableOf = (id: string) => (available.value ? (available.value.get(id) ?? 0) : undefined)
const isLow = (i: { id: string, fallBelow: number }) => {
  const units = availableOf(i.id)
  return units !== undefined && units <= i.fallBelow
}
const lowCount = computed(() => (ingredients.value ?? []).filter(isLow).length)

const rows = computed(() => {
  const query = search.value.trim().toLowerCase()
  return [...(ingredients.value ?? [])]
    .filter(i => !lowOnly.value || isLow(i))
    .filter(i => !query
      || i.name.toLowerCase().includes(query)
      || i.unitOfMeasurement.toLowerCase().includes(query)
      || (i.remarks ?? '').toLowerCase().includes(query)
      || (brandNames.value.get(i.id) ?? []).some(name => name.toLowerCase().includes(query)))
    .sort((a, b) => a.name.localeCompare(b.name))
})
const hasFilters = computed(() => !!search.value.trim() || lowOnly.value)
function clearFilters() {
  search.value = ''
  lowOnly.value = false
}
</script>

<template>
  <StockArea
    v-model:search="search"
    tab="ingredients"
    search-placeholder="Ingredient, brand or unit"
    search-label="Search active ingredients"
  >
    <template #actions>
      <Button data-ingredient-add @click="adding = true">
        <PhPlus aria-hidden="true" /> Add active ingredient
      </Button>
    </template>
    <template #filters>
      <div role="group" aria-label="Show" class="flex border border-band-border bg-card max-sm:w-full">
        <button
          type="button"
          class="flex h-8 items-center justify-center gap-1.5 border-r border-band-border px-2.5 text-xs font-semibold whitespace-nowrap max-sm:flex-1 touch:h-11 touch:text-[0.8125rem]"
          :class="!lowOnly ? 'bg-chrome text-white' : 'text-chrome hover:bg-accent'"
          :aria-pressed="!lowOnly"
          @click="lowOnly = false"
        >
          All
        </button>
        <button
          type="button"
          class="flex h-8 items-center justify-center gap-1.5 px-2.5 text-xs font-semibold whitespace-nowrap max-sm:flex-1 touch:h-11 touch:text-[0.8125rem]"
          :class="lowOnly ? 'bg-chrome text-white' : 'text-chrome hover:bg-accent'"
          :aria-pressed="lowOnly"
          @click="lowOnly = true"
        >
          Low stock
          <span class="tabular-nums" :class="lowOnly ? 'text-white/80' : lowCount > 0 ? 'text-destructive' : 'text-muted-foreground'">{{ lowCount }}</span>
        </button>
      </div>
      <Button v-if="hasFilters" variant="ghost" size="sm" class="text-xs touch:h-10" @click="clearFilters">
        Reset
      </Button>
      <p v-if="ingredients" class="text-xs text-muted-foreground sm:ml-auto touch:text-sm" role="status">
        {{ rows.length }} {{ rows.length === 1 ? 'active ingredient' : 'active ingredients' }}
      </p>
    </template>

    <section aria-label="Active ingredients" class="border border-border bg-card">
      <StateMessage v-if="!ingredients && isError" kind="error" :message="describeLoadError(error, 'The active ingredients could not be loaded. Check the connection and reload.')" />
      <StateMessage v-else-if="!ingredients && isPending" kind="loading" message="Loading active ingredients…" />
      <div v-else-if="!ingredients || ingredients.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="font-semibold">
          No active ingredients yet
        </p>
        <p class="text-xs text-muted-foreground touch:text-sm">
          Start here: add what doctors order (e.g. Paracetamol 500mg, counted in tablets), then its brands and batches.
        </p>
        <Button size="sm" @click="adding = true">
          <PhPlus aria-hidden="true" /> Add active ingredient
        </Button>
      </div>
      <div v-else-if="rows.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="text-xs text-muted-foreground touch:text-sm">
          No active ingredients match these filters.
        </p>
        <Button variant="outline" size="sm" @click="clearFilters">
          Clear filters
        </Button>
      </div>
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Active ingredient</TableHead>
            <TableHead>Counted in</TableHead>
            <TableHead class="text-right">
              Low at
            </TableHead>
            <TableHead class="text-right">
              Available now
            </TableHead>
            <TableHead>Brands</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              <span class="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <IngredientRow
            v-for="ingredient in rows"
            :key="ingredient.id"
            :ingredient="ingredient"
            :available="availableOf(ingredient.id)"
            :brand-names="brandNames.get(ingredient.id)"
          />
        </TableBody>
      </Table>
    </section>
    <AddIngredientDialog v-if="adding" @close="adding = false" />
  </StockArea>
</template>
