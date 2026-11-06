<script setup lang="ts">
import type { Component } from 'vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

/**
 * Station worklist (Vitals / Vision / Consults / Dental): every patient with
 * their latest visit's village and a single action that opens the station
 * form. A gridded table on wide screens, stacked rows on phones.
 */
const props = defineProps<{
  actionLabel: string
  actionIcon: Component
  linkFor: (patientId: number) => string
  emptyMessage: string
}>()

const { data, isLoading, isError } = usePatientsWithLatestVisit()
const patients = computed(() => data.value?.patients ?? [])
</script>

<template>
  <AppPanel title="Patients" :description="isLoading ? undefined : `${patients.length} total`" flush>
    <StateMessage v-if="isError" kind="error" message="An error has occurred!" />
    <StateMessage v-else-if="isLoading" kind="loading" message="Loading patients..." />
    <StateMessage v-else-if="patients.length === 0" :message="props.emptyMessage" />
    <template v-else>
      <!-- Phones: stacked rows, the whole row is the action -->
      <ul class="divide-y divide-border md:hidden">
        <li v-for="patient in patients" :key="patient.id">
          <NuxtLink
            :to="props.linkFor(patient.id)"
            class="flex items-center gap-3 px-3 py-2.5 active:bg-accent"
            :aria-label="`${props.actionLabel}: ${patient.name}`"
          >
            <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="44" alt="" />
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-semibold">
                {{ patient.name }}
              </p>
              <PatientCode
                :village-code="patient.villageCode"
                :village-color-hex="patient.villageColorHex"
                :id="patient.id"
                class="text-xs text-muted-foreground touch:text-sm"
              />
            </div>
            <span class="inline-flex items-center gap-1 text-xs font-semibold text-primary touch:text-sm">
              <component :is="props.actionIcon" class="size-4" aria-hidden="true" />
              {{ props.actionLabel }}
            </span>
          </NuxtLink>
        </li>
      </ul>

      <!-- Tablet / desktop: gridded table -->
      <Table class="grid-table hidden md:table">
        <TableHeader>
          <TableRow>
            <TableHead class="w-36">
              ID
            </TableHead>
            <TableHead class="w-16">
              Photo
            </TableHead>
            <TableHead>Name</TableHead>
            <TableHead class="w-48 text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="patient in patients" :key="patient.id">
            <TableCell>
              <PatientCode :village-code="patient.villageCode" :village-color-hex="patient.villageColorHex" :id="patient.id" />
            </TableCell>
            <TableCell>
              <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="40" alt="" />
            </TableCell>
            <TableCell class="font-semibold">
              {{ patient.name }}
            </TableCell>
            <TableCell class="text-right">
              <Button as-child size="sm" variant="success">
                <NuxtLink :to="props.linkFor(patient.id)" :aria-label="`${props.actionLabel}: ${patient.name}`">
                  <component :is="props.actionIcon" aria-hidden="true" />
                  {{ props.actionLabel }}
                </NuxtLink>
              </Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </template>
  </AppPanel>
</template>
