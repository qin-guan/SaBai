<script setup lang="ts">
import { PhMagnifyingGlass } from '@phosphor-icons/vue'
import type { PatientLinkTarget } from '~/utils/paths'

/**
 * Global patient lookup (ARIA combobox). "/" focuses it from anywhere,
 * ↑/↓ move through matches, Enter opens, Esc clears. Results open the station
 * page the current screen belongs to (e.g. on Vitals they open the patient's
 * vitals), otherwise the patient record.
 */
const route = useRoute()
const input = ref<HTMLInputElement>()
const searchText = ref('')
const debounced = useDebouncedRef(searchText, 300)
const activeIndex = ref(0)
const focused = ref(false)

const { data, isLoading, isRefetching } = usePatientsList()

const results = computed(() => {
  if (!debounced.value.trim())
    return []
  return (data.value?.patients ?? []).filter(p => patientMatches(p, debounced.value))
})
watch(results, () => (activeIndex.value = 0))

const open = computed(() => focused.value && debounced.value.trim() !== '')
const listId = useId()
const optionId = (index: number) => `${listId}-opt-${index}`

function linkFor(patientId: number) {
  return patientLinkFor(route.meta.patientLink as PatientLinkTarget | undefined, patientId)
}

async function choose(index: number) {
  const patient = results.value[index]
  if (!patient)
    return
  searchText.value = ''
  input.value?.blur()
  await navigateTo(linkFor(patient.id))
}

function onKeydown(event: KeyboardEvent) {
  const count = results.value.length
  if (event.key === 'ArrowDown' && count) {
    event.preventDefault()
    activeIndex.value = (activeIndex.value + 1) % count
  }
  else if (event.key === 'ArrowUp' && count) {
    event.preventDefault()
    activeIndex.value = (activeIndex.value - 1 + count) % count
  }
  else if (event.key === 'Enter' && count) {
    event.preventDefault()
    choose(activeIndex.value)
  }
  else if (event.key === 'Escape') {
    if (searchText.value)
      searchText.value = ''
    else input.value?.blur()
  }
}

watch(activeIndex, (index) => {
  nextTick(() => document.getElementById(optionId(index))?.scrollIntoView({ block: 'nearest' }))
})

defineExpose({ focus: () => input.value?.focus() })
</script>

<template>
  <div class="relative w-full max-w-xl">
    <PhMagnifyingGlass
      class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground"
      aria-hidden="true"
    />
    <input
      ref="input"
      v-model="searchText"
      type="search"
      role="combobox"
      autocomplete="off"
      spellcheck="false"
      aria-label="Search patients by name, ID or identification number"
      aria-autocomplete="list"
      aria-keyshortcuts="/"
      :aria-expanded="open"
      :aria-controls="listId"
      :aria-activedescendant="open && results.length ? optionId(activeIndex) : undefined"
      placeholder="Search patients…"
      class="h-8 w-full border border-input bg-card pr-10 pl-8 text-xs text-foreground outline-none placeholder:text-muted-foreground focus-visible:border-ring focus-visible:ring-1 focus-visible:ring-ring/50 [@media(pointer:coarse)]:h-11 [@media(pointer:coarse)]:text-base"
      @focus="focused = true"
      @blur="focused = false"
      @keydown="onKeydown"
    >
    <ShortcutHint
      v-if="!searchText"
      keys="/"
      class="pointer-events-none absolute top-1/2 right-2 -translate-y-1/2 [@media(pointer:coarse)]:hidden"
    />

    <div
      v-show="open"
      class="absolute top-full right-0 left-0 z-50 mt-1 border border-border bg-popover text-popover-foreground shadow-lg"
    >
      <p v-if="isLoading || isRefetching" class="px-3 py-2 text-xs text-muted-foreground" role="status">
        Loading patients…
      </p>
      <p v-else-if="results.length === 0" class="px-3 py-2 text-xs text-muted-foreground" role="status">
        No matching patients found.
      </p>
      <ul
        v-else
        :id="listId"
        role="listbox"
        aria-label="Matching patients"
        class="max-h-[min(70dvh,32rem)] overflow-y-auto py-1"
      >
        <li
          v-for="(patient, index) in results"
          :id="optionId(index)"
          :key="patient.id"
          role="option"
          :aria-selected="index === activeIndex"
          class="flex cursor-pointer items-center gap-2 px-2 py-1.5"
          :class="index === activeIndex ? 'bg-accent text-accent-foreground' : 'hover:bg-muted'"
          @mousedown.prevent="choose(index)"
          @mousemove="activeIndex = index"
        >
          <PatientPhoto :patient-id="patient.id" :has-photo="patient.hasPhoto" :size="36" alt="" />
          <div class="min-w-0 flex-1">
            <p class="truncate text-sm font-semibold">
              {{ patient.name }}
            </p>
            <p class="text-xs text-muted-foreground">
              ID {{ patient.id }} · {{ calculateAge(patient.dateOfBirth) }} yrs · <span class="capitalize">{{ patient.gender }}</span>
            </p>
          </div>
        </li>
      </ul>
    </div>
    <span class="sr-only" aria-live="polite">
      {{ open && !isLoading ? `${results.length} patients found` : '' }}
    </span>
  </div>
</template>
