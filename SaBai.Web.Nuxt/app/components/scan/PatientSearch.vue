<script setup lang="ts">
import { PhMagnifyingGlass } from '@phosphor-icons/vue'
import { Input } from '@/components/ui/input'

/**
 * Find a patient without the camera — by name, any form of their ID, ID
 * number, phone or village (the same index as the universal search) — and
 * start a visit for them or open their record.
 */
const query = ref('')
const debounced = useDebouncedRef(query, 120)

const { data, isLoading, isError } = usePatientsWithLatestVisit()
const patients = computed(() => data.value?.patients ?? [])
const byId = computed(() => new Map(patients.value.map(p => [p.id, p])))
const index = computed(() => buildPatientIndex(patients.value))

const MAX_RESULTS = 10
const hits = computed(() =>
  searchIndex(index.value, debounced.value)
    .slice(0, MAX_RESULTS)
    .map(r => byId.value.get(String(r.id)))
    .filter(p => !!p),
)
const lastVisit = (date: string) => `Last visit ${formatVisitDate(date)}`
</script>

<template>
  <AppPanel title="Find patient" flush>
    <div class="border-b border-border p-3">
      <div class="relative">
        <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <Input
          v-model="query"
          type="search"
          class="pl-8"
          placeholder="Name, patient ID, ID number, phone or tag"
          aria-label="Search patients to start a visit"
        />
      </div>
    </div>
    <StateMessage v-if="isError" kind="error" message="Patients could not be loaded. Check the connection and reload." />
    <StateMessage v-else-if="isLoading && query.trim()" kind="loading" message="Loading patients…" />
    <template v-else-if="debounced.trim()">
      <p v-if="hits.length === 0" class="px-3 py-6 text-center text-xs text-muted-foreground touch:text-sm" role="status">
        No patients match “{{ debounced.trim() }}”.
      </p>
      <ol v-else class="divide-y divide-border" aria-label="Matching patients">
        <li v-for="p in hits" :key="p!.id" class="flex flex-col gap-2 px-3 py-2">
          <div class="flex items-center gap-3">
            <PatientPhoto :patient-id="p!.id" :has-photo="p!.hasPhoto" :size="40" alt="" zoomable :name="p!.name" />
            <div class="min-w-0 flex-1">
              <NuxtLink :to="paths.patientDetail(p!.id)" class="block truncate text-sm font-semibold hover:text-primary hover:underline">
                {{ p!.name }}
              </NuxtLink>
              <p class="flex flex-wrap items-center gap-x-1.5 text-xs text-muted-foreground touch:text-sm">
                <PatientCode :id="p!.number ?? p!.id" :village-code="p!.villageCode" :village-color-hex="p!.villageColorHex" />
                <span aria-hidden="true">·</span>
                <span>{{ calculateAge(p!.dateOfBirth) }} y <span class="capitalize">{{ p!.gender }}</span></span>
                <template v-if="p!.tags?.length">
                  <span aria-hidden="true">·</span>
                  <span>{{ p!.tags.map(t => `#${t}`).join(' ') }}</span>
                </template>
                <template v-if="p!.lastVisitDate">
                  <span aria-hidden="true">·</span>
                  <span>{{ lastVisit(p!.lastVisitDate) }}</span>
                </template>
              </p>
            </div>
            <StartVisitButton :patient-id="p!.id" :name="p!.name" size="sm" />
          </div>
          <div v-if="p!.lastVisitJourney && p!.lastVisitId" class="pl-[3.25rem]">
            <JourneyTracker :journey="p!.lastVisitJourney" :patient-id="p!.id" :visit-id="p!.lastVisitId" size="compact" />
          </div>
        </li>
      </ol>
    </template>
  </AppPanel>
</template>
