<script setup lang="ts">
import { PhUserFocus, PhUserPlus } from '@phosphor-icons/vue'
import { useMutation, useQuery } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import {
  saBaiAPIEndpointsPatientsSearchByFacePostEndpoint,
  saBaiAPIEndpointsVisitsCreatePostEndpoint,
  saBaiAPIEndpointsVisitsHasRecentGetEndpoint,
} from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'

/**
 * Searches the face index for patients matching the captured photo and opens a
 * visit for the chosen match (then goes to their vitals). Asks for confirmation
 * when the patient already had a visit opened recently.
 */
const props = defineProps<{ image: string }>()
const emit = defineEmits<{ register: [] }>()

useKeyShortcut('n', () => emit('register'))
const { selectedVillageCodeId, isLoading: villageLoading } = useVillageCode()
const duplicate = ref<{ patientId: number, existingVisitDate: Date | null } | null>(null)
const isCheckingDuplicate = ref(false)

const search = useQuery({
  queryKey: computed(() => queryKeys.faceSearch(props.image)),
  retry: false,
  queryFn: async () => {
    const image = await dataUrlToFile(props.image, 'face-search')
    const result = await saBaiAPIEndpointsPatientsSearchByFacePostEndpoint({
      ...apiConfig,
      body: { image },
      query: { maxMatches: 5 },
      throwOnError: true,
    }).unwrap()
    return result.matches
  },
})
const matches = computed(() => search.data.value ?? [])

const createVisit = useMutation({
  mutationFn: async (patientId: number) => {
    if (!selectedVillageCodeId.value)
      throw new Error('Please select a village code first.')
    return saBaiAPIEndpointsVisitsCreatePostEndpoint({
      ...apiConfig,
      body: { patientId, villageCodeId: selectedVillageCodeId.value },
      throwOnError: true,
    }).unwrap()
  },
})

const busy = computed(() => isCheckingDuplicate.value || createVisit.isPending.value)

async function submitCreateVisit(patientId: number) {
  try {
    await createVisit.mutateAsync(patientId)
    duplicate.value = null
    await navigateTo(paths.vitalsDetail(patientId))
  }
  catch (error) {
    toast.error(getApiErrorMessage(error, 'Failed to start a visit for this patient.'))
  }
}

async function select(patientId: number) {
  if (!selectedVillageCodeId.value) {
    toast.error('Please select a village code before starting a visit.')
    return
  }

  isCheckingDuplicate.value = true
  let recent: { hasRecentVisit: boolean, existingVisitDate?: string | null } | null
  try {
    recent = await saBaiAPIEndpointsVisitsHasRecentGetEndpoint({
      ...apiConfig,
      query: { patientId },
      throwOnError: true,
    }).unwrap()
  }
  catch {
    // Don't block visit creation if the duplicate check itself fails.
    recent = null
  }
  finally {
    isCheckingDuplicate.value = false
  }

  if (recent?.hasRecentVisit) {
    duplicate.value = {
      patientId,
      existingVisitDate: recent.existingVisitDate ? new Date(recent.existingVisitDate) : null,
    }
    return
  }

  await submitCreateVisit(patientId)
}
</script>

<template>
  <AppPanel title="Face matches" flush>
    <template #actions>
      <Button size="sm" @click="emit('register')">
        <PhUserPlus aria-hidden="true" /> Register New Patient <ShortcutHint class="hidden kb:inline-flex" keys="N" />
      </Button>
    </template>

    <StateMessage v-if="search.isLoading.value" kind="loading" message="Searching for matches…" />
    <StateMessage
      v-else-if="search.isError.value"
      kind="error"
      :message="getApiErrorMessage(search.error.value, 'Face search failed. Try again or register the patient manually.')"
    />
    <div v-else-if="matches.length === 0" class="flex flex-col items-center gap-2 py-8 text-center">
      <PhUserFocus class="size-12 text-muted-foreground/50" aria-hidden="true" />
      <p class="text-xs text-muted-foreground touch:text-sm" role="status">
        No matching patients found.
      </p>
    </div>
    <ol v-else class="divide-y divide-border" aria-label="Matching patients, best match first">
      <li v-for="match in matches" :key="match.id" class="flex flex-col gap-2 px-3 py-2">
        <div class="flex items-center gap-3">
          <PatientPhoto :patient-id="match.id" :has-photo="match.hasPhoto" :size="48" alt="" zoomable :name="match.name" />
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-semibold">
              {{ match.name }}
            </p>
            <p class="text-xs text-muted-foreground touch:text-sm">
              {{ match.displayId }}<template v-if="match.villageCode">
                • {{ match.villageCode }}
              </template>
              • <span class="font-semibold text-foreground">{{ Math.round(match.similarity * 100) }}% match</span>
            </p>
          </div>
          <Button
            variant="success"
            :disabled="busy || villageLoading"
            :aria-label="`Start a new visit for ${match.name}`"
            @click="select(match.id)"
          >
            {{ busy ? 'Starting…' : 'New Visit' }}
          </Button>
        </div>
        <div class="pl-[3.75rem]">
          <MatchVisits :patient-id="match.id" />
        </div>
      </li>
    </ol>
  </AppPanel>

  <AppDialog v-if="duplicate" title="Duplicate Visit" size="sm" @close="duplicate = null">
    <p>
      A visit already exists for this patient<template v-if="duplicate.existingVisitDate">
        at {{ duplicate.existingVisitDate.toLocaleTimeString() }}
      </template>. Are you sure you want to create another?
    </p>
    <template #footer>
      <Button variant="outline" @click="duplicate = null">
        Cancel
      </Button>
      <Button variant="success" :disabled="createVisit.isPending.value" @click="submitCreateVisit(duplicate.patientId)">
        {{ createVisit.isPending.value ? 'Creating...' : 'Confirm' }}
      </Button>
    </template>
  </AppDialog>
</template>
