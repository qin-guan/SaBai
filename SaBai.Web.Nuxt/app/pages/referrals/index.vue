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
  villageCodeId: selectedVillageId.value ? Number(selectedVillageId.value) : null,
  referralState: selectedStatus.value ? (selectedStatus.value as SaBaiAPIDomainReferralStateKey) : null,
}))

const filteredReferrals = computed(() => {
  const list = referrals.value ?? []
  return list.filter(item => matchesReferral(item, filterOptions.value))
})

const hasActiveFilters = computed(() => !!search.value || !!selectedVillageId.value || !!selectedStatus.value)

function clearFilters() {
  search.value = ''
  selectedVillageId.value = ''
  selectedStatus.value = ''
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <PageHeader
      title="Referrals"
      description="Track patients referred for further care."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Referrals' }]"
    />

    <!-- Filter & search bar -->
    <div class="flex flex-col gap-2 rounded border border-border bg-card p-3 sm:flex-row sm:items-center sm:justify-between">
      <div class="relative flex-1">
        <PhMagnifyingGlass class="text-muted-foreground absolute top-1/2 left-2.5 size-4 -translate-y-1/2" aria-hidden="true" />
        <Input
          v-model="search"
          type="search"
          placeholder="Search by patient name, ID (e.g. SB0012), reason, notes…"
          class="pl-8 text-xs touch:text-sm"
          aria-label="Search referrals"
        />
        <button
          v-if="search"
          type="button"
          class="text-muted-foreground hover:text-foreground absolute top-1/2 right-2.5 -translate-y-1/2"
          aria-label="Clear search"
          @click="search = ''"
        >
          <PhX class="size-3.5" aria-hidden="true" />
        </button>
      </div>

      <div class="flex flex-wrap items-center gap-2">
        <div class="w-40">
          <NativeSelect v-model="selectedVillageId" aria-label="Filter by village" class="text-xs">
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

        <div class="w-44">
          <NativeSelect v-model="selectedStatus" aria-label="Filter by status" class="text-xs">
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
          class="text-xs"
          @click="clearFilters"
        >
          Reset
        </Button>
      </div>
    </div>

    <!-- State messages -->
    <StateMessage v-if="isLoading" kind="loading" message="Loading referrals…" />
    <StateMessage v-else-if="isError" kind="error" message="Failed to load referrals. Please refresh to try again." />
    <AppPanel v-else-if="!referrals || referrals.length === 0" title="Referrals">
      <div class="p-8 text-center text-muted-foreground">
        <PhArrowUpRight class="mx-auto mb-2 size-8 text-muted-foreground/60" aria-hidden="true" />
        <p class="font-medium">No referrals recorded yet</p>
        <p class="text-xs mt-1">Referrals opened during doctor consultations will appear here.</p>
      </div>
    </AppPanel>
    <AppPanel v-else-if="filteredReferrals.length === 0" title="Referrals">
      <div class="p-8 text-center text-muted-foreground">
        <p class="font-medium">No referrals match your search</p>
        <p class="text-xs mt-1">Try adjusting the search query or filters.</p>
        <Button variant="outline" size="sm" class="mt-3 text-xs" @click="clearFilters">
          Clear filters
        </Button>
      </div>
    </AppPanel>

    <!-- Referrals Table -->
    <AppPanel
      v-else
      :title="`Referrals (${filteredReferrals.length}${filteredReferrals.length !== referrals.length ? ` of ${referrals.length}` : ''})`"
      description="Active and completed patient referrals"
      flush
    >
      <div class="overflow-x-auto">
        <table class="w-full text-left text-xs touch:text-sm">
          <thead class="border-b border-border bg-muted/50 text-xs font-semibold text-muted-foreground">
            <tr>
              <th scope="col" class="py-2.5 pr-2 pl-3">Photo</th>
              <th scope="col" class="px-2 py-2.5">Patient</th>
              <th scope="col" class="px-2 py-2.5">Referral summary</th>
              <th scope="col" class="px-2 py-2.5 min-w-[150px]">Status</th>
              <th scope="col" class="py-2.5 pr-3 pl-2 text-right">Actions</th>
            </tr>
          </thead>
          <tbody class="divide-y divide-border">
            <tr
              v-for="referral in filteredReferrals"
              :key="referral.id"
              class="transition-colors hover:bg-muted/30"
            >
              <!-- Photo -->
              <td class="py-2.5 pr-2 pl-3 align-top">
                <PatientPhoto
                  :patient-id="referral.patientId"
                  :has-photo="referral.hasPhoto"
                  :name="referral.patientName"
                  :size="48"
                  zoomable
                />
              </td>

              <!-- Patient info -->
              <td class="px-2 py-2.5 align-top">
                <div class="flex flex-col gap-0.5">
                  <div class="flex items-center gap-1.5 font-semibold text-foreground">
                    <span
                      class="size-2.5 shrink-0 rounded-full"
                      :style="{ backgroundColor: referral.villageColorHex }"
                      :title="referral.villageCode"
                      aria-hidden="true"
                    />
                    <NuxtLink
                      :to="paths.patientDetail(referral.patientId)"
                      class="hover:underline font-mono"
                    >
                      {{ formatPatientCode(referral.villageCode, referral.patientId) }}
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
              <td class="px-2 py-2.5 align-top">
                <div class="flex flex-col gap-1 max-w-md">
                  <p class="font-semibold text-foreground">
                    Referred for: <span class="text-primary">{{ referral.referredFor }}</span>
                  </p>
                  <p v-if="referral.referralNotes" class="text-xs text-muted-foreground line-clamp-2 whitespace-pre-wrap">
                    {{ referral.referralNotes }}
                  </p>
                  <p v-if="referral.referralOutcome" class="text-xs text-emerald-700 dark:text-emerald-400 font-medium line-clamp-1">
                    Outcome: {{ referral.referralOutcome }}
                  </p>
                  <div class="flex flex-wrap items-center gap-x-3 text-xs text-muted-foreground">
                    <span>Visited: {{ formatVisitDate(referral.consultDate) }}</span>
                    <span>Referred: {{ formatVisitDate(referral.createdAt) }}</span>
                  </div>
                </div>
              </td>

              <!-- Status Dropdown -->
              <td class="px-2 py-2.5 align-top">
                <ReferralStateDropdown
                  :referral-id="referral.id"
                  v-model="referral.referralState"
                />
              </td>

              <!-- Actions -->
              <td class="py-2.5 pr-3 pl-2 align-top text-right">
                <div class="flex flex-col sm:flex-row items-end sm:items-center justify-end gap-1.5">
                  <Button as-child variant="outline" size="sm" class="h-7 text-xs">
                    <NuxtLink :to="paths.referralsDetail(referral.id)">
                      <PhPencilSimple class="size-3.5" aria-hidden="true" />
                      <span>Details</span>
                    </NuxtLink>
                  </Button>

                  <Button as-child variant="ghost" size="sm" class="h-7 text-xs">
                    <NuxtLink :to="paths.consultsDetail(referral.patientId)">
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
