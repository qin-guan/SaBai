<script setup lang="ts">
import { PhArrowRight, PhMagnifyingGlass, PhScan, PhWarning } from '@phosphor-icons/vue'
import { useIntervalFn } from '@vueuse/core'
import type { StationKey } from '~/utils/journey'
import type { StationThroughput } from '~/utils/waitTime'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * The patient queue — one page for every station.
 *
 * Unfiltered it is the front desk's overview: everyone seen today and where
 * each is in their journey (vitals → vision → consult → dental → pharmacy).
 * With `?station=vitals` (the sidebar's station links) it becomes that
 * station's worklist: who is still waiting for it, longest-waiting first, and
 * one button that opens the station's form on the patient's current visit.
 *
 * Wait estimates come from each station's measured throughput (records saved
 * in the last hour, or today), falling back to this device's form-fill times
 * — see utils/waitTime.ts and utils/serviceTime.ts.
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

/**
 * Each station's queue: who it is waiting for, most ready first (earlier steps
 * done, dental referrals), then whoever arrived earliest.
 */
const queues = computed(() => {
  const out = {} as Record<StationKey, Row[]>
  for (const { key } of STATIONS) {
    out[key] = inScope.value
      .filter(p => p.lastVisitJourney && isWaitingFor(p.lastVisitJourney, key))
      .sort((a, b) =>
        stationPriority(a.lastVisitJourney!, key) - stationPriority(b.lastVisitJourney!, key)
        || new Date(a.lastVisitDate!).getTime() - new Date(b.lastVisitDate!).getTime(),
      )
  }
  return out
})
const waitingCounts = computed(() => {
  const counts = {} as Record<StationKey, number>
  for (const { key } of STATIONS)
    counts[key] = queues.value[key].length
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
const positions = computed(() => {
  const out = {} as Record<StationKey, Map<number, number>>
  for (const { key } of STATIONS)
    out[key] = new Map(queues.value[key].map((p, i) => [p.id, i + 1]))
  return out
})
/** Queue position (1 = next) and estimated wait for a patient at a station. */
function waitAt(p: Row, key: StationKey) {
  const position = positions.value[key].get(p.id)
  if (!position)
    return null
  return { position, minutes: estimatedWaitMinutes(throughput.value[key], position) }
}
/** The wait shown on a row: for this station's queue, or (unfiltered) for the patient's next step. */
function rowWait(p: Row) {
  const key = station.value ? (show.value === 'waiting' ? station.value : undefined) : actionFor(p).station
  const wait = key ? waitAt(p, key) : null
  return wait && key ? { ...wait, station: key } : null
}
/** Estimated time to clear a station's whole queue. */
const clearMinutes = (key: StationKey) => estimatedWaitMinutes(throughput.value[key], waitingCounts.value[key])
const basisNote = (t: StationThroughput) =>
  t.basis === 'form timing'
    ? 'Based on how long forms take on this device'
    : t.basis
      ? `Based on ${t.samples} patients seen ${t.basis === 'today' ? 'today' : 'in the last hour'}`
      : 'Not enough patients seen yet to estimate'

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
    return queues.value[s].filter(matchesFilter)
  return list
})

/** The summary row: all patients, then each station's queue and wait. */
const stationCards = computed(() => [
  {
    key: undefined as StationKey | undefined,
    label: 'All',
    count: inScope.value.length,
    detail: scope.value === 'today' ? 'in clinic today' : 'patients',
    note: '',
  },
  ...STATIONS.map(({ key, label }) => {
    const t = throughput.value[key]
    const waiting = waitingCounts.value[key]
    const detail = !waiting
      ? 'no queue'
      : t.perMinute
        ? `${formatWait(clearMinutes(key))} · ${formatRate(t.perMinute)}`
        : 'no estimate yet'
    return { key: key as StationKey | undefined, label, count: waiting, detail, note: basisNote(t) }
  }),
])

const ordinal = (n: number) => `${n}${n % 100 >= 11 && n % 100 <= 13 ? 'th' : ({ 1: 'st', 2: 'nd', 3: 'rd' } as Record<number, string>)[n % 10] ?? 'th'}`

const SHOW_OPTIONS: { value: Show, label: string }[] = [
  { value: 'waiting', label: 'Waiting' },
  { value: 'done', label: 'Done' },
  { value: 'all', label: 'All' },
]

/** The row's main action: open this station, or (unfiltered) the patient's next step. */
function actionFor(p: Row) {
  const visitId = p.lastVisitId ?? undefined
  if (station.value) {
    return { label: `Open ${stationLabel(station.value)}`, to: patientLinkFor(station.value, p.id, visitId) }
  }
  const next = p.lastVisitJourney ? nextStep(p.lastVisitJourney) : null
  if (next)
    return { label: `Next: ${next.label}`, to: patientLinkFor(next.key, p.id, visitId), station: next.key }
  return { label: 'Open record', to: paths.patientDetail(p.id) }
}

const emptyMessage = computed(() => {
  if (filter.value.trim())
    return `No patients match “${filter.value.trim()}”.`
  if (station.value && show.value === 'waiting')
    return `Nobody is waiting for ${stationLabel(station.value)}${scope.value === 'today' ? ' today' : ''}.`
  if (scope.value === 'today')
    return 'No visits today yet. Start one from Scan Face.'
  return 'No patients found. Register a patient from Scan Face.'
})

const timeOf = (date: string) => new Date(date).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
const dateOf = (date: string) => (isSameDay(date) ? `Today ${timeOf(date)}` : new Date(date).toLocaleDateString())
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      :title="station ? `${stationLabel(station)} queue` : 'Patients'"
      :breadcrumbs="station
        ? [{ label: 'Home', to: '/' }, { label: 'Patients', to: paths.patient() }, { label: stationLabel(station) }]
        : [{ label: 'Home', to: '/' }, { label: 'Patients' }]"
      class="mb-0!"
    >
      <template #actions>
        <Button as-child variant="outline">
          <NuxtLink :to="paths.scanFace()">
            <PhScan aria-hidden="true" /> Scan face / register
          </NuxtLink>
        </Button>
      </template>
    </PageHeader>

    <!--
      Summary first: one card per station picks that queue (the same page,
      filtered) and shows how many are waiting and roughly how long for.
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
          class="hidden border-b border-border bg-muted/50 px-3 py-1 text-[0.6875rem] font-semibold tracking-wide text-muted-foreground uppercase md:grid md:grid-cols-[2.75rem_minmax(0,1fr)_minmax(0,20rem)_8.5rem_9.5rem] md:gap-x-3"
          aria-hidden="true"
        >
          <span />
          <span>Patient</span>
          <span>Journey</span>
          <span>{{ station && show !== 'waiting' ? '' : 'Est. wait' }}</span>
          <span />
        </div>
        <ul class="divide-y divide-border">
          <li
            v-for="patient in rows"
            :key="patient.id"
            class="grid grid-cols-[2.75rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-2 px-3 py-2 md:grid-cols-[2.75rem_minmax(0,1fr)_minmax(0,20rem)_8.5rem_9.5rem]"
          >
            <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="44" alt="" />

            <div class="min-w-0">
              <p class="flex min-w-0 items-center gap-2">
                <NuxtLink
                  :to="paths.patientDetail(patient.id)"
                  class="truncate text-sm font-semibold text-foreground underline-offset-2 hover:text-primary hover:underline touch:text-base"
                >
                  {{ patient.name }}
                </NuxtLink>
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
                  <span :title="`Visit started ${formatVisitDate(patient.lastVisitDate)}`">{{ dateOf(patient.lastVisitDate) }}</span>
                </template>
              </p>
            </div>

            <!-- Wait: beside the name on phones, its own column on wide screens. -->
            <div class="text-right md:order-last md:col-start-4 md:row-start-1 md:text-left">
              <template v-if="rowWait(patient)">
                <p class="text-sm font-semibold whitespace-nowrap tabular-nums touch:text-base">
                  {{ rowWait(patient)!.minutes !== null ? formatWait(rowWait(patient)!.minutes) : `${ordinal(rowWait(patient)!.position)} in line` }}
                </p>
                <p class="text-[0.6875rem] whitespace-nowrap text-muted-foreground touch:text-xs">
                  <template v-if="rowWait(patient)!.minutes !== null">{{ ordinal(rowWait(patient)!.position) }}</template><template v-else>No estimate</template><template v-if="!station"> · {{ stationLabel(rowWait(patient)!.station) }}</template>
                </p>
              </template>
            </div>

            <div class="col-span-3 min-w-0 md:col-span-1 md:col-start-3 md:row-start-1">
              <JourneyTracker
                v-if="patient.lastVisitJourney && patient.lastVisitId"
                :journey="patient.lastVisitJourney"
                :patient-id="patient.id"
                :visit-id="patient.lastVisitId"
                :active="station"
                size="compact"
              />
              <span v-else class="text-xs text-muted-foreground touch:text-sm">No visit yet</span>
            </div>

            <Button
              as-child
              size="sm"
              :variant="station ? 'success' : 'outline'"
              class="col-span-3 md:col-span-1 md:col-start-5 md:row-start-1 md:justify-self-stretch"
            >
              <NuxtLink :to="actionFor(patient).to" :aria-label="`${actionFor(patient).label}: ${patient.name}`">
                {{ actionFor(patient).label }}
                <PhArrowRight aria-hidden="true" />
              </NuxtLink>
            </Button>
          </li>
        </ul>
      </template>
    </AppPanel>
  </div>
</template>
