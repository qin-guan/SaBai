<script setup lang="ts">
import { PhUserFocus } from '@phosphor-icons/vue'
import type { PatientLinkTarget } from '~/utils/paths'

/**
 * Find a patient by face from anywhere. Capture a photo, pick a match and it
 * opens the station you are on (Vitals, Vision, Consults, Dental) for that
 * patient — or any other section via the quick links. Mount with v-if.
 */
const emit = defineEmits<{ close: [] }>()

const route = useRoute()
const station = computed(() => route.meta.patientLink as PatientLinkTarget | undefined)
const stationLabel = computed(() => ({
  vitals: 'Vitals',
  vision: 'Vision',
  consults: 'Consults',
  dental: 'Dental',
  patient: 'record',
})[station.value ?? 'patient'])

const image = ref<string | null>(null)
const search = useFaceSearch(image)
const matches = computed(() => search.data.value ?? [])
const { remember } = useRecentPatients()

async function open(patientId: number, target: PatientLinkTarget | undefined = station.value) {
  remember(patientId)
  emit('close')
  await navigateTo(patientLinkFor(target, patientId))
}
</script>

<template>
  <AppDialog title="Find patient by face" size="md" @close="emit('close')">
    <div class="flex flex-col gap-3">
      <WebcamCapture v-model="image" :size="280" />

      <StateMessage v-if="search.isLoading.value" kind="loading" message="Searching for matches…" />
      <StateMessage
        v-else-if="search.isError.value"
        kind="error"
        :message="getApiErrorMessage(search.error.value, 'Face search failed. Try again.')"
      />
      <div v-else-if="image && matches.length === 0" class="flex flex-col items-center gap-2 py-4 text-center">
        <PhUserFocus class="size-10 text-muted-foreground/50" aria-hidden="true" />
        <p class="text-xs text-muted-foreground touch:text-sm" role="status">
          No matching patients found.
        </p>
      </div>
      <ol v-else-if="matches.length" class="divide-y divide-border border border-border" aria-label="Matching patients, best match first">
        <li v-for="match in matches" :key="match.id" class="flex flex-col gap-2 px-3 py-2">
          <div class="flex items-center gap-3">
            <PatientPhoto :patient-id="match.id" :has-photo="match.hasPhoto" :size="48" alt="" :name="match.name" />
            <div class="min-w-0 flex-1">
              <p class="truncate text-sm font-semibold">
                {{ match.name }}
              </p>
              <p class="text-xs text-muted-foreground touch:text-sm">
                {{ match.displayId }}
                • <span class="font-semibold text-foreground">{{ Math.round(match.similarity * 100) }}% match</span>
              </p>
            </div>
            <Button variant="success" @click="open(match.id)">
              Open {{ stationLabel }}
            </Button>
          </div>
          <StationLinks :patient-id="match.id" :active="station" @navigate="emit('close')" />
        </li>
      </ol>
    </div>
  </AppDialog>
</template>
