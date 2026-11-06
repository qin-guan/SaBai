<script setup lang="ts">
import { PhFloppyDisk, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { z } from 'zod'
import { saBaiAPIEndpointsConsultsCreatePostEndpoint } from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsConsultsCreatePostRequest } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'

/** The doctor's consultation form for one visit (each save appends a consult). */
const props = defineProps<{ visitId: number }>()

const consultFormSchema = z.object({
  pastMedicalHistory: z.string().trim().optional(),
  consultation: z.string().trim().optional(),
  treatmentPlan: z.string().trim().optional(),
  remarks: z.string().trim().optional(),
  diagnoses: z.array(
    z.object({
      details: z.string().trim().min(1, 'Diagnosis details are required'),
      category: z.string().min(1, 'Please select a category'),
    }),
  ),
})
type ConsultFormValues = z.infer<typeof consultFormSchema>

let nextKey = 1
// Stable key for the first diagnosis so a blank form always compares equal.
const blank = () => ({
  pastMedicalHistory: '',
  consultation: '',
  treatmentPlan: '',
  remarks: '',
  diagnoses: [{ key: 0, details: '', category: '' }],
})

const form = ref(blank())
// Each save appends a consult, so the draft is the consult being written now.
const draft = useFormDraft(() => `consult:${props.visitId}`, form)
form.value = draft.resume(blank())
nextKey = Math.max(0, ...form.value.diagnoses.map(d => d.key)) + 1
const errors = ref<Record<string, string>>({})
const formEl = ref<HTMLFormElement>()
// Times this form for the queue's wait estimates.
const serviceTimer = useServiceTimer('consults', formEl, () => form.value)

function addDiagnosis() {
  form.value.diagnoses.push({ key: nextKey++, details: '', category: '' })
  nextTick(() => {
    const selects = formEl.value?.querySelectorAll<HTMLSelectElement>('[data-diagnosis-category]')
    selects?.[selects.length - 1]?.focus()
  })
}

function removeDiagnosis(index: number) {
  form.value.diagnoses.splice(index, 1)
  errors.value = {}
}

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: (body: SaBaiAPIEndpointsConsultsCreatePostRequest) =>
    saBaiAPIEndpointsConsultsCreatePostEndpoint({ ...apiConfig, body, throwOnError: true }).unwrap(),
})

function submit() {
  if (create.isPending.value)
    return
  const parsed = consultFormSchema.safeParse(form.value)
  if (!parsed.success) {
    errors.value = Object.fromEntries(parsed.error.issues.map(i => [i.path.join('.'), i.message]))
    toast.error('Please fill in all required fields before saving.')
    focusFirstInvalid(formEl.value)
    return
  }
  errors.value = {}
  const data: ConsultFormValues = parsed.data
  create.mutate(
    {
      visitId: props.visitId,
      pastMedicalHistory: data.pastMedicalHistory || undefined,
      consultation: data.consultation || undefined,
      treatmentPlan: data.treatmentPlan || undefined,
      remarks: data.remarks || undefined,
      diagnoses: data.diagnoses.map(d => ({ details: d.details, category: d.category as DiagnosisCategory })),
    },
    {
      onSuccess: () => {
        // Before the form is cleared for the next consult.
        serviceTimer.saved()
        queryClient.invalidateQueries({ queryKey: queryKeys.consults(props.visitId) })
        // Refresh the journey bars and station queues.
        queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
        toast.success('Consult has been saved successfully!')
        form.value = blank()
        draft.clear(form.value)
      },
      onError: error => toast.error(getApiErrorMessage(error, 'Failed to save consult.')),
    },
  )
}

useSaveShortcut(submit)
</script>

<template>
  <form ref="formEl" novalidate aria-label="Doctor's consult form" class="min-w-0" @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <AppPanel title="Doctor's Consult Form">
      <div class="flex flex-col gap-3">
        <FormField v-slot="{ id }" label="Past Medical History">
          <Textarea :id="id" v-model="form.pastMedicalHistory" :rows="3" placeholder="Type the patient's past medical history here..." />
        </FormField>
        <FormField v-slot="{ id }" label="Consultation">
          <Textarea :id="id" v-model="form.consultation" :rows="4" placeholder="Type your consultation here..." />
        </FormField>

        <fieldset class="flex flex-col gap-2">
          <legend class="mb-1 text-xs font-semibold touch:text-sm">
            Diagnoses <span class="text-destructive" aria-hidden="true">*</span><span class="sr-only">(at least one, required)</span>
          </legend>
          <div
            v-for="(diagnosis, index) in form.diagnoses"
            :key="diagnosis.key"
            class="flex flex-col gap-2 border border-border bg-stripe p-2"
          >
            <div class="flex items-center justify-between">
              <p class="text-xs font-bold text-muted-foreground touch:text-sm">
                Diagnosis {{ index + 1 }}
              </p>
              <Button
                v-if="form.diagnoses.length > 1"
                variant="ghost"
                size="sm"
                class="text-destructive"
                :aria-label="`Remove diagnosis ${index + 1}`"
                @click="removeDiagnosis(index)"
              >
                <PhTrash aria-hidden="true" /> Remove
              </Button>
            </div>
            <div class="grid grid-cols-1 gap-2 sm:grid-cols-[14rem_1fr]">
              <FormField v-slot="{ id, describedby, invalid }" label="Category" required :error="errors[`diagnoses.${index}.category`]">
                <NativeSelect
                  :id="id"
                  v-model="diagnosis.category"
                  data-diagnosis-category
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                >
                  <NativeSelectOption value="">
                    Choose a category
                  </NativeSelectOption>
                  <NativeSelectOption v-for="option in DIAGNOSIS_CATEGORY_OPTIONS" :key="option.value" :value="option.value">
                    {{ option.label }}
                  </NativeSelectOption>
                </NativeSelect>
              </FormField>
              <FormField v-slot="{ id, describedby, invalid }" label="Details" required :error="errors[`diagnoses.${index}.details`]">
                <Textarea
                  :id="id"
                  v-model="diagnosis.details"
                  :rows="2"
                  placeholder="Describe the diagnosis..."
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                />
              </FormField>
            </div>
          </div>
          <Button variant="outline" class="self-start" @click="addDiagnosis">
            <PhPlus aria-hidden="true" /> Add Diagnosis
          </Button>
        </fieldset>

        <FormField v-slot="{ id }" label="Plan">
          <Textarea :id="id" v-model="form.treatmentPlan" :rows="3" placeholder="Type your plan here..." />
        </FormField>
        <FormField v-slot="{ id }" label="Remarks">
          <Textarea :id="id" v-model="form.remarks" :rows="2" placeholder="Type your remarks here..." />
        </FormField>
      </div>
    </AppPanel>
    <SaveBar>
      <Button type="submit" variant="success" :disabled="create.isPending.value">
        <PhFloppyDisk aria-hidden="true" />
        {{ create.isPending.value ? 'Saving…' : 'Save Consult' }}
      </Button>
    </SaveBar>
  </form>
</template>
