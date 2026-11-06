<script setup lang="ts">
import { PhArrowCounterClockwise, PhFloppyDisk, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsDentalUpsertByVisitPutEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'

/**
 * The dental station's consultation for one visit. Loads any saved
 * consultation and saves the whole form back with a single PUT.
 */
const props = defineProps<{ visitId: number }>()

const SECTIONS = [
  { id: 'dental-history', title: 'Dental History', tab: 'Dental History' },
  { id: 'chief-complaint', title: 'Chief Complaint', tab: 'Complaint' },
  { id: 'clinical-examination', title: 'Clinical Examination', tab: 'Clinical Examination' },
  { id: 'dental-diagnosis', title: 'Dental Diagnosis', tab: 'Diagnosis' },
  { id: 'treatment-provided', title: 'Treatment Provided', tab: 'Treatment Provided' },
  { id: 'referral', title: 'Referral', tab: 'Referral' },
  { id: 'remarks', title: 'Remarks', tab: 'Remarks' },
] as const

// A visit without a dental consultation answers 404; that is just an empty form.
const { data, isLoading } = useVisitDental(() => props.visitId)
const form = ref<DentalFormValues>(toDentalFormValues(null))
const baseline = ref('')
const isDirty = computed(() => JSON.stringify(form.value) !== baseline.value)
const formEl = ref<HTMLFormElement>()

function load(values: DentalFormValues) {
  form.value = values
  baseline.value = JSON.stringify(values)
}

watch([data, isLoading], () => {
  if (!isLoading.value)
    load(toDentalFormValues(data.value))
}, { immediate: true })

function addDiagnosis() {
  form.value.diagnoses.push({ value: '' })
  nextTick(() => {
    const inputs = formEl.value?.querySelectorAll<HTMLInputElement>('[data-dental-diagnosis]')
    inputs?.[inputs.length - 1]?.focus()
  })
}

const queryClient = useQueryClient()
const save = useMutation({
  mutationFn: (values: DentalFormValues) =>
    saBaiAPIEndpointsDentalUpsertByVisitPutEndpoint({
      ...apiConfig,
      path: { visitId: props.visitId },
      body: toDentalRequest(values),
      throwOnError: true,
    }).unwrap(),
  onSuccess: (saved) => {
    queryClient.setQueryData(queryKeys.dental(props.visitId), saved)
    load(toDentalFormValues(saved as DentalResponse))
    toast.success('Dental consultation saved.')
  },
  onError: error => toast.error(getApiErrorMessage(error, 'Failed to save dental consultation.')),
})

function submit() {
  if (!save.isPending.value && !isLoading.value)
    save.mutate(form.value)
}

useSaveShortcut(submit)

const ids = Object.fromEntries(
  [...HISTORY_YES_NO_UNKNOWN, ...HISTORY_YES_NO].map(q => [q.name, useId()]),
) as Record<string, string>
const extraIds = { brushingFrequency: useId(), sugarConsumption: useId(), referredFor: useId(), referralPriority: useId() }
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading dental consultation" />
  <form v-else ref="formEl" novalidate aria-label="Dental consultation" @submit.prevent="submit">
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
        class="flex shrink-0 items-center gap-1.5 px-3 py-2 text-xs font-semibold whitespace-nowrap text-chrome hover:bg-accent focus-visible:bg-accent [@media(pointer:coarse)]:py-3"
      >
        <span class="text-muted-foreground" aria-hidden="true">{{ index + 1 }}</span>
        {{ section.tab }}
      </a>
    </nav>

    <div class="flex flex-col gap-3">
      <AppPanel :id="SECTIONS[0].id" :step="1" :title="SECTIONS[0].title">
        <div class="grid grid-cols-1 gap-x-4 gap-y-3 sm:grid-cols-2 lg:grid-cols-3">
          <div v-for="q in HISTORY_YES_NO_UNKNOWN.slice(0, 2)" :key="q.name" class="flex flex-col gap-1">
            <span :id="ids[q.name]" class="text-xs font-semibold">{{ q.label }}</span>
            <ChoiceGroup v-model="form[q.name]" :labelledby="ids[q.name]" :options="YES_NO_UNKNOWN" />
          </div>
          <div v-for="q in HISTORY_YES_NO" :key="q.name" class="flex flex-col gap-1">
            <span :id="ids[q.name]" class="text-xs font-semibold">{{ q.label }}</span>
            <ChoiceGroup v-model="form[q.name]" :labelledby="ids[q.name]" :options="YES_NO" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.brushingFrequency" class="text-xs font-semibold">Brushing frequency</span>
            <ChoiceGroup v-model="form.brushingFrequency" :labelledby="extraIds.brushingFrequency" :options="BRUSHING_FREQUENCY" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="ids.fluorideToothpaste" class="text-xs font-semibold">Fluoride toothpaste</span>
            <ChoiceGroup v-model="form.fluorideToothpaste" :labelledby="ids.fluorideToothpaste" :options="YES_NO_UNKNOWN" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.sugarConsumption" class="text-xs font-semibold">Sugar consumption</span>
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
        <h3 class="mb-2 text-xs font-bold uppercase tracking-wide text-muted-foreground">
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
        <h3 class="mt-4 mb-2 text-xs font-bold uppercase tracking-wide text-muted-foreground">
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
        <div class="hidden grid-cols-[6rem_11rem_1fr_1fr_1fr] gap-2 border-b border-border bg-stripe px-3 py-1 text-[0.6875rem] font-bold uppercase tracking-wide text-muted-foreground md:grid" aria-hidden="true">
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
            <span :id="extraIds.referredFor" class="text-xs font-semibold">Referred for</span>
            <ChoiceGroup v-model="form.referredFor" :labelledby="extraIds.referredFor" :options="REFERRED_FOR" />
          </div>
          <div class="flex flex-col gap-1">
            <span :id="extraIds.referralPriority" class="text-xs font-semibold">Priority</span>
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
      <span v-if="isDirty" class="text-xs font-semibold text-warning">Unsaved changes</span>
      <Button variant="outline" :disabled="!isDirty" @click="load(toDentalFormValues(data))">
        <PhArrowCounterClockwise aria-hidden="true" /> Reset<span class="hidden sm:inline">&nbsp;Form</span>
      </Button>
      <Button type="submit" variant="success" :disabled="save.isPending.value">
        <PhFloppyDisk aria-hidden="true" />
        <template v-if="save.isPending.value">Saving…</template>
        <template v-else>
          Save<span class="hidden sm:inline">&nbsp;Dental Consultation</span>
        </template>
      </Button>
    </SaveBar>
  </form>
</template>
