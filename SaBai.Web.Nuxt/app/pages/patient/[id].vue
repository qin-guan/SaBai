<script setup lang="ts">
import { PhArrowRight } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * The patient record: who they are, where today's visit is in the clinic
 * (vitals → vision → consult → dental → pharmacy, each step opens that
 * station), every past visit's journey, and their details — edited in place
 * like the station forms.
 */
const patientId = useRouteId('id')
const { data: patient, isLoading } = usePatient(patientId)
const { data: visitsData, isLoading: visitsLoading } = usePatientVisits(patientId)
const visits = computed(() => visitsData.value?.visits ?? [])
const current = computed(() => visits.value[0])
const next = computed(() => (current.value ? nextStep(current.value.journey) : null))
const progress = computed(() => (current.value ? requiredProgress(current.value.journey) : null))
useRememberPatient(patientId)

useHead({ title: () => `${patient.value?.name ?? 'Patient'} · Sa'Bai` })
</script>

<template>
  <div>
    <div v-if="!patientId || isLoading" class="flex flex-col gap-2" role="status" aria-label="Loading patient">
      <Skeleton class="h-16 w-full" />
      <Skeleton class="h-48 w-full" />
    </div>
    <AppPanel v-else-if="!patient" title="Patient not found">
      <p>No patient exists for ID: {{ patientId }}</p>
    </AppPanel>
    <template v-else>
      <PageHeader
        :title="patient.name"
        :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Patients', to: paths.patient() }, { label: patient.name }]"
        hide-title
      />
      <PatientBanner :patient="patient" />

      <div class="grid grid-cols-1 gap-3 xl:grid-cols-[minmax(0,1fr)_minmax(0,1fr)] xl:items-start">
        <div class="flex min-w-0 flex-col gap-3">
          <AppPanel
            title="Current visit"
            :description="current ? `${formatVisitDate(current.date)} · ${current.villageName}` : undefined"
          >
            <template #actions>
              <StartVisitButton :patient-id="patient.id" :name="patient.name" size="sm" />
            </template>
            <StateMessage v-if="visitsLoading" kind="loading" message="Loading visits..." />
            <p v-else-if="!current" class="text-xs text-muted-foreground touch:text-sm">
              No visits yet. Use New Visit to start one.
            </p>
            <div v-else class="flex flex-col gap-3">
              <JourneyTracker :journey="current.journey" :patient-id="patient.id" :visit-id="current.id" />
              <div class="flex flex-wrap items-center gap-2">
                <Button v-if="next" as-child variant="success">
                  <NuxtLink :to="patientLinkFor(next.key, patient.id, current.id)">
                    Next: {{ next.label }} <PhArrowRight aria-hidden="true" />
                  </NuxtLink>
                </Button>
                <p v-else class="text-xs font-semibold text-success touch:text-sm">
                  Visit complete — all needed stations done.
                </p>
                <p v-if="progress" class="text-xs text-muted-foreground touch:text-sm">
                  {{ progress.done }} of {{ progress.total }} required stations done
                </p>
              </div>
            </div>
          </AppPanel>

          <AppPanel title="Visit history" :description="visitsLoading ? undefined : `${visits.length} visits`" flush>
            <StateMessage v-if="visitsLoading" kind="loading" message="Loading visits..." />
            <StateMessage v-else-if="visits.length === 0" message="No visits recorded for this patient." />
            <ol v-else class="divide-y divide-border">
              <li v-for="(visit, index) in visits" :key="visit.id" class="flex flex-col gap-1.5 px-3 py-2">
                <div class="flex items-center justify-between gap-3">
                  <div class="flex items-center gap-2">
                    <span
                      class="size-3 shrink-0"
                      :style="{ backgroundColor: visit.villageColorHex }"
                      :title="`Village: ${visit.villageName}`"
                      aria-hidden="true"
                    />
                    <p class="font-semibold">
                      Visit #{{ visits.length - index }}
                      <span class="font-normal text-muted-foreground">· {{ visit.villageName }}</span>
                    </p>
                  </div>
                  <p class="text-right text-xs text-muted-foreground touch:text-sm">
                    {{ formatVisitDate(visit.date) }}
                  </p>
                </div>
                <JourneyTracker :journey="visit.journey" :patient-id="patient.id" :visit-id="visit.id" size="compact" />
              </li>
            </ol>
          </AppPanel>
        </div>

        <PatientDetailsForm :key="patient.id" :patient="patient" />
      </div>
    </template>
  </div>
</template>
