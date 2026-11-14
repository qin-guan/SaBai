<script setup lang="ts">
import { PhArrowUpRight, PhMagnifyingGlass, PhPencilSimple, PhStethoscope, PhX } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { matchesReferral, REFERRAL_STATE_OPTIONS } from '@/utils/referral'

useHead({ title: "Referrals · Sa'Bai" })

const { data: referrals, isLoading, isError } = useReferralsList()
const { villageCodes } = useVillageCode()

const search = ref('')
const selectedVillageId = ref<string>('')
const selectedStatus = ref<string>('')

const filterOptions = computed(() => ({
  search: search.value,
  villageCodeId: selectedVillageId.value || null,
  referralState: selectedStatus.value ? (selectedStatus.value as SaBaiAPIDomainReferralStateKey) : null,
}))

const filteredReferrals = computed(() => {
  const list = referrals.value ?? []
  return list.filter(item => matchesReferral(item, filterOptions.value))
})

const hasActiveFilters = computed(() => !!search.value || !!selectedVillageId.value || !!selectedStatus.value)

/** Esc in the search box clears it first; a second Esc leaves the field, a third goes back. */
function onSearchEsc(event: KeyboardEvent) {
  if (!search.value) return
  event.stopPropagation()
  search.value = ''
}

function clearFilters() {
  search.value = ''
  selectedVillageId.value = ''
  selectedStatus.value = ''
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="Referrals"
      description="Track patients referred for further care."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Referrals' }]"
      class="mb-0!"
    />

    <!-- Filter & search bar -->
    <div class="border border-border bg-card p-2">
      <div class="flex flex-col gap-2 md:flex-row md:items-center md:justify-between">
        <div class="relative flex-1">
          <PhMagnifyingGlass class="text-muted-foreground pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden="true" />
          <Input
            v-model="search"
            type="search"
            placeholder="Search name, ID, reason, notes…"
            class="h-8 pl-8 text-xs border-input touch:h-11 touch:text-base"
            aria-label="Search referrals"
            @keydown.esc="onSearchEsc"
          />
          <button
            v-if="search"
            type="button"
            class="text-muted-foreground hover:text-foreground absolute top-1/2 right-2.5 -translate-y-1/2 p-1"
            aria-label="Clear search"
            @click="search = ''"
          >
            <PhX class="size-3.5" aria-hidden="true" />
          </button>
        </div>

        <div class="grid grid-cols-2 gap-2 sm:flex sm:items-center sm:gap-2">
          <div class="w-full sm:w-40">
            <NativeSelect v-model="selectedVillageId" aria-label="Filter by village" class="text-xs touch:h-11">
              <NativeSelectOption value="">
                All villages
              </NativeSelectOption>
              <NativeSelectOption
                v-for="v in villageCodes"
                :key="v.id"
                :value="String(v.id)"
              >
                {{ v.name }} ({{ v.code }})
              </NativeSelectOption>
            </NativeSelect>
          </div>

          <div class="w-full sm:w-44">
            <NativeSelect v-model="selectedStatus" aria-label="Filter by status" class="text-xs touch:h-11">
              <NativeSelectOption value="">
                All statuses
              </NativeSelectOption>
              <NativeSelectOption
                v-for="opt in REFERRAL_STATE_OPTIONS"
                :key="opt.value"
                :value="opt.value"
              >
                {{ opt.label }}
              </NativeSelectOption>
            </NativeSelect>
          </div>

          <Button
            v-if="hasActiveFilters"
            variant="ghost"
            size="sm"
            class="col-span-2 text-xs sm:col-span-1 touch:h-10"
            @click="clearFilters"
          >
            Reset
          </Button>
        </div>
      </div>
    </div>

    <!-- State messages -->
    <StateMessage v-if="isLoading" kind="loading" message="Loading referrals…" />
    <StateMessage v-else-if="isError" kind="error" message="Failed to load referrals. Please refresh to try again." />
    <AppPanel v-else-if="!referrals || referrals.length === 0" title="Referrals">
      <div class="flex flex-col items-center gap-1 px-3 py-10 text-center text-muted-foreground" role="status">
        <PhArrowUpRight class="mb-1 size-8 text-muted-foreground/60" aria-hidden="true" />
        <p class="font-medium text-foreground">No referrals recorded yet</p>
        <p class="text-xs touch:text-sm">Referrals opened during doctor consultations will appear here.</p>
      </div>
    </AppPanel>
    <AppPanel v-else-if="filteredReferrals.length === 0" title="Referrals">
      <div class="flex flex-col items-center gap-1 px-3 py-10 text-center text-muted-foreground" role="status">
        <p class="font-medium text-foreground">No referrals match your search</p>
        <p class="text-xs touch:text-sm">Try adjusting the search query or filters.</p>
        <Button variant="outline" size="sm" class="mt-2" @click="clearFilters">
          Clear filters
        </Button>
      </div>
    </AppPanel>

    <!-- Referrals List / Table -->
    <AppPanel
      v-else
      :title="`Referrals (${filteredReferrals.length}${filteredReferrals.length !== referrals.length ? ` of ${referrals.length}` : ''})`"
      flush
    >
      <!-- Mobile card list (< md) -->
      <div class="divide-y divide-border md:hidden">
        <article
          v-for="referral in filteredReferrals"
          :key="referral.id"
          class="flex flex-col gap-2.5 p-3 hover:bg-accent/30 transition-colors"
        >
          <!-- Top row: Photo + full-width Patient Name & Code -->
          <div class="flex items-center gap-2.5 min-w-0">
            <PatientPhoto
              :patient-id="referral.patientId"
              :has-photo="referral.hasPhoto"
              :name="referral.patientName"
              :size="40"
              zoomable
            />
            <div class="min-w-0 flex-1">
              <div class="flex items-center gap-2">
                <NuxtLink
                  :to="paths.patientDetail(referral.patientId)"
                  class="truncate text-sm font-bold text-foreground hover:text-primary hover:underline touch:text-base"
                >
                  {{ referral.patientName }}
                </NuxtLink>
              </div>
              <div class="flex items-center gap-1.5 text-xs text-muted-foreground">
                <PatientCode
                  :village-code="referral.villageCode"
                  :village-color-hex="referral.villageColorHex"
                  :id="referral.patientNumber"
                />
              </div>
            </div>
          </div>

          <!-- Status row -->
          <div class="flex items-center justify-between gap-3">
            <span class="text-xs font-semibold text-muted-foreground shrink-0">Status</span>
            <ReferralStateDropdown
              :referral-id="referral.id"
              v-model="referral.referralState"
              class="max-w-[220px]"
            />
          </div>

          <!-- Referral detail box -->
          <div class="flex flex-col gap-1.5 border-l-2 border-band-border bg-stripe px-2.5 py-2 text-xs">
            <div class="flex flex-wrap items-baseline gap-x-1.5">
              <span class="font-semibold text-muted-foreground">Referred for:</span>
              <span class="font-bold text-chrome">{{ referral.referredFor }}</span>
            </div>
            <p v-if="referral.referralNotes" class="text-xs text-foreground whitespace-pre-wrap break-words">
              {{ referral.referralNotes }}
            </p>
            <div
              v-if="referral.referralOutcome"
              class="border-l-2 border-success/60 bg-success-subtle/50 px-2 py-1 text-xs text-foreground"
            >
              <span class="font-semibold text-success">Outcome: </span>
              <span>{{ referral.referralOutcome }}</span>
            </div>
            <div class="flex flex-wrap items-center gap-x-3 gap-y-0.5 text-[0.75rem] text-muted-foreground pt-0.5">
              <span>Visited: {{ formatVisitDate(referral.consultDate) }}</span>
              <span>Referred: {{ formatVisitDate(referral.createdAt) }}</span>
            </div>
          </div>

          <!-- Bottom Action Buttons: 3 equal touch targets -->
          <div class="grid grid-cols-3 gap-1.5">
            <Button as-child variant="outline" size="sm" class="h-9 text-xs touch:h-10">
              <NuxtLink :to="paths.referralsDetail(referral.id)">
                <PhPencilSimple class="size-3.5" aria-hidden="true" />
                <span>Details</span>
              </NuxtLink>
            </Button>

            <Button as-child variant="outline" size="sm" class="h-9 text-xs touch:h-10">
              <NuxtLink :to="paths.consultsDetail(referral.patientId, undefined, referral.consultId)">
                <PhStethoscope class="size-3.5" aria-hidden="true" />
                <span>Consult</span>
              </NuxtLink>
            </Button>

            <ConsultPdfLink
              :consult-id="referral.consultId"
              variant="outline"
              size="sm"
              label="PDF"
              class="h-9 text-xs touch:h-10"
            />
          </div>
        </article>
      </div>

      <!-- Desktop table (hidden on mobile, visible md and up) -->
      <div class="hidden md:block overflow-x-auto">
        <table class="grid-table w-full text-left text-xs">
          <thead>
            <tr>
              <th scope="col" class="py-2 pr-2 pl-3 w-12"><span class="sr-only">Photo</span></th>
              <th scope="col" class="px-2 py-2 min-w-[140px]">Patient</th>
              <th scope="col" class="px-2 py-2 min-w-[200px]">Referral summary</th>
              <th scope="col" class="px-2 py-2 min-w-[180px]">Status</th>
              <th scope="col" class="py-2 pr-3 pl-2 text-right min-w-[180px]">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr
              v-for="referral in filteredReferrals"
              :key="referral.id"
              class="transition-colors hover:bg-accent/60"
            >
              <!-- Photo -->
              <td class="py-2 pr-2 pl-3 align-top">
                <PatientPhoto
                  :patient-id="referral.patientId"
                  :has-photo="referral.hasPhoto"
                  :name="referral.patientName"
                  :size="40"
                  zoomable
                />
              </td>

              <!-- Patient info -->
              <td class="px-2 py-2 align-top">
                <div class="flex flex-col gap-0.5">
                  <div class="flex items-center gap-1.5 font-bold text-chrome">
                    <span
                      class="size-2.5 shrink-0 border border-border/40"
                      :style="{ backgroundColor: referral.villageColorHex }"
                      :title="referral.villageCode ?? undefined"
                      aria-hidden="true"
                    />
                    <NuxtLink
                      :to="paths.patientDetail(referral.patientId)"
                      class="hover:underline font-mono"
                    >
                      {{ referral.patientDisplayId }}
                    </NuxtLink>
                  </div>
                  <p class="font-medium text-foreground">
                    {{ referral.patientName }}
                  </p>
                  <p class="text-xs text-muted-foreground">
                    Village: {{ referral.villageCode }}
                  </p>
                </div>
              </td>

              <!-- Referral details -->
              <td class="px-2 py-2 align-top">
                <div class="flex flex-col gap-1 max-w-md">
                  <p class="font-semibold text-foreground">
                    Referred for: <span class="font-bold text-chrome">{{ referral.referredFor }}</span>
                  </p>
                  <p v-if="referral.referralNotes" class="text-xs text-muted-foreground line-clamp-2 whitespace-pre-wrap">
                    {{ referral.referralNotes }}
                  </p>
                  <div v-if="referral.referralOutcome" class="border-l-2 border-success/60 bg-success-subtle/40 px-2 py-0.5 text-xs text-foreground">
                    <span class="font-semibold text-success">Outcome: </span>
                    <span class="line-clamp-1">{{ referral.referralOutcome }}</span>
                  </div>
                  <div class="flex flex-wrap items-center gap-x-3 text-[0.75rem] text-muted-foreground">
                    <span>Visited: {{ formatVisitDate(referral.consultDate) }}</span>
                    <span>Referred: {{ formatVisitDate(referral.createdAt) }}</span>
                  </div>
                </div>
              </td>

              <!-- Status Dropdown -->
              <td class="px-2 py-2 align-top">
                <ReferralStateDropdown
                  :referral-id="referral.id"
                  v-model="referral.referralState"
                />
              </td>

              <!-- Actions -->
              <td class="py-2 pr-3 pl-2 align-top text-right">
                <div class="flex items-center justify-end gap-1.5">
                  <Button as-child variant="outline" size="sm" class="h-7 text-xs">
                    <NuxtLink :to="paths.referralsDetail(referral.id)">
                      <PhPencilSimple class="size-3.5" aria-hidden="true" />
                      <span>Details</span>
                    </NuxtLink>
                  </Button>

                  <Button as-child variant="outline" size="sm" class="h-7 text-xs">
                    <NuxtLink :to="paths.consultsDetail(referral.patientId, undefined, referral.consultId)">
                      <PhStethoscope class="size-3.5" aria-hidden="true" />
                      <span>Consult</span>
                    </NuxtLink>
                  </Button>

                  <ConsultPdfLink
                    :consult-id="referral.consultId"
                    variant="outline"
                    size="sm"
                    label="PDF"
                  />
                </div>
              </td>
            </tr>
          </tbody>
        </table>
      </div>
    </AppPanel>
  </div>
</template>
