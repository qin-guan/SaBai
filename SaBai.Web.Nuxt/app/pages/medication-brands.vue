<script setup lang="ts">
import { PhFlask, PhPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Brands · Sa'Bai" })
const { data: brands, isLoading, isError } = useMedicationBrands()
// For the active ingredient names and the ingredient picker.
const { data: ingredients, isLoading: ingredientsLoading } = useMedicationIngredients()
const adding = ref(false)
</script>

<template>
  <div>
    <PageHeader
      title="Medication Brands"
      description="Manage medication brands."
      :breadcrumbs="[
        { label: 'Home', to: '/' },
        { label: 'Medication Stock', to: paths.medicationStock() },
        { label: 'Medication Brands' },
      ]"
    >
      <template #actions>
        <Button variant="outline" as-child>
          <NuxtLink :to="paths.medicationActiveIngredients()">
            <PhFlask aria-hidden="true" /> Manage Active Ingredients
          </NuxtLink>
        </Button>
        <Button :disabled="ingredientsLoading" @click="adding = true">
          <PhPlus aria-hidden="true" /> Add Brand
        </Button>
      </template>
    </PageHeader>
    <AppPanel title="Brands" flush>
      <StateMessage v-if="isError" kind="error" message="An error has occurred!" />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading brands..." />
      <StateMessage v-else-if="!brands || brands.length === 0" message="No medication brands found. Seed the database or add a new record." />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Brand ID</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Active Ingredient</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <BrandRow v-for="brand in brands" :key="brand.id" :brand="brand" :ingredients="ingredients ?? []" />
        </TableBody>
      </Table>
    </AppPanel>
    <AddBrandDialog v-if="adding" :ingredients="ingredients ?? []" @close="adding = false" />
  </div>
</template>
