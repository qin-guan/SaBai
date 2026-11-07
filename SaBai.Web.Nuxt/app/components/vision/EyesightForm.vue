<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import type { z } from 'zod'
import {
  saBaiAPIEndpointsEyesightCreatePostEndpoint,
  saBaiAPIEndpointsEyesightUpdateByVisitPatchEndpoint,
} from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsEyesightCreatePostRequest } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'

/**
 * Full eyesight record for a visit (acuity, pinhole, astigmatism, prescribed
 * glasses, comments). Acuity/pinhole fields validate after first blur and then
 * live, so errors show before Save and clear once the input is valid.
 */
const props = defineProps<{ visitId: number }>()

type Key = keyof EyesightFormValues

const VALIDATORS: Partial<Record<Key, z.ZodType>> = {
  leftEyeDegree: visualAcuitySchema,
  rightEyeDegree: visualAcuitySchema,
  leftEyePinhole: pinholeSchema,
  rightEyePinhole: pinholeSchema,
}

const SECTIONS: { title: string, fields: { name: Key, label: string, side: 'Right' | 'Left' }[] }[] = [
  {
    title: 'Visual Acuity (Degree)',
    fields: [
      { name: 'rightEyeDegree', label: 'Right Eye Degree', side: 'Right' },
      { name: 'leftEyeDegree', label: 'Left Eye Degree', side: 'Left' },
    ],
  },
  {
    title: 'Pinhole',
    fields: [
      { name: 'rightEyePinhole', label: 'Right Eye Pinhole', side: 'Right' },
      { name: 'leftEyePinhole', label: 'Left Eye Pinhole', side: 'Left' },
    ],
  },
  {
    title: 'Astigmatism',
    fields: [
      { name: 'rightAstigmatism', label: 'Right Astigmatism', side: 'Right' },
      { name: 'leftAstigmatism', label: 'Left Astigmatism', side: 'Left' },
    ],
  },
  {
    title: 'Prescribed Glasses (Degree)',
    fields: [
      { name: 'rightPrescribedGlassesDegree', label: 'Right Prescribed Glasses Degree', side: 'Right' },
      { name: 'leftPrescribedGlassesDegree', label: 'Left Prescribed Glasses Degree', side: 'Left' },
    ],
  },
]

const { data: eyesight, isLoading } = useVisitEyesight(() => props.visitId)
const form = ref<EyesightFormValues>({ ...BLANK_EYESIGHT })
const draft = useFormDraft(() => `eyesight:${props.visitId}`, form)
const baseline = ref('')
const touched = ref(new Set<Key>())
const isDirty = computed(() => JSON.stringify(form.value) !== baseline.value)
const formEl = ref<HTMLFormElement>()
// Times this form for the station wait estimate.
const serviceTimer = useServiceTimer('vision', formEl, () => form.value)

function load(values: EyesightFormValues) {
  form.value = { ...values }
  baseline.value = JSON.stringify(form.value)
  touched.value = new Set()
}

watch([eyesight, isLoading], () => {
  if (isLoading.value)
    return
  const data = (eyesight.value ?? {}) as Partial<Record<Key, string | null>>
  const values = { ...BLANK_EYESIGHT }
  for (const key of Object.keys(values) as Key[])
    values[key] = data[key] ?? ''
  load(values)
  form.value = { ...draft.resume(values) }
}, { immediate: true })

const errors = computed(() => {
  const result: Partial<Record<Key, string>> = {}
  for (const [key, schema] of Object.entries(VALIDATORS) as [Key, z.ZodType][]) {
    if (!touched.value.has(key))
      continue
    const message = schemaError(schema, form.value[key])
    if (message)
      result[key] = message
  }
  return result
})

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: (body: SaBaiAPIEndpointsEyesightCreatePostRequest) =>
    saBaiAPIEndpointsEyesightCreatePostEndpoint({ ...apiConfig, body, throwOnError: true }).unwrap(),
})
const update = useMutation({
  mutationFn: (body: SaBaiAPIEndpointsEyesightCreatePostRequest) =>
    saBaiAPIEndpointsEyesightUpdateByVisitPatchEndpoint({
      ...apiConfig,
      path: { visitId: props.visitId },
      body,
      throwOnError: true,
    }).unwrap(),
})
const saving = computed(() => create.isPending.value || update.isPending.value)

function submit() {
  if (saving.value || isLoading.value)
    return
  touched.value = new Set(Object.keys(VALIDATORS) as Key[])
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }
  if (!isDirty.value) {
    toast.error('No form field changed!')
    return
  }

  const data = { ...form.value }
  const body: SaBaiAPIEndpointsEyesightCreatePostRequest = {
    visitId: props.visitId,
    leftEyeDegree: data.leftEyeDegree || undefined,
    rightEyeDegree: data.rightEyeDegree || undefined,
    leftEyePinhole: data.leftEyePinhole || undefined,
    rightEyePinhole: data.rightEyePinhole || undefined,
    leftAstigmatism: data.leftAstigmatism || undefined,
    rightAstigmatism: data.rightAstigmatism || undefined,
    leftPrescribedGlassesDegree: data.leftPrescribedGlassesDegree || undefined,
    rightPrescribedGlassesDegree: data.rightPrescribedGlassesDegree || undefined,
    comments: data.comments || undefined,
  }
  const callbacks = {
    onSuccess: () => {
      serviceTimer.saved()
      queryClient.invalidateQueries({ queryKey: queryKeys.eyesight(props.visitId) })
      // Refresh the journey bars and station backlogs.
      queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
      toast.success('Vision record saved successfully!')
      load(data)
      draft.clear(data)
    },
    onError: (error: unknown) => toast.error(getApiErrorMessage(error, 'Failed to save vision record.')),
  }
  if (!eyesight.value)
    create.mutate(body, callbacks)
  else update.mutate(body, callbacks)
}

useSaveShortcut(submit)
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading vision data" />
  <form v-else ref="formEl" novalidate aria-label="Vision record" @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <AppPanel title="Eyesight" description="Right eye first, then left (VA notation e.g. 6/9, 6/9+2, CF, HM, LP, NLP).">
      <div class="flex flex-col divide-y divide-border">
        <div
          v-for="section in SECTIONS"
          :key="section.title"
          class="grid grid-cols-1 gap-3 py-3 first:pt-0 md:grid-cols-[12rem_1fr_1fr] md:items-start"
        >
          <h3 class="text-[0.8125rem] font-bold text-chrome md:pt-6 touch:text-sm">
            {{ section.title }}
          </h3>
          <FormField
            v-for="field in section.fields"
            :key="field.name"
            v-slot="{ id, describedby, invalid }"
            :label="field.label"
            :error="errors[field.name]"
          >
            <Input
              :id="id"
              v-model="form[field.name]"
              autocapitalize="characters"
              autocomplete="off"
              spellcheck="false"
              :aria-describedby="describedby"
              :aria-invalid="invalid || undefined"
              class="font-mono uppercase"
              @blur="touched.add(field.name)"
            />
          </FormField>
        </div>
        <div class="pt-3">
          <FormField v-slot="{ id }" label="Comments">
            <Textarea :id="id" v-model="form.comments" :rows="3" />
          </FormField>
        </div>
      </div>
    </AppPanel>
    <SaveBar>
      <span v-if="isDirty" class="text-xs font-semibold text-warning touch:text-sm">Unsaved changes</span>
      <Button type="submit" variant="success" :disabled="saving">
        <PhFloppyDisk aria-hidden="true" />
        {{ saving ? 'Saving…' : 'Save Records' }}
      </Button>
    </SaveBar>
  </form>
</template>
