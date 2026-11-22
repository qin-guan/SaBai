<script setup lang="ts">
import { PhPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Brands · Sa'Bai" })

/** The Brands tab of the Stock screen: every brand with its active ingredient and what is in stock of it. */
const { data: brands, isPending, isError, error } = useMedicationBrands()
// For the active ingredient names and the ingredient picker.
const { data: ingredients, isPending: ingredientsLoading } = useMedicationIngredients()
const { data: stock } = useMedicationStock()
const adding = ref(false)
const search = ref('')
const ingredientId = ref('')

const ingredientNames = computed(() => new Map((ingredients.value ?? []).map(i => [i.id, i.name])))
const sortedIngredients = computed(() => [...(ingredients.value ?? [])].sort((a, b) => a.name.localeCompare(b.name)))

/** Active batches of each brand: how many, and the units still available. */
const inStock = computed(() => {
  if (!stock.value)
    return null
  const totals = new Map<string, { available: number, batches: number }>()
  for (const row of stock.value) {
    if (row.stockStatus !== 'active')
      continue
    const total = totals.get(row.medicationBrandId) ?? { available: 0, batches: 0 }
    total.available += row.availableQuantity
    total.batches += 1
    totals.set(row.medicationBrandId, total)
  }
  return totals
})

const rows = computed(() => {
  const query = search.value.trim().toLowerCase()
  return [...(brands.value ?? [])]
    .filter(b => !ingredientId.value || b.activeIngredientId === ingredientId.value)
    .filter(b => !query
      || b.name.toLowerCase().includes(query)
      || (ingredientNames.value.get(b.activeIngredientId) ?? '').toLowerCase().includes(query)
      || (b.remarks ?? '').toLowerCase().includes(query))
    .sort((a, b) =>
      (ingredientNames.value.get(a.activeIngredientId) ?? '').localeCompare(ingredientNames.value.get(b.activeIngredientId) ?? '')
      || a.name.localeCompare(b.name))
})
const hasFilters = computed(() => !!search.value.trim() || !!ingredientId.value)
function clearFilters() {
  search.value = ''
  ingredientId.value = ''
}
</script>

<template>
  <StockArea
    v-model:search="search"
    tab="brands"
    search-placeholder="Brand, active ingredient or remarks"
    search-label="Search brands"
  >
    <template #actions>
      <Button :disabled="ingredientsLoading" data-brand-add @click="adding = true">
        <PhPlus aria-hidden="true" /> Add brand
      </Button>
    </template>
    <template #filters>
      <NativeSelect v-model="ingredientId" aria-label="Active ingredient" class="sm:w-64">
        <NativeSelectOption value="">
          All active ingredients
        </NativeSelectOption>
        <NativeSelectOption v-for="i in sortedIngredients" :key="i.id" :value="i.id">
          {{ i.name }}
        </NativeSelectOption>
      </NativeSelect>
      <Button v-if="hasFilters" variant="ghost" size="sm" class="text-xs touch:h-10" @click="clearFilters">
        Reset
      </Button>
      <p v-if="brands" class="text-xs text-muted-foreground sm:ml-auto touch:text-sm" role="status">
        {{ rows.length }} {{ rows.length === 1 ? 'brand' : 'brands' }}
      </p>
    </template>

    <section aria-label="Brands" class="border border-border bg-card">
      <StateMessage v-if="!brands && isError" kind="error" :message="describeLoadError(error, 'The brands could not be loaded. Check the connection and reload.')" />
      <StateMessage v-else-if="!brands && isPending" kind="loading" message="Loading brands…" />
      <div v-else-if="!brands || brands.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="font-semibold">
          No brands yet
        </p>
        <p class="text-xs text-muted-foreground touch:text-sm">
          Add the brands each active ingredient is stocked as; batches are added per brand.
        </p>
        <Button size="sm" :disabled="ingredientsLoading" @click="adding = true">
          <PhPlus aria-hidden="true" /> Add brand
        </Button>
      </div>
      <div v-else-if="rows.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="text-xs text-muted-foreground touch:text-sm">
          No brands match these filters.
        </p>
        <Button variant="outline" size="sm" @click="clearFilters">
          Clear filters
        </Button>
      </div>
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Brand</TableHead>
            <TableHead>Active ingredient</TableHead>
            <TableHead class="text-right">
              In stock
            </TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              <span class="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <BrandRow
            v-for="brand in rows"
            :key="brand.id"
            :brand="brand"
            :ingredients="ingredients ?? []"
            :in-stock="inStock ? (inStock.get(brand.id) ?? { available: 0, batches: 0 }) : undefined"
          />
        </TableBody>
      </Table>
    </section>
    <AddBrandDialog v-if="adding" :ingredients="sortedIngredients" @close="adding = false" />
  </StockArea>
</template>
