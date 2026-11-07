<script setup lang="ts">
/**
 * Existing visits for a face match, each with its journey (every step opens
 * that station). The visit itself opens its next station, or the patient
 * record once every needed station is done.
 */
const props = defineProps<{ patientId: number }>()
const { data, isLoading } = usePatientVisits(() => props.patientId)
// Most recent first; numbered oldest = #1 so the newest carries the highest number.
const visits = computed(() =>
  [...(data.value?.visits ?? [])].sort(
    (a, b) => new Date(b.date).getTime() - new Date(a.date).getTime() || b.id - a.id,
  ),
)
</script>

<template>
  <p v-if="isLoading" class="text-xs text-muted-foreground touch:text-sm">
    Loading visits…
  </p>
  <p v-else-if="visits.length === 0" class="text-xs text-muted-foreground touch:text-sm">
    No previous visits.
  </p>
  <ul v-else class="flex flex-col gap-1" :aria-label="`Existing visits (${visits.length})`">
    <li v-for="(visit, index) in visits" :key="visit.id" class="flex flex-col gap-1 border border-border px-2 py-1">
      <NuxtLink
        :to="visitLink(visit.journey, props.patientId, visit.id).to"
        class="group flex min-w-0 items-center gap-2 text-xs hover:text-primary touch:text-sm"
      >
        <span class="size-2 shrink-0" :style="{ backgroundColor: visit.villageColorHex }" aria-hidden="true" />
        <span class="truncate group-hover:underline">#{{ visits.length - index }} · {{ formatVisitDate(visit.date) }} — {{ visit.villageName }}</span>
        <span class="ml-auto shrink-0 font-semibold text-primary">{{ visitLink(visit.journey, props.patientId, visit.id).label }} →</span>
      </NuxtLink>
      <JourneyTracker :journey="visit.journey" :patient-id="props.patientId" :visit-id="visit.id" size="compact" />
    </li>
  </ul>
</template>
