<script setup lang="ts">
import { PhPencilSimple } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

const patientId = useRouteId('id')
const { data: patient, isLoading, isRefetching } = usePatient(patientId)
const { data: visitsData, isLoading: visitsLoading } = usePatientVisits(patientId)
const visits = computed(() => visitsData.value?.visits ?? [])
const isEditing = ref(false)
useRememberPatient(patientId)

useHead({ title: () => `${patient.value?.name ?? 'Patient'} · Sa'Bai` })

const details = computed(() => {
  const p = patient.value
  if (!p)
    return []
  return [
    { label: 'ID', value: p.id.toString() },
    { label: 'Name', value: p.name },
    { label: 'Identification Number', value: p.identificationNumber || '-' },
    { label: 'Contact Number', value: p.contactNo || '-' },
    { label: 'Gender', value: p.gender },
    { label: 'Date of Birth', value: new Date(p.dateOfBirth).toLocaleDateString() },
    { label: 'Age', value: `${calculateAge(p.dateOfBirth)} years` },
    { label: 'Drug Allergy', value: p.drugAllergy },
    { label: 'POOR', value: String(p.hasPoorCard) },
    { label: 'BS2', value: String(p.hasBS2Card) },
    { label: 'Sabai Card', value: String(p.hasSabaiCard) },
  ]
})
</script>

<template>
  <div>
    <div v-if="!patientId || isLoading || isRefetching" class="flex flex-col gap-2" role="status" aria-label="Loading patient">
      <Skeleton class="h-16 w-full" />
      <Skeleton class="h-48 w-full" />
    </div>
    <AppPanel v-else-if="!patient" title="Patient not found">
      <p>No patient exists for ID: {{ patientId }}</p>
    </AppPanel>
    <template v-else>
      <PageHeader
        :title="patient.name"
        description="Simple patient profile details"
        :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Patients', to: paths.patient() }, { label: patient.name }]"
      />
      <PatientBanner :patient="patient">
        <template #actions>
          <Button @click="isEditing = true">
            <PhPencilSimple aria-hidden="true" /> Edit Patient Details
          </Button>
        </template>
      </PatientBanner>

      <div class="grid grid-cols-1 gap-3 xl:grid-cols-2">
        <AppPanel title="Demographics" flush>
          <dl class="grid grid-cols-1 sm:grid-cols-2">
            <div
              v-for="detail in details"
              :key="detail.label"
              class="grid grid-cols-[10rem_1fr] gap-2 border-b border-border px-3 py-1.5 sm:odd:border-r"
            >
              <dt class="text-xs font-semibold text-muted-foreground touch:text-sm">
                {{ detail.label }}
              </dt>
              <dd class="break-words">
                {{ detail.value }}
              </dd>
            </div>
          </dl>
        </AppPanel>

        <AppPanel title="Visit History" :description="visitsLoading ? undefined : `${visits.length} visits`" flush>
          <StateMessage v-if="visitsLoading" kind="loading" message="Loading visits..." />
          <StateMessage v-else-if="visits.length === 0" message="No visits recorded for this patient." />
          <ol v-else class="divide-y divide-border">
            <li v-for="(visit, index) in visits" :key="visit.id" class="flex items-center justify-between gap-3 px-3 py-2">
              <div class="flex items-center gap-2">
                <span
                  class="size-3 shrink-0"
                  :style="{ backgroundColor: visit.villageColorHex }"
                  :title="`Village: ${visit.villageName}`"
                  aria-hidden="true"
                />
                <div>
                  <p class="font-semibold">
                    Visit #{{ visits.length - index }}
                  </p>
                  <p class="text-xs text-muted-foreground touch:text-sm">
                    {{ visit.villageName }}
                  </p>
                </div>
              </div>
              <div class="text-right">
                <p class="font-semibold">
                  {{ new Date(visit.date).toLocaleDateString() }}
                </p>
                <p class="text-xs text-muted-foreground touch:text-sm">
                  {{ new Date(visit.date).toLocaleTimeString() }}
                </p>
              </div>
            </li>
          </ol>
        </AppPanel>
      </div>

      <EditPatientDialog v-if="isEditing" :patient="patient" @close="isEditing = false" />
    </template>
  </div>
</template>
