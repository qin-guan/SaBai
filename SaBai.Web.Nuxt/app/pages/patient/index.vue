<script setup lang="ts">
import { PhCaretRight, PhMagnifyingGlass, PhScan, PhWarning } from '@phosphor-icons/vue'
import { useIntervalFn } from '@vueuse/core'
import type { StationKey } from '~/utils/journey'
import type { StationThroughput } from '~/utils/waitTime'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * The patient list — one page for every station.
 *
 * Unfiltered it is the front desk's overview: everyone seen today and where
 * each is in their journey (vitals → vision → consult → dental → pharmacy).
 * With `?station=vitals` (the sidebar's station links) it becomes that
 * station's worklist: who still needs it, earliest arrival first, and a row
 * that opens the station's form on the patient's current visit.
 *
 * The station cards show how backlogged each station is: how many still need
 * it and roughly how long someone sent there now would wait. The pace comes
 * from each station's measured throughput (records saved in the last hour, or
 * today), falling back to this device's form-fill times — see
 * utils/waitTime.ts and utils/serviceTime.ts.
 */
const route = useRoute()
const station = computed<StationKey | undefined>(() =>
  isStationKey(route.query.station) ? route.query.station : undefined,
)
type Show = 'waiting' | 'done' | 'all'
const show = computed<Show>(() =>
  route.query.show === 'done' || route.query.show === 'all' ? route.query.show : 'waiting',
)
const scope = computed<'today' | 'all'>(() => (route.query.scope === 'all' ? 'all' : 'today'))

useHead({ title: () => `${station.value ? stationLabel(station.value) : 'Patients'} · Sa'Bai` })

/** Changes one query parameter, keeping the others (defaults are left out of the URL). */
function queryWith(key: string, value: string | undefined) {
  const query = { ...route.query, [key]: value }
  for (const k of Object.keys(query)) {
    if (query[k] === undefined || query[k] === '')
      delete query[k]
  }
  return { path: paths.patient(), query }
}

const filter = ref('')
const { data, isLoading, isError } = usePatientsWithLatestVisit()
type Row = NonNullable<typeof data.value>['patients'][number]

const inScope = computed(() => {
  const all = data.value?.patients ?? []
  return scope.value === 'all' ? all : all.filter(p => p.lastVisitDate && isSameDay(p.lastVisitDate))
})

const matchesFilter = (p: Row) => {
  const needle = filter.value.trim().toLowerCase()
  if (!needle)
    return true
  return [p.name, p.displayId, p.filterString, p.identificationNumber ?? '', p.contactNo ?? '']
    .some(v => v.toLowerCase().includes(needle))
}

/** How many patients each station still has to see. */
const waitingCounts = computed(() => {
  const counts = {} as Record<StationKey, number>
  for (const { key } of STATIONS)
    counts[key] = inScope.value.filter(p => p.lastVisitJourney && isWaitingFor(p.lastVisitJourney, key)).length
  return counts
})

// Throughput per station, refreshed each minute and whenever data changes.
const now = shallowRef(new Date())
useIntervalFn(() => (now.value = new Date()), 60_000)
const throughput = computed(() => {
  const journeys = (data.value?.patients ?? []).flatMap(p => (p.lastVisitJourney ? [p.lastVisitJourney] : []))
  const out = {} as Record<StationKey, StationThroughput>
  for (const { key } of STATIONS) {
    out[key] = withServiceTimeFallback(
      stationThroughput(journeys, key, now.value.getTime()),
      typicalServiceMinutes(key, now.value.getTime()).minutes,
    )
  }
  return out
})
const basisNote = (t: StationThroughput) =>
  t.basis === 'form timing'
    ? `About ${formatRate(t.perMinute)}, from how long forms take on this device`
    : t.basis
      ? `About ${formatRate(t.perMinute)}, from ${t.samples} patients seen ${t.basis === 'today' ? 'today' : 'in the last hour'}`
      : 'Not enough patients seen yet to estimate a wait'

const rows = computed(() => {
  const s = station.value
  let list = inScope.value.filter(matchesFilter)
  if (!s)
    return list
  // A station can only work on a visit.
  list = list.filter(p => p.lastVisitJourney)
  if (show.value !== 'all') {
    const waiting = show.value === 'waiting'
    list = list.filter(p => isWaitingFor(p.lastVisitJourney!, s) === waiting)
  }
  if (show.value === 'waiting')
    list = [...list].sort((a, b) => new Date(a.lastVisitDate!).getTime() - new Date(b.lastVisitDate!).getTime())
  return list
})

/** The summary row: all patients, then each station's backlog and wait for someone sent now. */
const stationCards = computed(() => [
  {
    key: undefined as StationKey | undefined,
    label: 'All',
    count: inScope.value.length,
    detail: scope.value === 'today' ? 'In clinic today' : 'Patients',
    note: '',
  },
  ...STATIONS.map(({ key, label }) => {
    const t = throughput.value[key]
    const waiting = waitingCounts.value[key]
    const minutes = stationWaitMinutes(t, waiting)
    const detail = !waiting
      ? 'No wait'
      : minutes !== null
        ? `${formatWait(minutes)} wait`
        : 'Waiting'
    return { key: key as StationKey | undefined, label, count: waiting, detail, note: basisNote(t) }
  }),
])

const SHOW_OPTIONS: { value: Show, label: string }[] = [
  { value: 'waiting', label: 'Waiting' },
  { value: 'done', label: 'Done' },
  { value: 'all', label: 'All' },
]

/**
 * Where a row leads: in a station worklist, that station's form for the
 * patient's current visit; otherwise the patient record, which shows the
 * journey and the next step.
 */
const rowLink = (p: Row) =>
  station.value ? patientLinkFor(station.value, p.id, p.lastVisitId ?? undefined) : paths.patientDetail(p.id)

const emptyMessage = computed(() => {
  if (filter.value.trim())
    return `No patients match “${filter.value.trim()}”.`
  if (station.value && show.value === 'waiting')
    return `Nobody is waiting for ${stationLabel(station.value)}${scope.value === 'today' ? ' today' : ''}.`
  if (scope.value === 'today')
    return 'No visits today yet. Start one from Scan Face (by face or search).'
  return 'No patients found. Register a patient from Scan Face.'
})

const timeOf = (date: string) => new Date(date).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
const dateOf = (date: string) => (isSameDay(date) ? `Arrived ${timeOf(date)}` : `Last visit ${new Date(date).toLocaleDateString()}`)

// One grid for the heading and every row, so columns line up.
const gridCols = 'md:grid-cols-[2.75rem_minmax(0,1fr)_minmax(0,20rem)_1rem]'
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      :title="station ? stationLabel(station) : 'Patients'"
      :breadcrumbs="station
        ? [{ label: 'Home', to: '/' }, { label: 'Patients', to: paths.patient() }, { label: stationLabel(station) }]
        : [{ label: 'Home', to: '/' }, { label: 'Patients' }]"
      class="mb-0!"
    >
      <template #actions>
        <Button as-child variant="outline">
          <NuxtLink :to="paths.scanFace()">
            <PhScan aria-hidden="true" /> Start visit / register
          </NuxtLink>
        </Button>
      </template>
    </PageHeader>

    <!--
      Summary first: one card per station picks that worklist (the same page,
      filtered) and shows how many are waiting and roughly how long someone
      sent there now would wait.
    -->
    <nav aria-label="Stations" class="grid grid-cols-2 gap-px border border-border bg-border sm:grid-cols-3 lg:grid-cols-6">
      <NuxtLink
        v-for="card in stationCards"
        :key="card.key ?? 'all'"
        :to="queryWith('station', card.key)"
        class="flex flex-col gap-1 bg-card px-3 py-2 hover:bg-accent focus-visible:bg-accent"
        :class="station === card.key && 'bg-accent shadow-[inset_0_-3px_0_var(--color-primary)]'"
        :aria-current="station === card.key ? 'page' : undefined"
        :title="card.note"
      >
        <span class="flex items-baseline justify-between gap-2">
          <span class="text-[0.6875rem] font-bold tracking-wide text-chrome uppercase touch:text-xs">{{ card.label }}</span>
          <span class="text-lg leading-none font-bold tabular-nums" :class="card.count === 0 && 'text-muted-foreground'">
            {{ isLoading ? '–' : card.count }}
          </span>
        </span>
        <span class="text-xs text-muted-foreground tabular-nums touch:text-sm">{{ card.detail }}</span>
        <span v-if="card.note" class="sr-only">{{ card.note }}</span>
      </NuxtLink>
    </nav>

    <AppPanel
      :title="station ? `${SHOW_OPTIONS.find(o => o.value === show)!.label} for ${stationLabel(station)}` : (scope === 'today' ? 'In clinic today' : 'All patients')"
      :description="isLoading ? undefined : `${rows.length}`"
      flush
    >
      <!-- Controls sit on the list they filter. -->
      <template #actions>
        <div class="relative w-full sm:w-56">
          <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2 size-3.5 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
          <Input v-model="filter" type="search" class="h-7 bg-card pl-7 text-xs touch:h-11" placeholder="Name, ID or phone" aria-label="Filter patients" />
        </div>
        <Segmented
          v-if="station"
          label="Show"
          :options="SHOW_OPTIONS.map(o => ({ ...o, to: queryWith('show', o.value === 'waiting' ? undefined : o.value) }))"
          :value="show"
        />
        <Segmented
          label="Visits"
          :options="[
            { value: 'today', label: 'Today', to: queryWith('scope', undefined) },
            { value: 'all', label: 'All time', to: queryWith('scope', 'all') },
          ]"
          :value="scope"
        />
      </template>

      <StateMessage v-if="isError" kind="error" message="Patients could not be loaded. Check the connection and reload." />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading patients..." />
      <div v-else-if="rows.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center">
        <p class="text-xs text-muted-foreground touch:text-sm" role="status">
          {{ emptyMessage }}
        </p>
        <Button v-if="scope === 'today' && !filter.trim()" as-child size="sm" variant="outline">
          <NuxtLink :to="queryWith('scope', 'all')">
            Show all time
          </NuxtLink>
        </Button>
      </div>
      <template v-else>
        <!-- Column headings (wide screens); rows share the same grid. -->
        <div
          class="hidden border-b border-border bg-muted/50 px-3 py-1 text-[0.6875rem] font-semibold tracking-wide text-muted-foreground uppercase md:grid md:gap-x-4"
          :class="gridCols"
          aria-hidden="true"
        >
          <span />
          <span>Patient</span>
          <span>Journey</span>
          <span />
        </div>
        <ul class="divide-y divide-border">
          <li v-for="patient in rows" :key="patient.id">
            <!-- The whole row is the link: one target per patient, no repeated buttons. -->
            <NuxtLink
              :to="rowLink(patient)"
              class="group grid grid-cols-[2.75rem_minmax(0,1fr)_auto] items-center gap-x-4 gap-y-2 px-3 py-2.5 hover:bg-accent/60 focus-visible:bg-accent focus-visible:outline-2 focus-visible:-outline-offset-2 focus-visible:outline-ring"
              :class="gridCols"
            >
              <span class="sr-only">{{ station ? `Open ${stationLabel(station)}:` : 'Open record:' }}</span>
              <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="44" alt="" />

              <div class="min-w-0">
                <p class="flex min-w-0 items-center gap-2">
                  <span class="truncate text-sm font-semibold group-hover:text-primary touch:text-base">{{ patient.name }}</span>
                  <span
                    v-if="hasDrugAllergy(patient.drugAllergy)"
                    class="inline-flex shrink-0 items-center gap-1 border border-destructive bg-destructive-subtle px-1 text-[0.6875rem] font-semibold text-destructive touch:text-xs"
                    :title="`Drug allergy: ${patient.drugAllergy}`"
                  >
                    <PhWarning class="size-3" aria-hidden="true" />
                    <span class="max-w-32 truncate">{{ patient.drugAllergy }}</span>
                  </span>
                </p>
                <p class="flex flex-wrap items-center gap-x-1.5 text-xs text-muted-foreground touch:text-sm">
                  <PatientCode :village-code="patient.villageCode" :village-color-hex="patient.villageColorHex" :id="patient.id" />
                  <span aria-hidden="true">·</span>
                  <span>{{ calculateAge(patient.dateOfBirth) }} y <span class="capitalize">{{ patient.gender }}</span></span>
                  <template v-if="patient.lastVisitDate">
                    <span aria-hidden="true">·</span>
                    <span>{{ dateOf(patient.lastVisitDate) }}</span>
                  </template>
                </p>
              </div>

              <PhCaretRight
                class="size-4 text-muted-foreground group-hover:text-primary md:hidden"
                aria-hidden="true"
              />

              <div class="col-span-3 min-w-0 md:col-span-1 md:col-start-3 md:row-start-1">
                <JourneyTracker
                  v-if="patient.lastVisitJourney && patient.lastVisitId"
                  :journey="patient.lastVisitJourney"
                  :patient-id="patient.id"
                  :visit-id="patient.lastVisitId"
                  :active="station"
                  :links="false"
                  size="compact"
                />
                <span v-else class="text-xs text-muted-foreground touch:text-sm">No visit yet</span>
              </div>

              <PhCaretRight
                class="hidden size-4 text-muted-foreground group-hover:text-primary md:col-start-4 md:block"
                aria-hidden="true"
              />
            </NuxtLink>
          </li>
        </ul>
      </template>
    </AppPanel>
  </div>
</template>
