<script setup lang="ts">
import { PhTrash } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsPatientsDeleteDeleteEndpoint } from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsPatientsGetResponse } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Admin-only permanent deletion of a patient and everything recorded for
 * them. The patient's code must be typed to confirm, since it cannot be undone.
 */
const props = defineProps<{
  patient: SaBaiAPIEndpointsPatientsGetResponse
  visitCount: number
}>()

const { isAdmin } = useCurrentUser()
const { forget } = useRecentPatients()
const queryClient = useQueryClient()

const isOpen = ref(false)
const confirmation = ref('')
const matches = computed(
  () => confirmation.value.trim().toUpperCase() === props.patient.displayId.toUpperCase(),
)

function open() {
  confirmation.value = ''
  isOpen.value = true
}

const remove = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsPatientsDeleteDeleteEndpoint({
      ...apiConfig,
      path: { id: props.patient.id },
      throwOnError: true,
    }).unwrap(),
  onSuccess: async () => {
    toast.success(`Deleted ${props.patient.name}.`)
    forget(props.patient.id)
    removeDraft(`edit-patient:${props.patient.id}`)
    isOpen.value = false
    const id = props.patient.id
    // Leave the record first so its queries are not refetched (and 404) while still mounted.
    await navigateTo(paths.patient())
    queryClient.removeQueries({ queryKey: queryKeys.patient(id) })
    queryClient.removeQueries({ queryKey: queryKeys.patientVisits(id) })
    queryClient.removeQueries({ queryKey: queryKeys.patientPhoto(id) })
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  },
  onError: (err) => {
    toast.error(getApiErrorMessage(err, 'Could not delete the patient.'))
  },
})
</script>

<template>
  <template v-if="isAdmin">
    <Button size="sm" variant="destructive" @click="open">
      <PhTrash aria-hidden="true" /> Delete Patient
    </Button>
    <AppDialog v-if="isOpen" title="Delete patient permanently?" size="sm" @close="isOpen = false">
      <form id="delete-patient-form" class="flex flex-col gap-3" @submit.prevent="matches && remove.mutate()">
        <p>
          This removes <strong>{{ patient.name }}</strong> and everything recorded for them:
          {{ visitCount }} {{ visitCount === 1 ? 'visit' : 'visits' }} with their vitals, vision,
          consults, dental records and medication orders, plus uploaded files, the profile photo
          and enrolled faces.
        </p>
        <p class="font-semibold text-destructive">
          This cannot be undone.
        </p>
        <FormField v-slot="{ id }" :label="`Type ${patient.displayId} to confirm`">
          <Input :id="id" v-model="confirmation" autocomplete="off" autocapitalize="characters" spellcheck="false" />
        </FormField>
      </form>
      <template #footer>
        <Button variant="outline" @click="isOpen = false">
          Cancel
        </Button>
        <Button
          type="submit"
          form="delete-patient-form"
          variant="destructive"
          :disabled="!matches || remove.isPending.value"
        >
          <PhTrash aria-hidden="true" /> {{ remove.isPending.value ? 'Deleting...' : 'Delete Patient' }}
        </Button>
      </template>
    </AppDialog>
  </template>
</template>
