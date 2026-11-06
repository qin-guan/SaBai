<script setup lang="ts">
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Medication Brands · Sa'Bai" })
const { data: brands, isLoading, isError } = useMedicationBrands()
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
    />
    <AppPanel title="Brands" flush>
      <StateMessage v-if="isError" kind="error" message="An error has occurred!" />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading brands..." />
      <StateMessage v-else-if="!brands || brands.length === 0" message="No medication brands found. Seed the database or add a new record." />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Brand ID</TableHead>
            <TableHead>Name</TableHead>
            <TableHead>Remarks</TableHead>
            <TableHead>Active Ingredient ID</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="brand in brands" :key="brand.id">
            <TableCell data-label="Brand ID" class="tabular-nums">
              {{ brand.id }}
            </TableCell>
            <TableCell data-label="Name" class="font-semibold">
              {{ brand.name }}
            </TableCell>
            <TableCell data-label="Remarks">{{ brand.remarks }}</TableCell>
            <TableCell data-label="Active Ingr. ID" class="tabular-nums">
              {{ brand.activeIngredientId }}
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>
  </div>
</template>
