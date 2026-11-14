<script setup lang="ts">
/** Read-only vitals and vision for the visit, shown beside the consult form. */
const props = defineProps<{ patientId: string, visitId: string }>()

const vitalsQuery = useVisitVitals(() => props.visitId)
const eyesightQuery = useVisitEyesight(() => props.visitId)
const vitals = computed(() => vitalsQuery.data.value ?? null)
const eyesight = computed(() => eyesightQuery.data.value ?? null)

const show = (v: unknown) => (v === null || v === undefined || v === '' || v === '-' ? '-' : String(v))

const vitalRows = computed(() => {
  const v = vitals.value
  if (!v)
    return []
  return [
    { label: 'Height', value: v.height },
    { label: 'Weight', value: v.weight },
    { label: 'BMI', value: computeBmi(v.height != null ? String(v.height) : null, v.weight != null ? String(v.weight) : null) },
    { label: 'Blood Pressure (Systolic / Diastolic) / mmHg', value: formatBloodPressure(v.systolic, v.diastolic) },
    { label: 'Heart Rate', value: v.heartRate },
    { label: 'Temperature', value: v.temperature },
    { label: 'Urine Dip Test', value: v.urineTest },
    { label: 'Hemocue Hb Count', value: v.hemocueCount },
    { label: 'Non-Fasting Blood Glucose', value: v.bloodGlucoseNonFasting },
    { label: 'Fasting Blood Glucose', value: v.bloodGlucoseFasting },
    { label: 'HbA1c', value: v.hba1c },
    { label: 'Diabetes Mellitus?', value: formatDiabetes(v.diabetesMellitus ?? null) },
  ]
})

const eyeRows = computed(() => {
  const e = eyesight.value
  if (!e)
    return []
  return [
    { label: 'Right Eye', value: e.rightEyeDegree },
    { label: 'Left Eye', value: e.leftEyeDegree },
    { label: 'Right Eye Pinhole', value: e.rightEyePinhole },
    { label: 'Left Eye Pinhole', value: e.leftEyePinhole },
  ]
})
</script>

<template>
  <StateMessage v-if="vitalsQuery.isLoading.value || eyesightQuery.isLoading.value" kind="loading" message="Loading vitals..." />
  <template v-else>
    <AppPanel title="Vitals" flush>
      <template #actions>
        <NuxtLink
          :to="paths.vitalsDetail(props.patientId, props.visitId)"
          class="text-xs font-semibold text-primary hover:underline touch:text-sm"
        >
          {{ vitals ? 'Edit Vitals →' : 'Record Vitals →' }}
        </NuxtLink>
      </template>
      <StateMessage v-if="!vitals" message="No vitals recorded for this visit." />
      <template v-else>
        <dl class="grid grid-cols-2 sm:grid-cols-4">
          <div v-for="row in vitalRows" :key="row.label" class="border-r border-b border-border px-2 py-1.5">
            <dt class="text-xs touch:text-xs font-semibold text-muted-foreground">
              {{ row.label }}
            </dt>
            <dd class="font-semibold tabular-nums">
              {{ show(row.value) }}
            </dd>
          </div>
        </dl>
        <div class="px-2 py-1.5">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            Others
          </p>
          <p class="whitespace-pre-wrap">
            {{ show(vitals.others) }}
          </p>
        </div>
      </template>
    </AppPanel>
    <AppPanel title="Vision" flush>
      <template #actions>
        <NuxtLink
          :to="paths.visionUpdateGlasses(props.patientId, props.visitId)"
          class="text-xs font-semibold text-primary hover:underline touch:text-sm"
        >
          {{ eyesight ? 'Edit Vision →' : 'Record Vision →' }}
        </NuxtLink>
      </template>
      <StateMessage v-if="!eyesight" message="No vision recorded for this visit." />
      <dl v-else class="grid grid-cols-2 sm:grid-cols-4">
        <div v-for="row in eyeRows" :key="row.label" class="border-r border-b border-border px-2 py-1.5">
          <dt class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ row.label }}
          </dt>
          <dd class="font-mono font-semibold">
            {{ show(row.value) }}
          </dd>
        </div>
      </dl>
    </AppPanel>
  </template>
</template>
