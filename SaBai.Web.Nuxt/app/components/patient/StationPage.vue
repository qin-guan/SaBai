<script setup lang="ts">
import { PhPencilSimple } from '@phosphor-icons/vue'
import type { SaBaiAPIEndpointsPatientsGetResponse, SaBaiAPIEndpointsPatientsVisitsGetItem } from '@sabai/sdk/types'
import type { Crumb } from '~/components/app/PageHeader.vue'
import { Button } from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'

/**
 * Frame for a per-patient station screen: page header, patient banner (with
 * "Edit Patient Details"), visit selector, then the station form in the
 * default slot (rendered only once a visit is chosen).
 */
const props = defineProps<{
  station: { label: string, to: string }
  title: string
  patient: SaBaiAPIEndpointsPatientsGetResponse | undefined
  visits: SaBaiAPIEndpointsPatientsVisitsGetItem[] | undefined
  selectedVisit: SaBaiAPIEndpointsPatientsVisitsGetItem | null
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
  { label: props.station.label, to: props.station.to },
  { label: props.patient ? `${props.title} — ${props.patient.name}` : props.title },
])

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
      <PageHeader :title="`${props.title} — ${props.patient.name}`" :breadcrumbs="crumbs" />
      <PatientBanner :patient="props.patient">
        <template v-if="props.allowEdit" #actions>
          <Button variant="outline" @click="isEditing = true">
            <PhPencilSimple aria-hidden="true" /> Edit Patient Details
          </Button>
        </template>
      </PatientBanner>
      <VisitSelector
        v-model="visitId"
        :visits="props.visits"
        :summary-prefix="props.summaryPrefix"
        :empty-message="props.emptyMessage"
      />
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
