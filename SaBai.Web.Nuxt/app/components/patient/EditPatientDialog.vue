<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import type { LocalPatientSummary } from '~/db/types'
import { Button } from '@/components/ui/button'

/**
 * Quick edit of a patient's details from a station screen, without leaving
 * the form in progress. The patient record edits the same fields inline
 * (<PatientDetailsForm>).
 */
const props = defineProps<{ patient: LocalPatientSummary }>()
const emit = defineEmits<{ close: [] }>()

const formEl = ref<HTMLFormElement>()
const { form, draft, errors, newPhoto, isSaving, submit } = usePatientEditForm(() => props.patient, formEl)

async function save() {
  if (await submit())
    emit('close')
}

useSaveShortcut(save)
</script>

<template>
  <AppDialog title="Edit Patient Details" size="lg" @close="emit('close')">
    <form id="edit-patient-form" ref="formEl" novalidate class="flex flex-col gap-4" @submit.prevent="save"
    @keydown.enter="enterMovesToNextField">
      <DraftNotice :restored-at="draft.restoredAt.value" class="mb-0" @discard="form = draft.discard()" />
      <PatientFields v-model="form" :errors="errors" />
      <AppPanel title="Profile photo & face">
        <p class="mb-3 text-xs text-muted-foreground touch:text-sm">
          {{ props.patient.hasPhoto
            ? 'Capture a new photo to replace the current one and re-enroll the patient\'s face.'
            : 'Capture a photo to set the patient\'s profile photo and enroll their face.' }}
        </p>
        <WebcamCapture v-model="newPhoto" :auto-open-camera="false" :size="240" />
      </AppPanel>
    </form>
    <template #footer>
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline touch:text-sm">
        <ShortcutHint keys="Ctrl+S" /> save · <ShortcutHint keys="Esc" /> cancel
      </span>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="edit-patient-form" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" />
        {{ isSaving ? 'Saving...' : 'Save Changes' }}
      </Button>
    </template>
  </AppDialog>
</template>
