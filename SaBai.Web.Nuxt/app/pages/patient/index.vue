<script setup lang="ts">
import { PhCaretRight, PhScan } from '@phosphor-icons/vue'
import { useIntervalFn } from '@vueuse/core'
import type { StationKey } from '~/utils/journey'
import type { StationThroughput } from '~/utils/waitTime'
import { Button } from '@/components/ui/button'

/**
 * The patient list — one page for every station.
 *
 * Unfiltered it is the front desk's overview: every patient and where their
 * latest visit is in its journey (vitals → vision → consult → dental → pharmacy).
 * With `?station=vitals` (the sidebar's station links) it splits into two
 * sections: a "Waiting for <Station>" queue on top (earliest arrival first) and
 * a collapsible "Completed & other patients" section below, with no calendar-day
 * cutoff so clinics running past midnight work without interruption.
 *
 * The station cards show how backlogged each station is: how many still need
 * it and roughly how long someone sent there now would wait. The pace comes
 * from each station's measured throughput (records saved in the last hour, or
 * recent session), falling back to this device's form-fill times — see
 * utils/waitTime.ts and utils/serviceTime.ts.
 */
const route = useRoute()
const station = computed<StationKey | undefined>(() =>
  isStationKey(route.query.station) ? route.query.station : undefined,
)

useHead({ title: () => `${station.value ? stationLabel(station.value) : 'Patients'} · Sa'Bai` })

const { data, isLoading, isError } = usePatientsWithLatestVisit()
type Row = NonNullable<typeof data.value>['patients'][number]

const allPatients = computed(() => data.value?.patients ?? [])

/** How many patients each station still has to see. */
const waitingCounts = computed(() => {
  const counts = {} as Record<StationKey, number>
  for (const { key } of STATIONS)
    counts[key] = allPatients.value.filter(p => p.lastVisitJourney && isWaitingFor(p.lastVisitJourney, key)).length
  return counts
})

// Throughput per station, refreshed each minute and whenever data changes.
const now = shallowRef(new Date())
useIntervalFn(() => (now.value = new Date()), 60_000)
const throughput = computed(() => {
  const journeys = allPatients.value.flatMap(p => (p.lastVisitJourney ? [p.lastVisitJourney] : []))
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
      ? `About ${formatRate(t.perMinute)}, from ${t.samples} patients seen ${t.basis === 'last hour' ? 'in the last hour' : 'recently'}`
      : 'Not enough patients seen yet to estimate a wait'

/** Patients currently in the selected station's queue, earliest visit first. */
const waitingRows = computed(() => {
  const s = station.value
  if (!s)
    return []
  return allPatients.value
    .filter(p => p.lastVisitJourney && isWaitingFor(p.lastVisitJourney, s))
    .sort((a, b) => new Date(a.lastVisitDate!).getTime() - new Date(b.lastVisitDate!).getTime())
})

const isDoneAtStation = (p: Row, s: StationKey) =>
  !!p.lastVisitJourney && stepState(p.lastVisitJourney, s).state === 'done'

/** Everyone not waiting for the selected station (completed this station first, then others). */
const otherRows = computed(() => {
  const s = station.value
  if (!s)
    return []
  return allPatients.value
    .filter(p => !(p.lastVisitJourney && isWaitingFor(p.lastVisitJourney, s)))
    .sort((a, b) => Number(isDoneAtStation(b, s)) - Number(isDoneAtStation(a, s)))
})

/** The summary row: all patients, then each station's backlog and wait for someone sent now. */
const stationCards = computed(() => [
  {
    key: undefined as StationKey | undefined,
    label: 'All patients',
    count: allPatients.value.length,
    detail: 'All records',
    note: '',
  },
  ...STATIONS.map(({ key, label }) => {
    const t = throughput.value[key]
    const waiting = waitingCounts.value[key]
    const minutes = stationWaitMinutes(t, waiting)
    const detail = !waiting
      ? 'None waiting'
      : minutes !== null
        ? `Waiting · ${formatWait(minutes)}`
        : 'Waiting'
    return { key: key as StationKey | undefined, label, count: waiting, detail, note: basisNote(t) }
  }),
])

const sections = computed(() => {
  const s = station.value
  if (!s) {
    return [
      {
        key: 'all',
        title: 'All patients',
        description: isLoading.value ? undefined : `${allPatients.value.length}`,
        collapsible: false,
        defaultOpen: true,
        arrivedLabel: 'Last visit',
        rows: allPatients.value,
        emptyMessage: 'No patients found. Register a patient from Scan Face.',
        showScanFaceEmptyAction: true,
      },
    ]
  }

  const label = stationLabel(s)
  const waitingCount = waitingRows.value.length
  const minutes = stationWaitMinutes(throughput.value[s], waitingCounts.value[s])
  const waitingDescription = isLoading.value
    ? undefined
    : !waitingCount
        ? 'None waiting'
        : minutes !== null
          ? `${waitingCount} waiting · ${formatWait(minutes)} wait`
          : `${waitingCount} waiting`

  const doneCount = otherRows.value.filter(p => isDoneAtStation(p, s)).length
  const otherDescription = doneCount > 0
    ? `${doneCount} done · ${otherRows.value.length} total`
    : `${otherRows.value.length}`

  const list = [
    {
      key: `waiting-${s}`,
      title: `Waiting for ${label}`,
      description: waitingDescription,
      collapsible: false,
      defaultOpen: true,
      arrivedLabel: 'Arrived',
      rows: waitingRows.value,
      emptyMessage: `Nobody is waiting for ${label}.`,
      showScanFaceEmptyAction: false,
    },
  ]

  if (!isLoading.value && !isError.value) {
    list.push({
      key: `other-${s}`,
      title: 'Completed & other patients',
      description: otherDescription,
      collapsible: true,
      defaultOpen: waitingCount === 0,
      arrivedLabel: 'Last visit',
      rows: otherRows.value,
      emptyMessage: 'No other patients.',
      showScanFaceEmptyAction: false,
    })
  }

  return list
})

/**
 * Where a row leads: in a station worklist, that station's form for the
 * patient's current visit; otherwise the patient record, which shows the
 * journey and the next step.
 */
const rowLink = (p: Row) =>
  station.value && station.value !== 'pharmacy'
    ? patientLinkFor(station.value, p.id, p.lastVisitId ?? undefined)
    : paths.patientDetail(p.id)

const timeOf = (date: string) => new Date(date).toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' })
const dateOf = (date: string) => (isRecentVisit(date, now.value) ? timeOf(date) : new Date(date).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: '2-digit' }))

/** Time in clinic so far ("42 min", "1 h 05"), for visits within the last 24 hours. */
function waited(date: string | null) {
  if (!isRecentVisit(date, now.value))
    return ''
  const minutes = Math.max(0, Math.floor((now.value.getTime() - new Date(date!).getTime()) / 60_000))
  return minutes < 60 ? `${minutes} min` : `${Math.floor(minutes / 60)} h ${String(minutes % 60).padStart(2, '0')}`
}

/*
 * One grid for the heading and every row, so columns line up:
 *   phone   photo | patient (meta below)     | ›     journey on its own line
 *   tablet  photo | patient (meta below)     | journey | ›
 *   laptop  photo | patient | allergies | ID | age | arrived | journey | ›
 */
const gridCols = 'md:grid-cols-[2rem_minmax(0,1fr)_minmax(15rem,22rem)_1rem] lg:grid-cols-[2rem_minmax(8rem,1fr)_minmax(6rem,11rem)_minmax(5.5rem,8rem)_3.75rem_8.5rem_minmax(16rem,22rem)_1rem]'
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
      Summary first: one cell per station picks that worklist (the same page,
      filtered) and shows how many are waiting and roughly how long someone
      sent there now would wait.
    -->
    <nav aria-label="Stations" class="grid grid-cols-3 gap-px border border-border bg-border lg:grid-cols-6">
      <NuxtLink
        v-for="card in stationCards"
        :key="card.key ?? 'all'"
        :to="paths.patient(card.key)"
        class="flex min-w-0 flex-col gap-0.5 bg-card px-3 py-1.5 hover:bg-accent focus-visible:bg-accent"
        :class="station === card.key && 'bg-accent shadow-[inset_0_-3px_0_var(--color-primary)]'"
        :aria-current="station === card.key ? 'page' : undefined"
        :title="card.note"
      >
        <span class="flex items-baseline justify-between gap-2">
          <span class="truncate text-xs font-bold text-chrome touch:text-sm">{{ card.label }}</span>
          <span class="text-lg leading-tight font-bold tabular-nums" :class="card.count === 0 && 'text-muted-foreground'">
            {{ isLoading ? '–' : card.count }}
          </span>
        </span>
        <span class="truncate text-xs text-muted-foreground tabular-nums touch:text-[0.8125rem]">{{ card.detail }}</span>
        <span v-if="card.note" class="sr-only">{{ card.note }}</span>
      </NuxtLink>
    </nav>

    <AppPanel
      v-for="section in sections"
      :key="section.key"
      :title="section.title"
      :description="section.description"
      :collapsible="section.collapsible"
      :default-open="section.defaultOpen"
      flush
    >
      <StateMessage v-if="isError" kind="error" message="Patients could not be loaded. Check the connection and reload." />
      <StateMessage v-else-if="isLoading" kind="loading" message="Loading patients..." />
      <div v-else-if="section.rows.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center">
        <p class="text-xs text-muted-foreground touch:text-sm" role="status">
          {{ section.emptyMessage }}
        </p>
        <div v-if="section.showScanFaceEmptyAction" class="flex flex-wrap justify-center gap-2">
          <Button as-child size="sm" variant="outline">
            <NuxtLink :to="paths.scanFace()">
              <PhScan aria-hidden="true" /> Open Scan Face
            </NuxtLink>
          </Button>
        </div>
      </div>
      <template v-else>
        <!-- Column headings (tablet and up); rows share the same grid. -->
        <div
          class="sticky top-[var(--app-header-h)] z-[2] hidden gap-x-3 border-b border-input bg-band px-3 py-1 text-xs font-semibold text-chrome md:grid"
          :class="gridCols"
          aria-hidden="true"
        >
          <span />
          <span>Patient</span>
          <span class="hidden lg:block">Allergies</span>
          <span class="hidden lg:block">ID</span>
          <span class="hidden lg:block">Age / sex</span>
          <span class="hidden lg:block">{{ section.arrivedLabel }}</span>
          <span>Journey</span>
          <span />
        </div>
        <ul class="divide-y divide-border">
          <li v-for="patient in section.rows" :key="patient.id">
            <!--
              The whole row opens the patient (the name's link stretches over
              it); the journey tiles sit above that and open their own station.
            -->
            <div
              class="group relative grid grid-cols-[2.5rem_minmax(0,1fr)_auto] items-center gap-x-3 gap-y-1.5 px-3 py-2 hover:bg-accent/60 has-[a.row-link:focus-visible]:bg-accent md:py-1.5 lg:py-1"
              :class="gridCols"
            >
              <span class="flex size-10 overflow-hidden md:size-8 [&>*]:size-full!">
                <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="40" alt="" />
              </span>

              <div class="min-w-0">
                <p class="flex min-w-0 items-center gap-1.5">
                  <NuxtLink
                    :to="rowLink(patient)"
                    class="row-link min-w-0 shrink truncate text-sm font-semibold group-hover:text-primary after:absolute after:inset-0 focus-visible:outline-none focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-ring touch:text-base"
                  >
                    <span class="sr-only">{{ station && station !== 'pharmacy' ? `Open ${stationLabel(station)}:` : 'Open record:' }}</span>
                    {{ patient.name }}
                  </NuxtLink>
                  <!-- Below laptop width the allergy rides next to the name (its own column on laptops). -->
                  <AllergyFlag :allergy="patient.drugAllergy" class="max-w-[50%] shrink-0 lg:hidden" />
                </p>
                <!-- Below laptop width the columns fold into one meta line. -->
                <p class="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground lg:hidden touch:text-[0.8125rem]">
                  <PatientCode :village-code="patient.villageCode" :village-color-hex="patient.villageColorHex" :id="patient.id" />
                  <span>{{ calculateAge(patient.dateOfBirth) }} y <span class="capitalize">{{ patient.gender }}</span></span>
                  <span v-if="patient.lastVisitDate" class="tabular-nums">
                    {{ dateOf(patient.lastVisitDate) }}<template v-if="waited(patient.lastVisitDate)">
                      <span> · {{ waited(patient.lastVisitDate) }}</span>
                    </template>
                  </span>
                </p>
              </div>

              <span class="hidden min-w-0 lg:block">
                <AllergyFlag :allergy="patient.drugAllergy" />
              </span>
              <span
                class="hidden min-w-0 truncate text-xs text-muted-foreground lg:block"
                :title="formatPatientCode(patient.villageCode, patient.id)"
              >
                <PatientCode :village-code="patient.villageCode" :village-color-hex="patient.villageColorHex" :id="patient.id" />
              </span>
              <span class="hidden truncate text-xs whitespace-nowrap lg:block">
                {{ calculateAge(patient.dateOfBirth) }} y <span class="text-muted-foreground">{{ patient.gender === 'female' ? 'F' : 'M' }}</span>
              </span>
              <span class="hidden text-xs whitespace-nowrap tabular-nums lg:block">
                <template v-if="patient.lastVisitDate">
                  {{ dateOf(patient.lastVisitDate) }}
                  <span v-if="waited(patient.lastVisitDate)" class="text-muted-foreground" :title="`In clinic for ${waited(patient.lastVisitDate)}`">· {{ waited(patient.lastVisitDate) }}</span>
                </template>
                <span v-else class="text-muted-foreground">—</span>
              </span>

              <PhCaretRight
                class="size-4 text-muted-foreground group-hover:text-primary md:hidden"
                aria-hidden="true"
              />

              <div class="relative z-[1] col-span-3 min-w-0 md:col-span-1 md:col-start-3 md:row-start-1 lg:col-start-7">
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

              <PhCaretRight
                class="hidden size-4 text-muted-foreground group-hover:text-primary md:col-start-4 md:block lg:col-start-8"
                aria-hidden="true"
              />
            </div>
          </li>
        </ul>
      </template>
    </AppPanel>
  </div>
</template>
