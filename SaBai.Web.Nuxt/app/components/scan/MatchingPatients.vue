<script setup lang="ts">
import { PhUserFocus, PhUserPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'

/**
 * Searches the face index for patients matching the captured photo; each match
 * links to their record, can start a visit (see StartVisitButton), and lists
 * their visits with each station a click away.
 */
const props = defineProps<{ image: string }>()
const emit = defineEmits<{ register: [] }>()

useKeyShortcut('n', () => emit('register'))
const search = useFaceSearch(() => props.image)
const matches = computed(() => search.data.value ?? [])
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
      :message="describeLoadError(search.error.value, 'Face search failed. Try again or register the patient manually.')"
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
            <NuxtLink
              :to="paths.patientDetail(match.id)"
              class="block truncate text-sm font-semibold hover:text-primary hover:underline"
              :title="`Open ${match.name}'s record`"
            >
              {{ match.name }}
            </NuxtLink>
            <p class="text-xs text-muted-foreground touch:text-sm">
              {{ match.displayId }}<template v-if="match.villageCode">
                • {{ match.villageCode }}
              </template>
              • <span class="font-semibold text-foreground">{{ Math.round(match.similarity * 100) }}% match</span>
            </p>
          </div>
          <StartVisitButton :patient-id="match.id" :name="match.name" />
        </div>
        <div class="pl-[3.75rem]">
          <MatchVisits :patient-id="match.id" />
        </div>
      </li>
    </ol>
  </AppPanel>

</template>
