<script setup lang="ts">
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

useHead({ title: "Patients · Sa'Bai" })

const { data, isLoading, isError } = usePatientsList()
const patients = computed(() => data.value?.patients ?? [])
</script>

<template>
  <div>
    <PageHeader
      title="Patients"
      description="Manage patient records and medical information."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Patients' }]"
    />
    <AppPanel title="Patient list" :description="isLoading ? undefined : `${patients.length} total`" flush>
      <StateMessage v-if="isError" kind="error" message="An error has occurred!" />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading patients..." />
      <StateMessage v-else-if="patients.length === 0" message="No patients found. Seed the database or add a new record." />
      <template v-else>
        <!-- Phones -->
        <ul class="divide-y divide-border md:hidden">
          <li v-for="patient in patients" :key="patient.id">
            <NuxtLink :to="paths.patientDetail(patient.id)" class="flex items-center gap-3 px-3 py-2.5 active:bg-accent">
              <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="44" alt="" />
              <div class="min-w-0 flex-1">
                <p class="truncate text-sm font-semibold">
                  {{ patient.name }}
                </p>
                <p class="text-xs text-muted-foreground">
                  <span class="font-mono">{{ formatPatientId(patient.id) }}</span> ·
                  <span class="capitalize">{{ patient.gender }}</span> · {{ calculateAge(patient.dateOfBirth) }} yrs
                </p>
                <div class="mt-1 flex flex-wrap gap-1">
                  <PatientCardBadges :patient="patient" />
                </div>
              </div>
            </NuxtLink>
          </li>
        </ul>
        <!-- Tablet / desktop -->
        <Table class="grid-table hidden md:table">
          <TableHeader>
            <TableRow>
              <TableHead class="w-20">
                ID
              </TableHead>
              <TableHead class="w-16">
                Photo
              </TableHead>
              <TableHead>Name</TableHead>
              <TableHead>Basic Info</TableHead>
              <TableHead>Status Flags</TableHead>
              <TableHead>Allergies</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="patient in patients" :key="patient.id">
              <TableCell class="font-mono">
                {{ formatPatientId(patient.id) || 'No ID' }}
              </TableCell>
              <TableCell>
                <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="40" alt="" />
              </TableCell>
              <TableCell>
                <NuxtLink :to="paths.patientDetail(patient.id)" class="font-semibold text-primary underline-offset-2 hover:underline">
                  {{ patient.name }}
                </NuxtLink>
              </TableCell>
              <TableCell>
                <span class="capitalize">{{ patient.gender }}</span> • {{ calculateAge(patient.dateOfBirth) }} yrs
                <span class="block text-muted-foreground">{{ patient.contactNo || '-' }}</span>
              </TableCell>
              <TableCell>
                <div class="flex flex-wrap gap-1">
                  <PatientCardBadges :patient="patient" />
                </div>
              </TableCell>
              <TableCell class="max-w-xs truncate">
                {{ patient.drugAllergy }}
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </template>
    </AppPanel>
  </div>
</template>
