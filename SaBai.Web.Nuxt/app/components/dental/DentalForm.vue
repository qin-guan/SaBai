<script setup lang="ts">
import { PhArrowCounterClockwise, PhFloppyDisk, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'

/**
 * The dental station's consultation for one visit. Loads any saved
 * consultation and saves the whole form back to local SQLite.
 */
const props = defineProps<{ visitId: string }>()

const SECTIONS = [
  { id: 'dental-history', title: 'Dental History', tab: 'Dental History' },
  { id: 'chief-complaint', title: 'Chief Complaint', tab: 'Complaint' },
  { id: 'clinical-examination', title: 'Clinical Examination', tab: 'Clinical Examination' },
  { id: 'dental-diagnosis', title: 'Dental Diagnosis', tab: 'Diagnosis' },
  { id: 'treatment-provided', title: 'Treatment Provided', tab: 'Treatment Provided' },
  { id: 'referral', title: 'Referral', tab: 'Referral' },
  { id: 'remarks', title: 'Remarks', tab: 'Remarks' },
] as const

const { data, isLoading } = useVisitDental(() => props.visitId)
const form = ref<DentalFormValues>(toDentalFormValues(null))
const draft = useFormDraft(() => `dental:${props.visitId}`, form)
const baseline = ref('')
const isDirty = computed(() => JSON.stringify(form.value) !== baseline.value)
const formEl = ref<HTMLFormElement>()
// Times this form for the station wait estimate.
const serviceTimer = useServiceTimer('dental', formEl, () => form.value)

function load(values: DentalFormValues) {
  form.value = values
  baseline.value = JSON.stringify(values)
}

watch([data, isLoading], () => {
  if (isLoading.value)
    return
  const values = toDentalFormValues(data.value)
  load(values)
  form.value = draft.resume(values)
}, { immediate: true })

function addDiagnosis() {
  form.value.diagnoses.push({ value: '' })
  nextTick(() => {
    const inputs = formEl.value?.querySelectorAll<HTMLInputElement>('[data-dental-diagnosis]')
    inputs?.[inputs.length - 1]?.focus()
  })
}

const saving = ref(false)
const toSqliteBool = (v: boolean | null | undefined) => (v === true ? 1 : v === false ? 0 : null)

async function submit() {
  if (saving.value || isLoading.value)
    return

  const values = { ...form.value }
  const body = toDentalRequest(values)
  const now = new Date().toISOString()
  const diagnosesJson = JSON.stringify(body.diagnoses ?? [])

  saving.value = true
  try {
    const db = getPowerSyncInstance()
    const existingId = data.value?.id ? String(data.value.id) : null
    const params = [
      body.previousDentalVisit ?? null,
      body.previousDentalTreatment ?? null,
      toSqliteBool(body.toothache),
      toSqliteBool(body.extraction),
      toSqliteBool(body.dentalTrauma),
      toSqliteBool(body.currentToothache),
      toSqliteBool(body.toothSensitivity),
      toSqliteBool(body.bleedingGums),
      body.brushingFrequency ?? null,
      body.fluorideToothpaste ?? null,
      body.sugarConsumption ?? null,
      body.otherDentalHistory ?? null,
      body.chiefComplaint ?? null,
      body.facialSymmetry ?? null,
      body.facialSwelling ?? null,
      body.lymphadenopathy ?? null,
      body.mouthOpening ?? null,
      body.oralHygiene ?? null,
      body.plaque ?? null,
      body.calculus ?? null,
      body.gingivalCondition ?? null,
      body.caries ?? null,
      body.missingTeeth ?? null,
      body.fracturedTeeth ?? null,
      body.mucosalLesions ?? null,
      body.malocclusion ?? null,
      diagnosesJson,
      toSqliteBool(body.fluorideDone),
      body.fluorideDate ?? null,
      body.fluorideOperator ?? null,
      body.fluorideToothNumbers ?? null,
      toSqliteBool(body.sealantDone),
      body.sealantDate ?? null,
      body.sealantOperator ?? null,
      body.sealantToothNumbers ?? null,
      toSqliteBool(body.sdfDone),
      body.sdfDate ?? null,
      body.sdfOperator ?? null,
      body.sdfToothNumbers ?? null,
      toSqliteBool(body.scalingDone),
      body.scalingDate ?? null,
      body.scalingOperator ?? null,
      body.scalingToothNumbers ?? null,
      body.referredFor ?? null,
      body.referralPriority ?? null,
      body.referralNotes ?? null,
      body.recommendedFacility ?? null,
      body.remarks ?? null,
      now,
    ]

    if (!existingId) {
      await db.execute(
        `INSERT INTO dental_consults (
          id, visit_id, recorded_at,
          previous_dental_visit, previous_dental_treatment,
          toothache, extraction, dental_trauma, current_toothache, tooth_sensitivity, bleeding_gums,
          brushing_frequency, fluoride_toothpaste, sugar_consumption, other_dental_history,
          chief_complaint, facial_symmetry, facial_swelling, lymphadenopathy, mouth_opening,
          oral_hygiene, plaque, calculus, gingival_condition, caries,
          missing_teeth, fractured_teeth, mucosal_lesions, malocclusion, diagnoses,
          fluoride_done, fluoride_date, fluoride_operator, fluoride_tooth_numbers,
          sealant_done, sealant_date, sealant_operator, sealant_tooth_numbers,
          sdf_done, sdf_date, sdf_operator, sdf_tooth_numbers,
          scaling_done, scaling_date, scaling_operator, scaling_tooth_numbers,
          referred_for, referral_priority, referral_notes, recommended_facility, remarks,
          updated_at, version
        ) VALUES (
          ?, ?, ?,
          ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
          ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
          ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?, ?,
          ?, ?, ?, ?, ?, ?, 1
        )`,
        [uuidv7(), props.visitId, now, ...params],
      )
    }
    else {
      await db.execute(
        `UPDATE dental_consults SET
          previous_dental_visit = ?, previous_dental_treatment = ?,
          toothache = ?, extraction = ?, dental_trauma = ?, current_toothache = ?, tooth_sensitivity = ?, bleeding_gums = ?,
          brushing_frequency = ?, fluoride_toothpaste = ?, sugar_consumption = ?, other_dental_history = ?,
          chief_complaint = ?, facial_symmetry = ?, facial_swelling = ?, lymphadenopathy = ?, mouth_opening = ?,
          oral_hygiene = ?, plaque = ?, calculus = ?, gingival_condition = ?, caries = ?,
          missing_teeth = ?, fractured_teeth = ?, mucosal_lesions = ?, malocclusion = ?, diagnoses = ?,
          fluoride_done = ?, fluoride_date = ?, fluoride_operator = ?, fluoride_tooth_numbers = ?,
          sealant_done = ?, sealant_date = ?, sealant_operator = ?, sealant_tooth_numbers = ?,
          sdf_done = ?, sdf_date = ?, sdf_operator = ?, sdf_tooth_numbers = ?,
          scaling_done = ?, scaling_date = ?, scaling_operator = ?, scaling_tooth_numbers = ?,
          referred_for = ?, referral_priority = ?, referral_notes = ?, recommended_facility = ?, remarks = ?,
          updated_at = ?
        WHERE id = ?`,
        [...params, existingId],
      )
    }
    await refreshPendingCrudCount()
    serviceTimer.saved()
    load(values)
    draft.clear(values)
    toast.success('Dental consultation saved.')
  }
  catch (error) {
    console.error('Error saving dental consultation:', error)
    toast.error('Failed to save dental consultation.')
  }
  finally {
    saving.value = false
  }
}

useSaveShortcut(submit)

const ids = Object.fromEntries(
  [...HISTORY_YES_NO_UNKNOWN, ...HISTORY_YES_NO].map(q => [q.name, useId()]),
) as Record<string, string>
const extraIds = { brushingFrequency: useId(), sugarConsumption: useId(), referredFor: useId(), referralPriority: useId() }
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading dental consultation" />
  <form v-else ref="formEl" novalidate aria-label="Dental consultation" @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
    <!-- Section jump bar: sticks under the app bar; scrolls sideways on phones. -->
    <nav
      aria-label="Dental consultation sections"
      class="sticky z-20 mb-3 flex gap-px overflow-x-auto border border-border bg-band"
      :style="{ top: 'var(--app-header-h)' }"
    >
      <a
        v-for="(section, index) in SECTIONS"
        :key="section.id"
        :href="`#${section.id}`"
        class="flex shrink-0 items-center gap-1.5 px-3 py-2 text-xs font-semibold whitespace-nowrap text-chrome hover:bg-accent focus-visible:bg-accent touch:py-3 touch:text-sm"
      >
        <span class="text-muted-foreground" aria-hidden="true">{{ index + 1 }}</span>
        {{ section.tab }}
      </a>
    </nav>

    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <div class="flex flex-col gap-3">
      <AppPanel :id="SECTIONS[0].id" :step="1" :title="SECTIONS[0].title">
        <div class="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
          <div v-for="q in HISTORY_YES_NO_UNKNOWN.slice(0, 2)" :key="q.name" class="flex flex-col gap-1">
            <span :id="ids[q.name]" class="text-xs font-semibold touch:text-sm">{{ q.label }}</span>
            <ChoiceGroup v-model="form[q.name]" :labelledby="ids[q.name]" :options="YES_NO_UNKNOWN" />
          </div>
          <div v-for="q in HISTORY_YES_NO" :key="q.name" class="flex flex-col gap-1">
            <span :id="ids[q.name]" class="text-xs font-semibold touch:text-sm">{{ q.label }}</span>
            <ChoiceGroup v-model="form[q.name]" :labelledby="ids[q.name]" :options="YES_NO" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.brushingFrequency" class="text-xs font-semibold touch:text-sm">Brushing frequency</span>
            <ChoiceGroup v-model="form.brushingFrequency" :labelledby="extraIds.brushingFrequency" :options="BRUSHING_FREQUENCY" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="ids.fluorideToothpaste" class="text-xs font-semibold touch:text-sm">Fluoride toothpaste</span>
            <ChoiceGroup v-model="form.fluorideToothpaste" :labelledby="ids.fluorideToothpaste" :options="YES_NO_UNKNOWN" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.sugarConsumption" class="text-xs font-semibold touch:text-sm">Sugar consumption</span>
            <ChoiceGroup v-model="form.sugarConsumption" :labelledby="extraIds.sugarConsumption" :options="SUGAR_CONSUMPTION" />
          </div>
        </div>
        <div class="mt-3">
          <FormField v-slot="{ id }" label="Other dental history (optional)">
            <Input :id="id" v-model="form.otherDentalHistory" placeholder="e.g. braces, previous filling..." />
          </FormField>
        </div>
      </AppPanel>

      <AppPanel :id="SECTIONS[1].id" :step="2" :title="SECTIONS[1].title">
        <FormField v-slot="{ id }" label="Chief complaint" hide-label>
          <Input :id="id" v-model="form.chiefComplaint" placeholder="What brings the patient to the dental station?" />
        </FormField>
      </AppPanel>

      <AppPanel :id="SECTIONS[2].id" :step="3" :title="SECTIONS[2].title">
        <h3 class="mb-2 text-xs font-semibold text-muted-foreground touch:text-sm">
          A. Extraoral examination
        </h3>
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-4">
          <FormField v-for="q in EXTRAORAL_EXAM" :key="q.name" v-slot="{ id }" :label="q.label">
            <NativeSelect :id="id" v-model="form[q.name]">
              <NativeSelectOption value="">
                Select...
              </NativeSelectOption>
              <NativeSelectOption v-for="o in q.options" :key="o.value" :value="o.value">
                {{ o.label }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>
        </div>
        <h3 class="mt-4 mb-2 text-xs font-semibold text-muted-foreground touch:text-sm">
          B. Intraoral examination
        </h3>
        <div class="grid grid-cols-1 gap-3 sm:grid-cols-2 lg:grid-cols-3">
          <FormField v-for="q in INTRAORAL_EXAM" :key="q.name" v-slot="{ id }" :label="q.label">
            <NativeSelect :id="id" v-model="form[q.name]">
              <NativeSelectOption value="">
                Select...
              </NativeSelectOption>
              <NativeSelectOption v-for="o in q.options" :key="o.value" :value="o.value">
                {{ o.label }}
              </NativeSelectOption>
            </NativeSelect>
          </FormField>
        </div>
      </AppPanel>

      <AppPanel :id="SECTIONS[3].id" :step="4" :title="SECTIONS[3].title">
        <div class="flex flex-col gap-2">
          <div v-for="(diagnosis, index) in form.diagnoses" :key="index" class="flex gap-2">
            <Input
              v-model="diagnosis.value"
              data-dental-diagnosis
              :aria-label="`Diagnosis ${index + 1}`"
              @keydown.enter.prevent.stop="index === form.diagnoses.length - 1 && diagnosis.value.trim() ? addDiagnosis() : enterMovesToNextField($event)"
              placeholder="e.g. Dental caries"
            />
            <Button variant="outline" :aria-label="`Remove diagnosis ${index + 1}`" @click="form.diagnoses.splice(index, 1)">
              <PhTrash aria-hidden="true" /> <span class="hidden sm:inline">Remove</span>
            </Button>
          </div>
          <Button variant="outline" class="self-start" @click="addDiagnosis">
            <PhPlus aria-hidden="true" /> Add Diagnosis
          </Button>
        </div>
      </AppPanel>

      <AppPanel :id="SECTIONS[4].id" :step="5" :title="SECTIONS[4].title" flush>
        <div class="hidden grid-cols-[6rem_11rem_1fr_1fr_1fr] gap-2 border-b border-border bg-stripe px-3 py-1 text-xs touch:text-xs font-semibold text-muted-foreground md:grid" aria-hidden="true">
          <span>Treatment</span><span>Status</span><span>Date</span><span>Operator</span><span>Tooth no.</span>
        </div>
        <div
          v-for="{ key, label } in TREATMENTS"
          :key="key"
          role="group"
          :aria-label="label"
          class="grid grid-cols-1 items-center gap-2 border-b border-border px-3 py-2 last:border-b-0 md:grid-cols-[6rem_11rem_1fr_1fr_1fr]"
        >
          <span class="text-sm font-bold">{{ label }}</span>
          <ChoiceGroup v-model="form[`${key}Done`]" :label="`${label} status`" :options="TREATMENT_STATUS" />
          <Input v-model="form[`${key}Date`]" type="date" :aria-label="`${label} date`" />
          <Input v-model="form[`${key}Operator`]" :placeholder="`${label} operator`" :aria-label="`${label} operator`" />
          <Input v-model="form[`${key}ToothNumbers`]" :placeholder="`${label} tooth no.`" :aria-label="`${label} tooth no.`" />
        </div>
      </AppPanel>

      <AppPanel :id="SECTIONS[5].id" :step="6" :title="SECTIONS[5].title">
        <div class="grid grid-cols-1 gap-3 md:grid-cols-2">
          <div class="flex flex-col gap-1">
            <span :id="extraIds.referredFor" class="text-xs font-semibold touch:text-sm">Referred for</span>
            <ChoiceGroup v-model="form.referredFor" :labelledby="extraIds.referredFor" :options="REFERRED_FOR" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.referralPriority" class="text-xs font-semibold touch:text-sm">Priority</span>
            <ChoiceGroup v-model="form.referralPriority" :labelledby="extraIds.referralPriority" :options="REFERRAL_PRIORITY" />
          </div>
          <FormField v-slot="{ id }" label="Referral notes">
            <Input :id="id" v-model="form.referralNotes" placeholder="e.g. Deep caries, needs restorative treatment" />
          </FormField>
          <FormField v-slot="{ id }" label="Recommended facility">
            <Input :id="id" v-model="form.recommendedFacility" placeholder="e.g. Provincial Dental Clinic" />
          </FormField>
        </div>
      </AppPanel>

      <AppPanel :id="SECTIONS[6].id" :step="7" :title="SECTIONS[6].title">
        <FormField v-slot="{ id }" label="Remarks" hide-label>
          <Textarea :id="id" v-model="form.remarks" :rows="3" placeholder="Additional notes..." />
        </FormField>
      </AppPanel>
    </div>

    <SaveBar>
      <span v-if="isDirty" class="text-xs font-semibold text-warning touch:text-sm">Unsaved changes</span>
      <Button variant="outline" :disabled="!isDirty" @click="form = draft.discard()">
        <PhArrowCounterClockwise aria-hidden="true" /> Reset<span class="hidden sm:inline">&nbsp;Form</span>
      </Button>
      <Button type="submit" variant="success" :disabled="saving">
        <PhFloppyDisk aria-hidden="true" />
        <template v-if="saving">Saving…</template>
        <template v-else>
          Save<span class="hidden sm:inline">&nbsp;Dental Consultation</span>
        </template>
      </Button>
    </SaveBar>
  </form>
</template>
