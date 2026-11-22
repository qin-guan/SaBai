<script setup lang="ts">
/** Read-only vitals and vision for the visit, shown beside the consult form. */
const props = defineProps<{ patientId: string, visitId: string }>()

const vitalsQuery = useVisitVitals(() => props.visitId)
const eyesightQuery = useVisitEyesight(() => props.visitId)
const vitals = computed(() => vitalsQuery.data.value ?? null)
const eyesight = computed(() => eyesightQuery.data.value ?? null)

const show = (v: unknown) => (v === null || v === undefined || v === '' || v === '-' ? '-' : String(v))

type SummaryRow = { label: string, value: unknown, alert?: boolean }

/** Vitals grouped as on the vitals form; a positive diabetes answer stands out. */
const vitalGroups = computed<{ title: string, rows: SummaryRow[] }[]>(() => {
  const v = vitals.value
  if (!v)
    return []
  return [
    {
      title: 'Anthropometric',
      rows: [
        { label: 'Height', value: v.height },
        { label: 'Weight', value: v.weight },
        { label: 'BMI', value: computeBmi(v.height != null ? String(v.height) : null, v.weight != null ? String(v.weight) : null) },
      ],
    },
    {
      title: 'Cardiovascular / General',
      rows: [
        { label: 'Blood Pressure (Systolic / Diastolic) / mmHg', value: formatBloodPressure(v.systolic, v.diastolic) },
        { label: 'Heart Rate', value: v.heartRate },
        { label: 'Temperature', value: v.temperature },
        { label: 'Hemocue Hb Count', value: v.hemocueCount },
      ],
    },
    {
      title: 'Metabolic',
      rows: [
        { label: 'Non-Fasting Blood Glucose', value: v.bloodGlucoseNonFasting },
        { label: 'Fasting Blood Glucose', value: v.bloodGlucoseFasting },
        { label: 'HbA1c', value: v.hba1c },
        { label: 'Diabetes Mellitus?', value: formatDiabetes(v.diabetesMellitus ?? null), alert: v.diabetesMellitus === true },
      ],
    },
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
        <section v-for="group in vitalGroups" :key="group.title" :aria-label="group.title">
          <h3 class="border-b border-border bg-muted/40 px-2 py-1 text-xs touch:text-xs font-semibold uppercase tracking-wide text-foreground">
            {{ group.title }}
          </h3>
          <dl class="grid grid-cols-2 sm:grid-cols-4">
            <div v-for="row in group.rows" :key="row.label" class="border-r border-b border-border px-2 py-1.5">
              <dt class="text-xs touch:text-xs font-semibold text-muted-foreground">
                {{ row.label }}
              </dt>
              <dd class="font-semibold tabular-nums" :class="row.alert && 'font-bold text-destructive'">
                {{ show(row.value) }}
              </dd>
            </div>
          </dl>
        </section>
        <div class="border-b border-border px-2 py-1.5">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            Urinalysis Diagnostics
          </p>
          <p class="whitespace-pre-wrap">
            {{ show(vitals.urineTest) }}
          </p>
        </div>
        <div class="px-2 py-1.5">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            Additional Clinical Remarks
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
