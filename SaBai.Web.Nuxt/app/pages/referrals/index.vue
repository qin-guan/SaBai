<script setup lang="ts">
import { PhBuildings, PhMagnifyingGlass, PhX } from '@phosphor-icons/vue'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { collectUniqueTags, type TagMatchMode } from '@/utils/patient'
import {
  inReferralStatusGroup,
  matchesReferral,
  REFERRAL_STATE_OPTIONS,
  REFERRAL_STATUS_GROUPS,
  type ReferralListItem,
  type ReferralStatusGroup,
} from '@/utils/referral'

/**
 * Referral worklist, laid out like the patient list: a status strip on top
 * (one cell per status, with counts) picks the list below, and each row is a
 * single link to the referral. Keyboard: ↓ from the search box enters the
 * list, ↑/↓ move between rows, Enter opens, and Esc on the referral comes back
 * to the same row with the filters kept.
 */
useHead({ title: "Referrals · Sa'Bai" })

const { data: referrals, isLoading, isError } = useReferralsList()
const { data: patientsData } = usePatientsList()
const { villageCodes } = useVillageCode()

// Kept for the session so coming back from a referral restores the view.
const filters = useState('referrals:filters', () => ({
  search: '',
  villageId: '',
  status: null as ReferralStatusGroup | null,
  tags: [] as string[],
  tagMatchMode: 'any' as TagMatchMode,
  statuses: [] as SaBaiAPIDomainReferralStateKey[],
}))
const lastOpened = useState<string | null>('referrals:last-opened', () => null)

const patientTagsById = computed(() => {
  const map = new Map<string, string[]>()
  for (const p of patientsData.value?.patients ?? []) {
    if (p.tags?.length)
      map.set(p.id, p.tags)
  }
  return map
})

const enrichedReferrals = computed<ReferralListItem[]>(() =>
  (referrals.value ?? []).map((item) => {
    const raw = item as ReferralListItem
    const tags = raw.tags?.length ? raw.tags : (patientTagsById.value.get(raw.patientId) ?? [])
    return { ...raw, tags }
  }),
)

const availableReferralTags = computed(() =>
  collectUniqueTags(enrichedReferrals.value.map(r => r.tags)),
)

/** Referrals matching the search, village, tags, and in-table status filters (before the status strip). */
const matching = computed(() => {
  const options = {
    search: filters.value.search,
    villageCodeId: filters.value.villageId || null,
    tags: filters.value.tags,
    tagMatchMode: filters.value.tagMatchMode,
    referralStates: filters.value.statuses,
  }
  return enrichedReferrals.value.filter(r => matchesReferral(r, options))
})
const rows = computed(() => matching.value.filter(r => inReferralStatusGroup(r.referralState, filters.value.status)))

const statusCells = computed(() => {
  const count = (group: ReferralStatusGroup) => matching.value.filter(r => inReferralStatusGroup(r.referralState, group)).length
  const succeeded = matching.value.filter(r => r.referralState === 'CompletedSuccess').length
  const failed = matching.value.filter(r => r.referralState === 'CompletedFailure').length
  return [
    { key: null, label: 'All', count: matching.value.length, detail: `${matching.value.length - count('Completed')} not completed` },
    ...REFERRAL_STATUS_GROUPS.map(group => ({
      key: group.key,
      label: group.label,
      count: count(group.key),
      detail: group.key === 'Completed' && (succeeded || failed) ? `${succeeded} succeeded, ${failed} failed` : '',
    })),
  ]
})

const hasFilters = computed(
  () =>
    !!filters.value.search ||
    !!filters.value.villageId ||
    !!filters.value.status ||
    filters.value.tags.length > 0 ||
    filters.value.statuses.length > 0,
)
const listTitle = computed(() => {
  const group = REFERRAL_STATUS_GROUPS.find(g => g.key === filters.value.status)
  return group ? `${group.label} referrals` : 'All referrals'
})

function clearFilters() {
  filters.value = {
    search: '',
    villageId: '',
    status: null,
    tags: [],
    tagMatchMode: 'any',
    statuses: [],
  }
}

/** Esc in the search box clears it first; a second Esc leaves the field, a third goes back. */
function onSearchEsc(event: KeyboardEvent) {
  if (!filters.value.search)
    return
  event.stopPropagation()
  filters.value.search = ''
}

const list = ref<HTMLElement>()
const rowLinks = () => [...(list.value?.querySelectorAll<HTMLAnchorElement>('a.row-link') ?? [])]

/** ↓ from the search box goes to the first referral. */
function onSearchDown(event: KeyboardEvent) {
  const first = rowLinks()[0]
  if (!first)
    return
  event.preventDefault()
  first.focus()
}

/** ↑/↓ (and Home/End) move between rows; ↑ on the first row goes back to the search box. */
function onListKeydown(event: KeyboardEvent) {
  const target = event.target as HTMLElement
  if (!target.classList.contains('row-link') || event.altKey || event.ctrlKey || event.metaKey)
    return
  const links = rowLinks()
  const index = links.indexOf(target as HTMLAnchorElement)
  let next: HTMLElement | undefined
  if (event.key === 'ArrowDown')
    next = links[index + 1]
  else if (event.key === 'ArrowUp')
    next = index === 0 ? searchInput.value : links[index - 1]
  else if (event.key === 'Home')
    next = links[0]
  else if (event.key === 'End')
    next = links.at(-1)
  else
    return
  event.preventDefault()
  next?.focus()
  next?.scrollIntoView({ block: 'nearest' })
}

const searchInput = ref<HTMLInputElement>()
const searchWrap = ref<HTMLElement>()
onMounted(() => {
  searchInput.value = searchWrap.value?.querySelector('input') ?? undefined
})

// Back from a referral: put focus on its row so ↑/↓ carry on from there.
watch(rows, async () => {
  if (lastOpened.value == null)
    return
  await nextTick()
  const link = list.value?.querySelector<HTMLAnchorElement>(`a.row-link[data-referral-id="${lastOpened.value}"]`)
  if (link) {
    link.focus({ preventScroll: true })
    link.scrollIntoView({ block: 'center' })
  }
  lastOpened.value = null
}, { immediate: true, flush: 'post' })

const shortDate = (date: string) => {
  const d = new Date(date)
  return d.toLocaleDateString(undefined, {
    day: 'numeric',
    month: 'short',
    year: d.getFullYear() === new Date().getFullYear() ? undefined : 'numeric',
  })
}

const gridCols = 'md:grid-cols-[2rem_minmax(9rem,1fr)_minmax(0,2fr)_5.5rem_11rem] lg:grid-cols-[2rem_minmax(9rem,1fr)_minmax(6rem,11rem)_minmax(0,2fr)_5.5rem_11rem]'
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="Referrals"
      description="Patients referred to partner clinics, from booth to outcome."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Referrals' }]"
      class="mb-0!"
    >
      <template #actions>
        <Button as-child variant="outline">
          <NuxtLink :to="paths.settingsReferralPartners()">
            <PhBuildings aria-hidden="true" /> Partner clinics
          </NuxtLink>
        </Button>
      </template>
    </PageHeader>

    <!-- One cell per status: picks the list below and shows how many are in it. -->
    <div role="group" aria-label="Filter by status" class="grid grid-cols-3 gap-px border border-border bg-border sm:grid-cols-6">
      <button
        v-for="cell in statusCells"
        :key="cell.key ?? 'all'"
        type="button"
        class="flex min-w-0 flex-col gap-0.5 bg-card px-3 py-1.5 text-left hover:bg-accent focus-visible:bg-accent focus-visible:outline-none"
        :class="filters.status === cell.key && 'bg-accent shadow-[inset_0_-3px_0_var(--color-primary)]'"
        :aria-pressed="filters.status === cell.key"
        @click="filters.status = cell.key"
      >
        <span class="flex items-baseline justify-between gap-2">
          <span class="truncate text-xs font-bold text-chrome touch:text-sm">{{ cell.label }}</span>
          <span class="text-lg leading-tight font-bold tabular-nums" :class="cell.count === 0 && 'text-muted-foreground'">
            {{ isLoading ? '–' : cell.count }}
          </span>
        </span>
        <span v-if="cell.detail" class="truncate text-xs text-muted-foreground touch:text-[0.8125rem]">{{ cell.detail }}</span>
      </button>
    </div>

    <AppPanel :title="listTitle" :description="isLoading ? undefined : `${rows.length} shown`" flush>
      <template #actions>
        <div class="flex w-full flex-col gap-2 sm:w-auto sm:flex-row sm:flex-wrap sm:items-center">
          <div ref="searchWrap" class="relative sm:w-64">
            <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
            <Input
              v-model="filters.search"
              type="search"
              placeholder="Name, ID, tag, reason or notes"
              class="bg-card pr-8 pl-8"
              aria-label="Search referrals"
              aria-keyshortcuts="ArrowDown"
              @keydown.esc="onSearchEsc"
              @keydown.down="onSearchDown"
            />
            <button
              v-if="filters.search"
              type="button"
              class="absolute top-1/2 right-1.5 -translate-y-1/2 p-1 text-muted-foreground hover:text-foreground"
              aria-label="Clear search"
              @click="filters.search = ''"
            >
              <PhX class="size-3.5" aria-hidden="true" />
            </button>
          </div>
          <NativeSelect v-model="filters.villageId" aria-label="Village" class="bg-card sm:w-40">
            <NativeSelectOption value="">
              All villages
            </NativeSelectOption>
            <NativeSelectOption v-for="v in villageCodes" :key="v.id" :value="String(v.id)">
              {{ v.name }} ({{ v.code }})
            </NativeSelectOption>
          </NativeSelect>
          <TagFilter
            v-model="filters.tags"
            v-model:match-mode="filters.tagMatchMode"
            :available-tags="availableReferralTags"
          />
          <StatusFilter
            v-model="filters.statuses"
            :options="REFERRAL_STATE_OPTIONS"
            label="Status"
          />
          <Button
            v-if="hasFilters"
            variant="ghost"
            size="sm"
            class="text-xs touch:h-10"
            @click="clearFilters"
          >
            Reset
          </Button>
        </div>
      </template>

      <StateMessage v-if="isLoading" kind="loading" message="Loading referrals…" />
      <StateMessage v-else-if="isError" kind="error" message="Referrals could not be loaded. Check the connection and reload." />
      <div v-else-if="!enrichedReferrals.length" class="flex flex-col items-center gap-1 px-3 py-10 text-center" role="status">
        <p class="font-semibold">
          No referrals yet
        </p>
        <p class="text-xs text-muted-foreground touch:text-sm">
          Doctors open a referral from the consult form; it shows up here for the PN team.
        </p>
      </div>
      <template v-else>
        <!-- Column headings (tablet and up) with in-table tag & status filter dropdowns -->
        <div
          v-if="rows.length > 0 || hasFilters"
          class="hidden items-center gap-x-3 border-b border-input bg-band px-3 py-1 text-xs font-semibold text-chrome md:grid"
          :class="gridCols"
        >
          <span />
          <span class="flex items-center gap-3">
            <span>Patient</span>
            <TagFilter
              v-model="filters.tags"
              v-model:match-mode="filters.tagMatchMode"
              :available-tags="availableReferralTags"
              variant="header"
              label="Tags"
              class="lg:hidden"
            />
          </span>
          <span class="hidden lg:block">
            <TagFilter
              v-model="filters.tags"
              v-model:match-mode="filters.tagMatchMode"
              :available-tags="availableReferralTags"
              variant="header"
              label="Tags"
            />
          </span>
          <span>Referred for</span>
          <span>Referred</span>
          <span>
            <StatusFilter
              v-model="filters.statuses"
              :options="REFERRAL_STATE_OPTIONS"
              variant="header"
              label="Status"
              align="end"
            />
          </span>
        </div>

        <div v-if="!rows.length" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
          <p class="text-xs text-muted-foreground touch:text-sm">
            No referrals match these filters.
          </p>
          <Button v-if="hasFilters" variant="outline" size="sm" @click="clearFilters">
            Clear filters
          </Button>
        </div>

        <ul v-else ref="list" class="divide-y divide-border" @keydown="onListKeydown">
          <li v-for="r in rows" :key="r.id">
            <!-- The whole row opens the referral (the name's link stretches over it); the tags and status sit above and act on their own. -->
            <div
              class="group relative grid grid-cols-[2.5rem_minmax(0,1fr)] items-start gap-x-3 gap-y-1.5 px-3 py-2 hover:bg-accent/60 has-[a.row-link:focus-visible]:bg-accent md:items-center md:py-1.5"
              :class="gridCols"
            >
              <span class="flex size-10 overflow-hidden md:size-8 [&>*]:size-full!">
                <PatientPhoto :patient-id="r.patientId" :has-photo="r.hasPhoto" :size="40" alt="" />
              </span>

              <div class="min-w-0">
                <NuxtLink
                  :to="paths.referralsDetail(r.id)"
                  :data-referral-id="r.id"
                  class="row-link block truncate text-sm font-semibold group-hover:text-primary after:absolute after:inset-0 focus-visible:outline-none focus-visible:after:outline-2 focus-visible:after:-outline-offset-2 focus-visible:after:outline-ring touch:text-base"
                >
                  <span class="sr-only">Open referral for</span>
                  {{ r.patientName }}
                </NuxtLink>
                <p class="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground touch:text-[0.8125rem]">
                  <PatientCode :village-code="r.villageCode" :village-color-hex="r.villageColorHex" :id="r.patientNumber" />
                  <span class="tabular-nums md:hidden">{{ shortDate(r.createdAt) }}</span>
                </p>
                <div v-if="r.tags?.length" class="mt-1 lg:hidden">
                  <PatientTagBadges
                    v-model:selected-tags="filters.tags"
                    :tags="r.tags"
                  />
                </div>
              </div>

              <span class="hidden min-w-0 lg:block">
                <PatientTagBadges
                  v-model:selected-tags="filters.tags"
                  :tags="r.tags"
                  show-empty
                />
              </span>

              <div class="col-start-2 min-w-0 text-xs touch:text-sm md:col-start-auto">
                <p class="font-bold text-chrome">
                  {{ r.referredFor }}
                </p>
                <p v-if="r.referralOutcome" class="truncate text-success">
                  <span class="font-semibold">Outcome:</span> {{ r.referralOutcome }}
                </p>
                <p v-else-if="r.referralNotes" class="line-clamp-2 text-muted-foreground md:line-clamp-1">
                  {{ r.referralNotes }}
                </p>
              </div>

              <span class="hidden text-xs text-muted-foreground tabular-nums md:block">{{ shortDate(r.createdAt) }}</span>

              <div class="relative z-[1] col-start-2 md:col-start-auto">
                <ReferralStateDropdown v-model="r.referralState" :referral-id="r.id" :label="`Status for ${r.patientName}`" class="w-full! min-w-0!" />
              </div>
            </div>
          </li>
        </ul>
      </template>
    </AppPanel>
  </div>
</template>
