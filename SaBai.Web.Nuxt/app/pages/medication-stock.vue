<script setup lang="ts">
import { PhPlus, PhWarning } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import type { StockFlag } from '~/utils/stockView'

useHead({ title: "Medication Stock · Sa'Bai" })

/**
 * The Batches tab of the Stock screen: every batch under its active
 * ingredient (with the ingredient's totals against its low-stock level), by
 * brand and earliest expiry.
 */

// Cached data renders even when the refetch failed (offline); errors show only when there is nothing to show.
const { data: stock, isPending, isError, error } = useMedicationStock()
// Ingredients without any batch still show, as out of stock.
const { data: ingredients } = useMedicationIngredients()

const search = ref('')
const status = ref('')
const flag = ref<StockFlag>('all')

const groups = computed(() => groupStockByIngredient(stock.value ?? [], ingredients.value ?? []))
const counts = computed(() => countStock(groups.value))
const shown = computed(() => filterStockGroups(groups.value, { query: search.value, status: status.value, flag: flag.value }))
const shownBatches = computed(() => shown.value.reduce((n, g) => n + g.batches.length, 0))
const hasFilters = computed(() => !!search.value.trim() || !!status.value || flag.value !== 'all')

const flags = computed<{ key: StockFlag, label: string, count?: number, tone?: string }[]>(() => [
  { key: 'all', label: 'All' },
  { key: 'low', label: 'Low stock', count: counts.value.lowIngredients, tone: 'text-destructive' },
  { key: 'soon', label: 'Expiring soon', count: counts.value.expiringSoon, tone: 'text-warning' },
  { key: 'expired', label: 'Expired', count: counts.value.expired, tone: 'text-destructive' },
])

function clearFilters() {
  search.value = ''
  status.value = ''
  flag.value = 'all'
}

const creating = ref(false)
const logFor = ref<MedicationStockRow | null>(null)
const editing = ref<MedicationStockRow | null>(null)
const adjusting = ref<MedicationStockRow | null>(null)
const splitting = ref<MedicationStockRow | null>(null)

/** The dialogs show the batch as it is now (a refetch or an optimistic edit may have changed it). */
const live = (item: MedicationStockRow | null) => (item && stock.value?.find(row => row.id === item.id)) || item
</script>

<template>
  <StockArea
    v-model:search="search"
    tab="stock"
    search-placeholder="Ingredient, brand, location or remarks"
    search-label="Search batches"
  >
    <template #actions>
      <Button data-stock-add @click="creating = true">
        <PhPlus aria-hidden="true" /> Add batch
      </Button>
    </template>
    <template #filters>
      <div role="group" aria-label="Show" class="flex border border-band-border bg-card max-sm:w-full">
        <button
          v-for="f in flags"
          :key="f.key"
          type="button"
          class="flex h-8 items-center justify-center gap-1.5 border-r border-band-border px-2.5 text-xs font-semibold whitespace-nowrap last:border-r-0 max-sm:flex-1 max-sm:px-1 touch:h-11 touch:text-[0.8125rem]"
          :class="flag === f.key ? 'bg-chrome text-white' : 'text-chrome hover:bg-accent'"
          :aria-pressed="flag === f.key"
          :data-stock-flag="f.key"
          @click="flag = f.key"
        >
          {{ f.label }}
          <span
            v-if="f.count !== undefined"
            class="tabular-nums"
            :class="flag === f.key ? 'text-white/80' : f.count > 0 ? f.tone : 'text-muted-foreground'"
          >{{ f.count }}</span>
        </button>
      </div>
      <NativeSelect v-model="status" aria-label="Batch status" class="sm:w-40">
        <NativeSelectOption value="">
          All statuses
        </NativeSelectOption>
        <NativeSelectOption v-for="o in stockStatusOptions" :key="o.value" :value="o.value">
          {{ o.label }}
        </NativeSelectOption>
      </NativeSelect>
      <Button v-if="hasFilters" variant="ghost" size="sm" class="text-xs touch:h-10" @click="clearFilters">
        Reset
      </Button>
      <p v-if="stock" class="text-xs text-muted-foreground sm:ml-auto touch:text-sm" role="status">
        {{ shownBatches }} {{ shownBatches === 1 ? 'batch' : 'batches' }} of {{ shown.length }} {{ shown.length === 1 ? 'ingredient' : 'ingredients' }}
      </p>
    </template>

    <section aria-label="Batches by active ingredient" class="border border-border bg-card">
      <StateMessage v-if="!stock && isError" kind="error" :message="describeLoadError(error, 'The stock list could not be loaded. Check the connection and reload.')" />
      <StateMessage v-else-if="!stock && isPending" kind="loading" message="Loading stock…" />
      <div v-else-if="groups.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="font-semibold">
          No stock yet
        </p>
        <p class="text-xs text-muted-foreground touch:text-sm">
          Add the active ingredients and their brands first, then add each batch as it arrives.
        </p>
        <Button size="sm" @click="creating = true">
          <PhPlus aria-hidden="true" /> Add batch
        </Button>
      </div>
      <div v-else-if="shown.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="text-xs text-muted-foreground touch:text-sm">
          No batches match these filters.
        </p>
        <Button variant="outline" size="sm" @click="clearFilters">
          Clear filters
        </Button>
      </div>
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Brand</TableHead>
            <TableHead>Location</TableHead>
            <TableHead class="text-right">
              On hand
            </TableHead>
            <TableHead class="text-right">
              Reserved
            </TableHead>
            <TableHead class="text-right">
              Available
            </TableHead>
            <TableHead>Expiry</TableHead>
            <TableHead>Status</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              <span class="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody v-for="group in shown" :key="group.ingredientId" :data-ingredient-group="group.ingredientName">
          <!-- The ingredient: its name, what can be ordered and where the low-stock level is. -->
          <TableRow class="stock-group-row bg-band! hover:bg-band!">
            <TableCell
              colspan="9"
              class="border-y border-band-border py-1.5!"
              :class="group.isLow && 'shadow-[inset_3px_0_0_var(--color-destructive)]'"
            >
              <div class="flex flex-wrap items-center gap-x-3 gap-y-1">
                <h2 class="text-[0.8125rem] font-bold text-chrome touch:text-sm">
                  {{ group.ingredientName }}
                </h2>
                <span
                  v-if="group.isLow"
                  class="inline-flex items-center gap-1 border border-destructive/40 bg-destructive-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-destructive"
                >
                  <PhWarning class="size-3" weight="fill" aria-hidden="true" />
                  {{ group.isOut ? 'Out of stock' : 'Low stock' }}
                </span>
                <dl class="flex flex-wrap items-baseline gap-x-4 gap-y-0.5 text-xs tabular-nums sm:ml-auto touch:text-[0.8125rem]">
                  <div class="flex gap-1.5">
                    <dt class="text-muted-foreground">
                      Available
                    </dt>
                    <dd class="font-bold" :class="group.isLow ? 'text-destructive' : 'text-foreground'">
                      {{ formatQuantity(group.available, group.unit) }}
                    </dd>
                  </div>
                  <div class="flex gap-1.5">
                    <dt class="text-muted-foreground">
                      Reserved
                    </dt>
                    <dd class="font-semibold">
                      {{ formatQuantity(group.reserved, group.unit) }}
                    </dd>
                  </div>
                  <div class="flex gap-1.5">
                    <dt class="text-muted-foreground">
                      On hand
                    </dt>
                    <dd class="font-semibold">
                      {{ formatQuantity(group.onHand, group.unit) }}
                    </dd>
                  </div>
                  <div class="flex gap-1.5">
                    <dt class="text-muted-foreground">
                      Low at
                    </dt>
                    <dd class="font-semibold">
                      {{ formatQuantity(group.fallBelow, group.unit) }} or fewer
                    </dd>
                  </div>
                </dl>
              </div>
            </TableCell>
          </TableRow>
          <StockRow
            v-for="(item, index) in group.batches"
            :key="item.id"
            :item="item"
            :repeated-brand="index > 0 && group.batches[index - 1]!.brandName === item.brandName"
            @log="logFor = $event"
            @edit="editing = $event"
            @adjust="adjusting = $event"
            @split="splitting = $event"
          />
          <TableRow v-if="group.batches.length === 0">
            <TableCell colspan="9" class="text-xs text-muted-foreground touch:text-sm">
              No batches of this ingredient. Add one when stock arrives.
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </section>

    <CreateStockDialog v-if="creating" @close="creating = false" />
    <StockLogSheet v-if="logFor" :stock="live(logFor)!" @close="logFor = null" />
    <EditStockDialog v-if="editing" :stock="live(editing)!" @close="editing = null" />
    <AdjustStockDialog v-if="adjusting" :stock="live(adjusting)!" @close="adjusting = null" />
    <SplitStockDialog v-if="splitting" :stock="splitting" @close="splitting = null" />
  </StockArea>
</template>
