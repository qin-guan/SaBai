<script setup lang="ts">
import { PhPlus, PhTag } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Active Ingredients · Sa'Bai" })
const { data: ingredients, isPending, isError, error } = useMedicationIngredients()
const adding = ref(false)
</script>

<template>
  <div>
    <PageHeader
      title="Medication Active Ingredient"
      description="Manage medication active ingredients."
      :breadcrumbs="[
        { label: 'Home', to: '/' },
        { label: 'Medication Stock', to: paths.medicationStock() },
        { label: 'Medication Active Ingredients' },
      ]"
    >
      <template #actions>
        <Button variant="outline" as-child>
          <NuxtLink :to="paths.medicationBrands()">
            <PhTag aria-hidden="true" /> Manage Brands
          </NuxtLink>
        </Button>
        <Button @click="adding = true">
          <PhPlus aria-hidden="true" /> Add Active Ingredient
        </Button>
      </template>
    </PageHeader>
    <AppPanel title="Active ingredients" flush>
      <StateMessage v-if="!ingredients && isError" kind="error" :message="describeLoadError(error, 'An error has occurred!')" />
      <StateMessage v-else-if="!ingredients && isPending" kind="loading" message="Loading active ingredients..." />
      <StateMessage v-else-if="!ingredients || ingredients.length === 0" message="No active ingredient found. Seed the database or add a new record." />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Active Ingredient ID</TableHead>
            <TableHead>Active Ingredient Name</TableHead>
            <TableHead>Unit of Measurement</TableHead>
            <TableHead>Fall Below</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <IngredientRow v-for="ingredient in ingredients" :key="ingredient.id" :ingredient="ingredient" />
        </TableBody>
      </Table>
    </AppPanel>
    <AddIngredientDialog v-if="adding" @close="adding = false" />
  </div>
</template>
