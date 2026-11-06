<script setup lang="ts">
import { PhFlask, PhPlus, PhTag } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Stock · Sa'Bai" })

const { data: stock, isLoading, isError } = useMedicationStock()
const creating = ref(false)
const splitting = ref<MedicationStockRow | null>(null)
</script>

<template>
  <div>
    <PageHeader
      title="Medication Stock"
      description="Manage medication stock."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Medication Stock' }]"
    >
      <template #actions>
        <Button variant="outline" as-child>
          <NuxtLink :to="paths.medicationBrands()">
            <PhTag aria-hidden="true" /> Manage Brands
          </NuxtLink>
        </Button>
        <Button variant="outline" as-child>
          <NuxtLink :to="paths.medicationActiveIngredients()">
            <PhFlask aria-hidden="true" /> Manage Active Ingredients
          </NuxtLink>
        </Button>
        <Button @click="creating = true">
          <PhPlus aria-hidden="true" /> Add Stock
        </Button>
      </template>
    </PageHeader>

    <AppPanel title="Stock" :description="stock ? `${stock.length} lines · low stock highlighted red` : undefined" flush>
      <StateMessage v-if="isError" kind="error" message="An error has occurred!" />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading stock..." />
      <StateMessage v-else-if="!stock || stock.length === 0" message="No stock found. Seed the database or add a new record." />
      <Table v-else class="grid-table">
        <TableHeader>
          <TableRow>
            <TableHead>Active Ingredient</TableHead>
            <TableHead>Brand</TableHead>
            <TableHead>Location</TableHead>
            <TableHead class="text-right">
              Quantity
            </TableHead>
            <TableHead>Expiry</TableHead>
            <TableHead>State</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <StockRow v-for="item in stock" :key="item.id" :item="item" @split="splitting = $event" />
        </TableBody>
      </Table>
    </AppPanel>

    <CreateStockDialog v-if="creating" @close="creating = false" />
    <SplitStockDialog v-if="splitting" :stock="splitting" @close="splitting = null" />
  </div>
</template>
