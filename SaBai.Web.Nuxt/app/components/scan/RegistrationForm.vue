<script setup lang="ts">
import { PhMagnifyingGlass, PhUserPlus } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsPatientsCreatePostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'

/** Registers a new patient with the captured photo, in the selected village. */
const image = defineModel<string | null>('image', { required: true })
const emit = defineEmits<{ match: [] }>()

const form = ref<PatientFormValues>(blankPatientForm())
// The photo is not kept (too large for storage); the typed details are.
const draft = useFormDraft('register-patient', form)
form.value = draft.resume(blankPatientForm())
const errors = ref<Partial<Record<keyof PatientFormValues, string>>>({})
const formEl = ref<HTMLFormElement>()
const { selectedVillageCodeId } = useVillageCode()
const { remember } = useRecentPatients()
const lastCreated = ref<{ id: number, name: string, displayId: string, visitId: number } | null>(null)
const blankJourney: Journey = {
  vitals: false,
  vision: false,
  consult: false,
  dental: false,
  dentalReferred: false,
  pharmacyOrders: 0,
  pharmacyPending: 0,
}

const queryClient = useQueryClient()
const create = useMutation({
  mutationFn: (values: PatientFormValues) =>
    saBaiAPIEndpointsPatientsCreatePostEndpoint({
      ...apiConfig,
      body: {
        name: values.name,
        identificationNumber: values.identificationNumber,
        contactNo: values.contactNo,
        gender: values.gender as 'male' | 'female',
        dateOfBirth: new Date(values.dateOfBirth).toISOString(),
        drugAllergy: values.drugAllergy,
        hasPoorCard: values.hasPoorCard,
        hasBS2Card: values.hasBS2Card,
        hasSabaiCard: values.hasSabaiCard,
        toGetReport: false,
        villageCodeId: selectedVillageCodeId.value!,
        tags: values.tags,
      } as never,
      throwOnError: true,
    }).unwrap(),
})

async function submit() {
  if (create.isPending.value)
    return
  errors.value = validatePatientForm(form.value)
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }
  if (!image.value) {
    toast.error('Please capture a face image before submitting.')
    return
  }
  if (!selectedVillageCodeId.value) {
    toast.error('Please select a village code before submitting.')
    return
  }

  let patient
  try {
    patient = await create.mutateAsync(form.value)
  }
  catch (error) {
    console.error('Error creating patient:', error)
    toast.error(getApiErrorMessage(error, 'Failed to create patient. Please try again.'))
    return
  }

  remember(patient.id)
  queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  queryClient.invalidateQueries({ queryKey: queryKeys.tags() })

  // Best-effort: the patient already exists, so a failure here must not discard the registration.
  try {
    await setPatientPhotoAndFace(patient.id, image.value)
  }
  catch (error) {
    console.error('Error saving patient photo/face:', error)
    toast.error('Patient created, but the photo or face could not be saved. You can retry from the patient page.')
  }

  toast.success('Patient created successfully!')
  lastCreated.value = {
    id: patient.id,
    name: patient.name,
    displayId: patient.displayId,
    visitId: patient.visitId,
  }
  image.value = null
  form.value = blankPatientForm()
  draft.clear(form.value)
  errors.value = {}
}

useSaveShortcut(submit)
useKeyShortcut('m', () => emit('match'))
useKeyShortcut('Escape', () => emit('match'))
</script>

<template>
  <AppPanel title="Register new patient">
    <template #actions>
      <Button size="sm" variant="outline" @click="emit('match')">
        <PhMagnifyingGlass aria-hidden="true" /> Match Instead <ShortcutHint class="hidden kb:inline-flex" keys="M" />
      </Button>
    </template>
    <div
      v-if="lastCreated"
      role="status"
      class="mb-3 flex flex-col gap-1.5 border border-success/40 bg-success-subtle px-3 py-2 text-xs touch:text-sm"
    >
      <div class="flex flex-wrap items-center justify-between gap-2">
        <p>
          Registered
          <NuxtLink
            :to="paths.patientDetail(lastCreated.id)"
            class="font-semibold text-primary underline-offset-2 hover:underline"
            :title="`Open ${lastCreated.name}'s record`"
          >
            {{ lastCreated.name }} ({{ lastCreated.displayId }})
          </NuxtLink>
          and opened a visit.
        </p>
        <NuxtLink
          :to="paths.patientDetail(lastCreated.id)"
          class="font-semibold text-primary hover:underline"
        >
          Open record →
        </NuxtLink>
      </div>
      <JourneyTracker
        :journey="blankJourney"
        :patient-id="lastCreated.id"
        :visit-id="lastCreated.visitId"
        size="compact"
      />
    </div>
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <form ref="formEl" novalidate @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
      <PatientFields v-model="form" :errors="errors" />
      <SaveBar>
        <Button type="submit" variant="success" :disabled="create.isPending.value">
          <PhUserPlus aria-hidden="true" />
          {{ create.isPending.value ? 'Creating new patient...' : 'Create New Patient' }}
        </Button>
      </SaveBar>
    </form>
  </AppPanel>
</template>
