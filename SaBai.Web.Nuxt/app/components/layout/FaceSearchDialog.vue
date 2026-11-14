<script setup lang="ts">
import { PhUserFocus } from '@phosphor-icons/vue'
import type { PatientLinkTarget } from '~/utils/paths'

/**
 * Find a patient by face from anywhere. Capture a photo, pick a match and it
 * opens the station you are on (Vitals, Vision, Consults, Dental), or the record for that
 * patient — or any station of a visit via its journey tiles. Mount with v-if.
 */
const emit = defineEmits<{ close: [] }>()

const route = useRoute()
const station = computed(() => patientTargetFor(route))
const stationName = computed(() => (station.value === 'patient' ? 'record' : stationLabel(station.value)))

const image = ref<string | null>(null)
const { isOnline } = useSyncState()
const search = useFaceSearch(image)
const matches = computed(() => search.data.value ?? [])
const { remember } = useRecentPatients()

function closeOnLink(event: MouseEvent) {
  if ((event.target as HTMLElement | null)?.closest('a'))
    emit('close')
}

async function open(patientId: string | number, target: PatientLinkTarget | undefined = station.value) {
  const id = String(patientId)
  remember(id)
  emit('close')
  await navigateTo(patientLinkFor(target, id))
}
</script>

<template>
  <AppDialog title="Find patient by face" size="md" @close="emit('close')">
    <div class="flex flex-col gap-3">
      <StateMessage
        v-if="!isOnline"
        kind="warning"
        message="Face search requires an internet connection. Use Universal Search (Ctrl+K) to find patients offline."
      />
      <WebcamCapture v-model="image" :size="280" />

      <StateMessage v-if="search.isLoading.value" kind="loading" message="Searching for matches…" />
      <StateMessage
        v-else-if="search.isError.value"
        kind="error"
        :message="describeLoadError(search.error.value, 'Face search failed. Try again.')"
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
              <NuxtLink
                :to="paths.patientDetail(match.id)"
                class="block truncate text-sm font-semibold hover:text-primary hover:underline"
                :title="`Open ${match.name}'s record`"
                @click="emit('close')"
              >
                {{ match.name }}
              </NuxtLink>
              <p class="text-xs text-muted-foreground touch:text-sm">
                {{ match.displayId }}
                • <span class="font-semibold text-foreground">{{ Math.round(match.similarity * 100) }}% match</span>
              </p>
            </div>
            <Button variant="success" @click="open(match.id)">
              Open {{ stationName }}
            </Button>
          </div>
          <!-- Each journey tile opens that station for the visit; close on any link click. -->
          <div class="pl-[3.75rem]" @click="closeOnLink">
            <MatchVisits :patient-id="match.id" />
          </div>
        </li>
      </ol>
    </div>
  </AppDialog>
</template>
