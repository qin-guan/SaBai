<script setup lang="ts">
/** Existing visits for a face match; each opens that visit's vitals. */
const props = defineProps<{ patientId: number }>()
const { data, isLoading } = usePatientVisits(() => props.patientId)
const visits = computed(() => data.value?.visits ?? [])
</script>

<template>
  <p v-if="isLoading" class="text-xs text-muted-foreground touch:text-sm">
    Loading visits…
  </p>
  <p v-else-if="visits.length === 0" class="text-xs text-muted-foreground touch:text-sm">
    No previous visits.
  </p>
  <ul v-else class="flex flex-wrap gap-1.5" :aria-label="`Existing visits (${visits.length})`">
    <li v-for="visit in visits" :key="visit.id">
      <NuxtLink
        :to="paths.vitalsDetail(patientId, visit.id)"
        class="inline-flex items-center gap-1.5 border border-border px-2 py-0.5 text-xs hover:bg-muted touch:text-sm"
      >
        <span class="size-2 shrink-0" :style="{ backgroundColor: visit.villageColorHex }" aria-hidden="true" />
        {{ formatVisitDate(visit.date) }} — {{ visit.villageName }}
      </NuxtLink>
    </li>
  </ul>
</template>
