<script setup lang="ts">
import { PhFloppyDisk, PhWarning } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'

const props = defineProps<{
  visitId: string
  visitDate: string
  patient: { dateOfBirth: string, gender: string }
}>()

type NumberKey =
  | 'height' | 'weight' | 'temperature' | 'systolic' | 'diastolic' | 'heartRate'
  | 'hemocueCount' | 'bloodGlucoseNonFasting' | 'bloodGlucoseFasting' | 'hba1c'
type VitalsFormValues = Record<NumberKey, string> & {
  diabetesMellitus: string
  urineTest: string
  others: string
}

const NUMBER_KEYS: NumberKey[] = [
  'height', 'weight', 'temperature', 'systolic', 'diastolic', 'heartRate',
  'hemocueCount', 'bloodGlucoseNonFasting', 'bloodGlucoseFasting', 'hba1c',
]

const blank = (): VitalsFormValues => ({
  height: '', weight: '', temperature: '', systolic: '', diastolic: '', heartRate: '',
  hemocueCount: '', bloodGlucoseNonFasting: '', bloodGlucoseFasting: '', hba1c: '',
  diabetesMellitus: '', urineTest: '', others: '',
})

const { data: vitalData, isLoading } = useVisitVitals(() => props.visitId)
const form = ref<VitalsFormValues>(blank())
const draft = useFormDraft(() => `vitals:${props.visitId}`, form)
const baseline = ref('')
const isDirty = computed(() => JSON.stringify(form.value) !== baseline.value)

// Load the saved vitals (or a blank form when the visit has none yet).
watch(
  [vitalData, isLoading],
  () => {
    if (isLoading.value)
      return
    const values = blank()
    const data = vitalData.value
    if (data) {
      for (const key of NUMBER_KEYS)
        values[key] = data[key] == null ? '' : String(data[key])
      values.diabetesMellitus = data.diabetesMellitus === true ? 'true' : data.diabetesMellitus === false ? 'false' : ''
      values.urineTest = data.urineTest == null ? '' : String(data.urineTest)
      values.others = data.others == null ? '' : String(data.others)
    }
    baseline.value = JSON.stringify(values)
    form.value = draft.resume(values)
  },
  { immediate: true },
)

const patientAge = computed(() => {
  const atDate = new Date(props.visitDate)
  const dob = new Date(props.patient.dateOfBirth)
  return Math.floor((atDate.getTime() - dob.getTime()) / (365.25 * 24 * 60 * 60 * 1000))
})
const showChart = computed(() => patientAge.value >= 2 && patientAge.value <= 18)
const chartHeight = computed(() => (form.value.height ? Number.parseFloat(form.value.height) : null))
const chartWeight = computed(() => (form.value.weight ? Number.parseFloat(form.value.weight) : null))

const bloodPressureStatus = computed(() =>
  form.value.systolic !== '' && form.value.diastolic !== ''
    ? classifyBloodPressure(Number(form.value.systolic), Number(form.value.diastolic))
    : null,
)
const bmi = computed(() => computeBmi(form.value.height || null, form.value.weight || null))

const formEl = ref<HTMLFormElement>()
// Times this form for the station wait estimate.
const serviceTimer = useServiceTimer('vitals', formEl, () => form.value)

const saving = ref(false)

async function submit() {
  if (saving.value || isLoading.value)
    return
  if (!isDirty.value) {
    toast('No form field changed!')
    return
  }
  const data = form.value
  const num = (v: string) => (v ? Number(v) : null)
  const diabetes = data.diabetesMellitus === 'true' ? 1 : data.diabetesMellitus === 'false' ? 0 : null
  const urineTest = data.urineTest || null
  const others = data.others || null
  const now = new Date().toISOString()

  saving.value = true
  try {
    const db = getPowerSyncInstance()
    const existingId = vitalData.value?.id ? String(vitalData.value.id) : null
    if (!existingId) {
      await db.execute(
        `INSERT INTO vitals (
          id, visit_id, recorded_at, height, weight, temperature, systolic, diastolic,
          heart_rate, hemocue_count, diabetes_mellitus, urine_test,
          blood_glucose_non_fasting, blood_glucose_fasting, hba1c, others, version, updated_at
        ) VALUES (?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, 1, ?)`,
        [
          uuidv7(),
          props.visitId,
          now,
          num(data.height),
          num(data.weight),
          num(data.temperature),
          num(data.systolic),
          num(data.diastolic),
          num(data.heartRate),
          num(data.hemocueCount),
          diabetes,
          urineTest,
          num(data.bloodGlucoseNonFasting),
          num(data.bloodGlucoseFasting),
          num(data.hba1c),
          others,
          now,
        ],
      )
    }
    else {
      await db.execute(
        `UPDATE vitals
         SET height = ?, weight = ?, temperature = ?, systolic = ?, diastolic = ?,
             heart_rate = ?, hemocue_count = ?, diabetes_mellitus = ?, urine_test = ?,
             blood_glucose_non_fasting = ?, blood_glucose_fasting = ?, hba1c = ?, others = ?,
             updated_at = ?
         WHERE id = ?`,
        [
          num(data.height),
          num(data.weight),
          num(data.temperature),
          num(data.systolic),
          num(data.diastolic),
          num(data.heartRate),
          num(data.hemocueCount),
          diabetes,
          urineTest,
          num(data.bloodGlucoseNonFasting),
          num(data.bloodGlucoseFasting),
          num(data.hba1c),
          others,
          now,
          existingId,
        ],
      )
    }
    await refreshPendingCrudCount()
    serviceTimer.saved()
    baseline.value = JSON.stringify(form.value)
    draft.clear(form.value)
    toast.success('Vitals saved!')
  }
  catch (error) {
    console.error('Error saving vitals:', error)
    toast.error('Failed to save vitals.')
  }
  finally {
    saving.value = false
  }
}

useSaveShortcut(submit)

const diabetesLabelId = useId()
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading Vitals" />
  <form
    v-else
    ref="formEl"
    novalidate
    class="grid grid-cols-1 gap-3"
    :class="showChart && 'min-[1128px]:grid-cols-[minmax(0,1.25fr)_minmax(0,1fr)]'"
    aria-label="Vitals"
    @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField"
  >
    <DraftNotice
      :restored-at="draft.restoredAt.value"
      :class="showChart && 'min-[1128px]:col-start-1'"
      @discard="form = draft.discard()"
    />
    <AppPanel title="Body Measurements" description="General physical measurements.">
      <div class="grid grid-cols-2 items-end gap-3 lg:grid-cols-4">
        <FormField v-slot="{ id }" label="Height (cm)">
          <NumericInput :id="id" v-model="form.height" unit="cm" step="0.1" />
        </FormField>
        <FormField v-slot="{ id }" label="Weight (kg)">
          <NumericInput :id="id" v-model="form.weight" unit="kg" step="0.01" />
        </FormField>
        <FormField v-slot="{ id }" label="Body Temperature (°C)">
          <NumericInput :id="id" v-model="form.temperature" unit="°C" step="0.1" />
        </FormField>
        <div class="flex flex-col gap-1">
          <span class="text-xs font-semibold touch:text-sm">BMI</span>
          <output class="flex h-8 items-center touch:h-11 touch:text-base border border-dashed border-border bg-muted px-2.5 text-xs tabular-nums" aria-live="polite">
            {{ bmi }}
          </output>
        </div>
      </div>
    </AppPanel>

    <AppPanel title="Cardiovascular" description="Blood pressure and heart rate readings.">
      <div class="grid grid-cols-2 items-end gap-3 md:grid-cols-3">
        <FormField v-slot="{ id }" label="Systolic Blood Pressure (mmHg)">
          <NumericInput :id="id" v-model="form.systolic" unit="mmHg" />
        </FormField>
        <FormField v-slot="{ id }" label="Diastolic Blood Pressure (mmHg)">
          <NumericInput :id="id" v-model="form.diastolic" unit="mmHg" />
        </FormField>
        <FormField v-slot="{ id }" label="Heart Rate (BPM)">
          <NumericInput :id="id" v-model="form.heartRate" unit="BPM" />
        </FormField>
      </div>
      <div v-if="bloodPressureStatus" class="mt-3 flex flex-wrap items-center gap-2" role="status">
        <span
          class="inline-flex items-center gap-1 border px-2 py-0.5 text-xs font-bold touch:text-sm"
          :class="bloodPressureStatus === 'Pre-HTN'
            ? 'border-warning-border bg-warning-subtle text-warning'
            : 'border-destructive/40 bg-destructive-subtle text-destructive'"
        >
          <PhWarning class="size-4" aria-hidden="true" />
          {{ bloodPressureStatus }}
        </span>
        <span class="text-xs text-muted-foreground touch:text-sm">{{ describeBloodPressureThreshold(bloodPressureStatus) }}</span>
      </div>
    </AppPanel>

    <AppPanel title="Blood & Metabolic" description="Blood glucose, haemoglobin and diabetes status.">
      <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
        <FormField v-slot="{ id }" label="Fasting Blood Glucose (mmol/L)">
          <NumericInput :id="id" v-model="form.bloodGlucoseFasting" unit="mmol/L" step="0.01" />
        </FormField>
        <FormField v-slot="{ id }" label="Non-Fasting Blood Glucose (mmol/L)">
          <NumericInput :id="id" v-model="form.bloodGlucoseNonFasting" unit="mmol/L" step="0.01" />
        </FormField>
        <FormField v-slot="{ id }" label="HbA1c Level (%)">
          <NumericInput :id="id" v-model="form.hba1c" unit="%" step="0.01" />
        </FormField>
        <FormField v-slot="{ id }" label="Hemocue Hemoglobin Count (g/dL)">
          <NumericInput :id="id" v-model="form.hemocueCount" unit="g/dL" step="0.01" />
        </FormField>
      </div>
      <div class="mt-3 flex flex-col gap-1">
        <span :id="diabetesLabelId" class="text-xs font-semibold touch:text-sm">Diabetes Mellitus History Status Flag</span>
        <ChoiceGroup
          v-model="form.diabetesMellitus"
          :labelledby="diabetesLabelId"
          size="default"
          :options="[{ label: 'Positive', value: 'true' }, { label: 'Negative', value: 'false' }]"
        />
      </div>
    </AppPanel>

    <AppPanel title="Urinalysis" description="Urine test findings." collapsible :default-open="!!form.urineTest">
      <FormField v-slot="{ id }" label="Urinalysis Diagnostics (Leukocytes, Nitrites, Protein notes)">
        <Textarea :id="id" v-model="form.urineTest" :rows="3" />
      </FormField>
    </AppPanel>

    <AppPanel title="Additional Remarks" description="Any additional clinical notes." collapsible :default-open="!!form.others">
      <FormField v-slot="{ id }" label="Additional Clinical Remarks">
        <Textarea :id="id" v-model="form.others" :rows="3" />
      </FormField>
    </AppPanel>

    <!-- Growth chart: after the fields on narrow screens (so opening it never pushes a field away), pinned in a side column on wide screens. -->
    <div
      v-if="showChart"
      class="self-start min-[1128px]:sticky min-[1128px]:top-16 min-[1128px]:col-start-2 min-[1128px]:row-span-6 min-[1128px]:row-start-1"
    >
      <AppPanel
        title="Growth Charts"
        description="Height (blue) and weight (red) on the NCHS chart for age and gender."
        collapsible
        collapse-key="growth-chart"
      >
        <HeightWeightChart :age="patientAge" :height="chartHeight" :weight="chartWeight" :gender="props.patient.gender === 'female' ? 'female' : 'male'" />
      </AppPanel>
    </div>

    <SaveBar :class="showChart && 'min-[1128px]:col-start-1'">
      <span v-if="isDirty" class="text-xs font-semibold text-warning touch:text-sm">Unsaved changes</span>
      <Button type="submit" variant="success" :disabled="saving">
        <PhFloppyDisk aria-hidden="true" />
        {{ saving ? 'Saving…' : 'Save Records' }}
      </Button>
    </SaveBar>
  </form>
</template>
