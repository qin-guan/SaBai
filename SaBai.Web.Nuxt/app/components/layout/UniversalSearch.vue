<script setup lang="ts">
import type { Component } from 'vue'
import { PhClockCounterClockwise, PhKeyboard, PhMagnifyingGlass, PhSignOut, PhX } from '@phosphor-icons/vue'
import { DialogContent, DialogDescription, DialogOverlay, DialogPortal, DialogRoot, DialogTitle } from 'reka-ui'
import type { PatientLinkTarget } from '~/utils/paths'

/**
 * Universal search (header box, "/" or Ctrl/⌘+K).
 *
 * One box for everything: patients (MiniSearch over name, ID / patient code,
 * ID number, phone, village — prefix + typo tolerant), sections and sub-pages
 * (with clinical keywords: "bp" → Vitals) and actions. Empty query shows
 * recently opened patients. Implements the ARIA combobox + listbox pattern.
 *
 *   ↑/↓ move · Enter open · Shift+Enter open the patient record · Esc close
 *
 * On a station page (Vitals, Vision, Consults, Dental) Enter on a patient opens
 * that station for them; each row also has quick links to every station.
 */
const open = defineModel<boolean>('open', { required: true })
const emit = defineEmits<{ showShortcuts: [] }>()

const route = useRoute()
const query = ref('')
const debounced = useDebouncedRef(query, 80)
const activeIndex = ref(0)
const listId = useId()
const optionId = (i: number) => `${listId}-o${i}`

const { data: patientsData, isLoading: patientsLoading } = usePatientsList()
const patients = computed(() => patientsData.value?.patients ?? [])
const patientsById = computed(() => new Map(patients.value.map(p => [p.id, p])))
const patientIndex = computed(() => buildPatientIndex(patients.value))

const pages = [...NAV_ITEMS, ...SUB_PAGES]
const pageIndex = buildPageIndex(pages.map(p => ({ id: p.href, title: p.name, keywords: p.keywords })))

const { recentIds, remember } = useRecentPatients()

const stationTarget = computed(() => route.meta.patientLink as PatientLinkTarget | undefined)
const stationName = computed(() => ({
  vitals: 'Vitals',
  vision: 'Vision',
  consults: 'Consults',
  dental: 'Dental',
  patient: 'patient record',
})[stationTarget.value ?? 'patient'])

type Patient = (typeof patients.value)[number]
interface Item {
  key: string
  kind: 'patient' | 'page' | 'action'
  label: string
  icon?: Component
  patient?: Patient
  run: (record?: boolean) => void
}
interface Group { title: string, items: Item[] }

const QUICK_LINKS: { label: string, target: PatientLinkTarget }[] = [
  { label: 'Record', target: 'patient' },
  { label: 'Vitals', target: 'vitals' },
  { label: 'Vision', target: 'vision' },
  { label: 'Consult', target: 'consults' },
  { label: 'Dental', target: 'dental' },
]

function go(to: string) {
  open.value = false
  navigateTo(to)
}

function openPatient(p: Patient, target?: PatientLinkTarget) {
  remember(p.id)
  go(patientLinkFor(target, p.id))
}

const patientItem = (p: Patient): Item => ({
  key: `p${p.id}`,
  kind: 'patient',
  label: p.name,
  patient: p,
  run: record => openPatient(p, record ? 'patient' : stationTarget.value),
})

const pageItem = (p: (typeof pages)[number]): Item => ({
  key: p.href,
  kind: 'page',
  label: p.name,
  icon: p.icon,
  run: () => go(p.href),
})

const actions: Item[] = [
  { key: 'shortcuts', kind: 'action', label: 'Keyboard shortcuts', icon: PhKeyboard, run: () => { open.value = false; emit('showShortcuts') } },
  { key: 'sign-out', kind: 'action', label: 'Sign out', icon: PhSignOut, run: () => { open.value = false; signOut() } },
]

const MAX_PATIENTS = 25

const groups = computed<Group[]>(() => {
  const q = debounced.value.trim()
  if (!q) {
    const recent = recentIds.value.map(id => patientsById.value.get(id)).filter((p): p is Patient => !!p)
    return [
      { title: 'Recent patients', items: recent.map(patientItem) },
      { title: 'Go to', items: NAV_ITEMS.map(pageItem) },
    ].filter(g => g.items.length)
  }
  const patientHits = searchIndex(patientIndex.value, q)
    .slice(0, MAX_PATIENTS)
    .map(r => patientsById.value.get(r.id as number))
    .filter((p): p is Patient => !!p)
  const pageHits = searchIndex(pageIndex, q)
    .slice(0, 5)
    .map(r => pages.find(p => p.href === r.id)!)
  const actionHits = actions.filter(a => a.label.toLowerCase().includes(q.toLowerCase()))
  return [
    { title: 'Go to', items: pageHits.map(pageItem) },
    { title: 'Patients', items: patientHits.map(patientItem) },
    { title: 'Actions', items: actionHits },
  ].filter(g => g.items.length)
})

const flat = computed(() => groups.value.flatMap(g => g.items))
const indexOf = (item: Item) => flat.value.indexOf(item)
watch(flat, () => (activeIndex.value = 0))

function onKeydown(event: KeyboardEvent) {
  const count = flat.value.length
  if (event.key === 'ArrowDown' || (event.key === 'n' && event.ctrlKey)) {
    event.preventDefault()
    if (count)
      activeIndex.value = (activeIndex.value + 1) % count
  }
  else if (event.key === 'ArrowUp' || (event.key === 'p' && event.ctrlKey)) {
    event.preventDefault()
    if (count)
      activeIndex.value = (activeIndex.value - 1 + count) % count
  }
  else if (event.key === 'Home' && event.ctrlKey) {
    activeIndex.value = 0
  }
  else if (event.key === 'Enter') {
    event.preventDefault()
    flat.value[activeIndex.value]?.run(event.shiftKey)
  }
}

watch(activeIndex, (i) => {
  nextTick(() => document.getElementById(optionId(i))?.scrollIntoView({ block: 'nearest' }))
})

// Fresh search every time it opens.
watch(open, (isOpen) => {
  if (isOpen) {
    query.value = ''
    activeIndex.value = 0
  }
})

async function signOut() {
  clearToken()
  clearAllFormDrafts()
  clearRecentPatients()
  await navigateTo(paths.login())
}

const resultSummary = computed(() => {
  if (!open.value || !debounced.value.trim())
    return ''
  const n = flat.value.length
  return n ? `${n} result${n === 1 ? '' : 's'}` : 'No results'
})
</script>

<template>
  <DialogRoot v-model:open="open">
    <DialogPortal>
      <DialogOverlay class="fixed inset-0 z-50 bg-black/40 data-[state=open]:animate-in data-[state=open]:fade-in-0" />
      <DialogContent
        class="fixed inset-x-0 top-0 z-50 flex max-h-dvh flex-col border border-border bg-popover text-popover-foreground shadow-2xl outline-none sm:inset-x-auto sm:top-[12dvh] sm:left-1/2 sm:max-h-[76dvh] sm:w-[min(42rem,calc(100vw-2rem))] sm:-translate-x-1/2"
        @open-auto-focus.prevent="($event.target as HTMLElement | null)?.querySelector<HTMLInputElement>('input')?.focus()"
      >
        <DialogTitle class="sr-only">
          Search
        </DialogTitle>
        <DialogDescription class="sr-only">
          Search patients, sections and actions. Use the up and down arrows to move, Enter to open, Shift+Enter to open the patient record, Escape to close.
        </DialogDescription>

        <div class="flex items-center gap-2 border-b border-border bg-band px-3 pt-[env(safe-area-inset-top)]">
          <PhMagnifyingGlass class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
          <input
            v-model="query"
            type="text"
            role="combobox"
            autocomplete="off"
            autocapitalize="off"
            spellcheck="false"
            enterkeyhint="go"
            aria-label="Search patients, sections and actions"
            aria-autocomplete="list"
            aria-expanded="true"
            :aria-controls="listId"
            :aria-activedescendant="flat.length ? optionId(activeIndex) : undefined"
            placeholder="Patient name, ID, phone, or a section…"
            class="h-12 min-w-0 flex-1 bg-transparent text-sm outline-none placeholder:text-muted-foreground touch:text-base"
            @keydown="onKeydown"
          >
          <button
            type="button"
            class="inline-flex size-9 shrink-0 cursor-pointer items-center justify-center text-muted-foreground hover:bg-accent touch:size-11"
            aria-label="Close search"
            @click="open = false"
          >
            <PhX aria-hidden="true" />
          </button>
        </div>

        <div class="min-h-0 flex-1 overflow-y-auto overscroll-contain">
          <p
            v-if="debounced.trim() && !flat.length"
            class="px-3 py-6 text-center text-xs text-muted-foreground touch:text-sm"
          >
            {{ patientsLoading ? 'Loading patients…' : `Nothing matches “${debounced.trim()}”.` }}
          </p>
          <ul :id="listId" role="listbox" aria-label="Search results" class="py-1">
            <template v-for="group in groups" :key="group.title">
              <li
                role="presentation"
                class="flex items-center gap-1.5 px-3 pt-2 pb-1 text-[0.6875rem] font-bold tracking-wide text-chrome uppercase touch:text-xs"
              >
                <PhClockCounterClockwise v-if="group.title === 'Recent patients'" class="size-3.5" aria-hidden="true" />
                {{ group.title }}
              </li>
              <li
                v-for="item in group.items"
                :id="optionId(indexOf(item))"
                :key="item.key"
                role="option"
                :aria-selected="indexOf(item) === activeIndex"
                class="flex cursor-pointer items-center gap-2.5 px-3 py-1.5 touch:min-h-12 touch:py-2"
                :class="indexOf(item) === activeIndex ? 'bg-accent text-accent-foreground' : 'hover:bg-muted'"
                @click="item.run(false)"
                @mousemove="activeIndex = indexOf(item)"
              >
                <template v-if="item.patient">
                  <PatientPhoto :patient-id="item.patient.id" :has-photo="item.patient.hasPhoto" :size="32" alt="" />
                  <div class="min-w-0 flex-1">
                    <p class="truncate text-sm font-semibold touch:text-base">
                      {{ item.patient.name }}
                    </p>
                    <p class="truncate text-xs text-muted-foreground touch:text-sm">
                      <span class="font-mono">{{ formatPatientCode(item.patient.villageCode, item.patient.id) }}</span>
                      · {{ calculateAge(item.patient.dateOfBirth) }} y · <span class="capitalize">{{ item.patient.gender }}</span>
                      <template v-if="item.patient.identificationNumber">
                        · {{ item.patient.identificationNumber }}
                      </template>
                    </p>
                  </div>
                  <!-- Quick links (mouse / touch); keyboard uses Enter / Shift+Enter. -->
                  <div class="hidden shrink-0 gap-px md:flex" @click.stop>
                    <button
                      v-for="link in QUICK_LINKS"
                      :key="link.target"
                      type="button"
                      tabindex="-1"
                      class="cursor-pointer border border-border bg-card px-1.5 py-0.5 text-[0.6875rem] hover:border-primary hover:text-primary"
                      :class="(stationTarget ?? 'patient') === link.target && 'border-primary text-primary'"
                      @click="openPatient(item.patient, link.target)"
                    >
                      {{ link.label }}
                    </button>
                  </div>
                </template>
                <template v-else>
                  <component :is="item.icon" class="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                  <span class="flex-1 text-sm touch:text-base">{{ item.label }}</span>
                  <span v-if="item.kind === 'page'" class="text-[0.6875rem] text-muted-foreground">Section</span>
                </template>
              </li>
            </template>
          </ul>
        </div>

        <footer class="hidden flex-wrap items-center gap-x-3 gap-y-1 border-t border-border bg-stripe px-3 py-1.5 text-[0.6875rem] text-muted-foreground kb:flex">
          <span><ShortcutHint keys="↑" /> <ShortcutHint keys="↓" /> move</span>
          <span><ShortcutHint keys="Enter" /> open<template v-if="stationTarget && stationTarget !== 'patient'"> in {{ stationName }}</template></span>
          <span><ShortcutHint keys="Shift+Enter" /> patient record</span>
          <span><ShortcutHint keys="Esc" /> close</span>
        </footer>
        <span class="sr-only" aria-live="polite">{{ resultSummary }}</span>
      </DialogContent>
    </DialogPortal>
  </DialogRoot>
</template>
