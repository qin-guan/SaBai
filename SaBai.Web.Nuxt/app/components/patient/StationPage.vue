<script setup lang="ts">
import { PhCaretRight, PhIdentificationCard, PhPencilSimple } from '@phosphor-icons/vue'
import type { LocalPatientSummary, LocalVisitSummary } from '~/db/types'
import type { Crumb } from '~/components/app/PageHeader.vue'
import type { StationKey } from '~/utils/journey'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * Frame for a per-patient station screen: page header, patient banner (with
 * "Edit details" and a link to the full record), visit selector, the
 * visit's journey (where the patient has been and goes next — each step opens
 * that station on the same visit), then the station form in the default slot
 * (rendered only once a visit is chosen).
 */
const props = defineProps<{
  station: StationKey
  title: string
  patient: LocalPatientSummary | null | undefined
  visits: LocalVisitSummary[] | undefined
  selectedVisit: LocalVisitSummary | null
  isLoading: boolean
  summaryPrefix?: string
  emptyMessage: string
  chooseMessage: string
  allowEdit?: boolean
}>()
const visitId = defineModel<string>('visitId', { required: true })
const isEditing = ref(false)

const crumbs = computed<Crumb[]>(() => [
  { label: 'Home', to: '/' },
  { label: stationLabel(props.station), to: paths.patient(props.station) },
  { label: props.patient?.name ?? props.title },
])

const journeyId = useId()
const { open: journeyOpen, toggle: toggleJourney } = usePersistedToggle('station-journey', false)
const journeySummary = computed(() => {
  const journey = props.selectedVisit?.journey
  if (!journey)
    return ''
  const next = nextStep(journey)
  const { done, total } = requiredProgress(journey)
  return next ? `Next: ${next.label} · ${done} of ${total} required done` : 'All needed stations done'
})

useHead({ title: () => `${props.title}${props.patient ? ` — ${props.patient.name}` : ''} · Sa'Bai` })
</script>

<template>
  <div>
    <div v-if="props.isLoading" class="flex flex-col gap-2" role="status" aria-label="Loading patient and visits...">
      <Skeleton class="h-6 w-64" />
      <Skeleton class="h-16 w-full" />
      <Skeleton class="h-10 w-full" />
      <Skeleton class="h-64 w-full" />
    </div>
    <StateMessage v-else-if="!props.patient" kind="error" message="Patient not found" />
    <template v-else>
      <PageHeader :title="`${props.title} — ${props.patient.name}`" :breadcrumbs="crumbs" hide-title />
      <PatientBanner :patient="props.patient" :to="paths.patientDetail(props.patient.id)">
        <template #actions>
          <Button v-if="props.allowEdit" variant="outline" @click="isEditing = true">
            <PhPencilSimple aria-hidden="true" /> Edit Patient Details
          </Button>
          <Button as-child variant="outline">
            <NuxtLink :to="paths.patientDetail(props.patient.id)">
              <PhIdentificationCard aria-hidden="true" /> Patient record
            </NuxtLink>
          </Button>
          <StartVisitButton :patient-id="props.patient.id" :name="props.patient.name" />
        </template>
      </PatientBanner>
      <VisitSelector
        v-model="visitId"
        :visits="props.visits"
        :summary-prefix="props.summaryPrefix"
        :empty-message="props.emptyMessage"
      >
        <div v-if="props.selectedVisit" class="flex min-w-0 flex-col gap-2">
          <!-- Collapsed by default: one summary line; open for the clickable steps. -->
          <button
            type="button"
            class="flex min-w-0 cursor-pointer items-center gap-2 text-left text-xs hover:underline touch:min-h-11 touch:text-sm"
            :aria-expanded="journeyOpen"
            :aria-controls="journeyId"
            @click="toggleJourney"
          >
            <PhCaretRight weight="bold" class="size-3.5 shrink-0 transition-transform motion-reduce:transition-none" :class="journeyOpen && 'rotate-90'" aria-hidden="true" />
            <span class="font-semibold">Journey</span>
            <span class="truncate text-muted-foreground">{{ journeySummary }}</span>
          </button>
          <JourneyTracker
            v-show="journeyOpen"
            :id="journeyId"
            :journey="props.selectedVisit.journey"
            :patient-id="props.patient.id"
            :visit-id="props.selectedVisit.id"
            :active="props.station"
          />
        </div>
      </VisitSelector>
      <slot v-if="props.selectedVisit" :visit="props.selectedVisit" :patient="props.patient" />
      <div
        v-else-if="props.visits && props.visits.length > 0"
        class="border-2 border-dashed border-border py-12 text-center text-xs font-medium text-muted-foreground touch:text-sm"
      >
        {{ props.chooseMessage }}
      </div>
      <EditPatientDialog v-if="isEditing" :patient="props.patient" @close="isEditing = false" />
    </template>
  </div>
</template>
