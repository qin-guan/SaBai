<script setup lang="ts">
import { PhArrowCounterClockwise, PhFloppyDisk } from '@phosphor-icons/vue'
import type { LocalPatientSummary } from '~/db/types'
import { Button } from '@/components/ui/button'

/**
 * Patient details, edited in place on the patient record like the station
 * forms: fields are always editable, unsaved input is kept as a draft, and
 * the save bar (Ctrl+S) appears with the changes.
 */
const props = defineProps<{ patient: LocalPatientSummary }>()

const formEl = ref<HTMLFormElement>()
const { form, draft, errors, newPhoto, isDirty, isSaving, submit, reset } = usePatientEditForm(() => props.patient, formEl)
const showCamera = ref(false)

useSaveShortcut(submit)
</script>

<template>
  <form ref="formEl" novalidate aria-label="Patient details" @submit.prevent="submit" @keydown.enter="enterMovesToNextField">
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="reset" />
    <div class="flex flex-col gap-3">
      <AppPanel title="Patient details">
        <PatientFields v-model="form" :errors="errors" />
      </AppPanel>
      <AppPanel
        title="Profile photo & face"
        :description="props.patient.hasPhoto ? 'Photo on file' : 'No photo yet'"
        collapsible
        :default-open="showCamera || !!newPhoto"
      >
        <template #actions>
          <Button v-if="!showCamera && !newPhoto" size="sm" variant="outline" @click="showCamera = true">
            {{ props.patient.hasPhoto ? 'Retake photo' : 'Take photo' }}
          </Button>
        </template>
        <p class="text-xs text-muted-foreground touch:text-sm" :class="(showCamera || newPhoto) && 'mb-3'">
          {{ props.patient.hasPhoto
            ? 'A new photo replaces the current one and re-enrolls the patient\'s face.'
            : 'No photo yet. A photo sets the profile photo and enrolls the patient\'s face for face search.' }}
        </p>
        <WebcamCapture v-if="showCamera || newPhoto" v-model="newPhoto" :size="240" />
      </AppPanel>
    </div>
    <SaveBar v-if="isDirty || draft.restoredAt.value">
      <span class="text-xs font-semibold text-warning touch:text-sm">Unsaved changes</span>
      <Button variant="outline" @click="reset(); showCamera = false">
        <PhArrowCounterClockwise aria-hidden="true" /> Reset
      </Button>
      <Button type="submit" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" />
        {{ isSaving ? 'Saving…' : 'Save details' }}
      </Button>
    </SaveBar>
  </form>
</template>
