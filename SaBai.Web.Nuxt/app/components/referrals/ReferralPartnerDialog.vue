<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'
import type { ReferralPartner } from '@/composables/useReferralPartners'

const props = defineProps<{
  partner: ReferralPartner | null
}>()
const emit = defineEmits<{ close: [] }>()

const isEditing = computed(() => !!props.partner)

const form = ref({
  name: props.partner?.name ?? '',
  remarks: props.partner?.remarks ?? '',
  isActive: props.partner?.isActive ?? true,
})

const nameError = ref('')
const formEl = ref<HTMLFormElement>()

const createPartner = useCreateReferralPartner()
const updatePartner = useUpdateReferralPartner()
const saving = computed(() => createPartner.isPending.value || updatePartner.isPending.value)

function submit() {
  if (saving.value) return
  nameError.value = ''

  if (!form.value.name.trim()) {
    nameError.value = 'Partner name is required.'
    focusFirstInvalid(formEl.value)
    return
  }

  if (isEditing.value) {
    updatePartner.mutate(
      {
        id: props.partner!.id,
        name: form.value.name,
        remarks: form.value.remarks,
        isActive: form.value.isActive,
      },
      {
        onSuccess: () => {
          toast.success('Referral partner updated')
          emit('close')
        },
        onError: err => toast.error(getApiErrorMessage(err, 'Unable to update referral partner.')),
      },
    )
    return
  }

  createPartner.mutate(
    {
      name: form.value.name,
      remarks: form.value.remarks,
      isActive: form.value.isActive,
    },
    {
      onSuccess: () => {
        toast.success('Referral partner created')
        emit('close')
      },
      onError: err => toast.error(getApiErrorMessage(err, 'Unable to create referral partner.')),
    },
  )
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog :title="isEditing ? `Edit ${props.partner!.name}` : 'New Referral Partner'" size="sm" @close="emit('close')">
    <form id="referral-partner-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Partner Name" required :error="nameError">
        <Input
          :id="id"
          v-model="form.name"
          placeholder="e.g. Siem Reap Provincial Hospital"
          :aria-describedby="describedby"
          :aria-invalid="invalid || undefined"
        />
      </FormField>

      <FormField v-slot="{ id, describedby }" label="Remarks" hint="Address, specialty, or contact details.">
        <Textarea
          :id="id"
          v-model="form.remarks"
          rows="3"
          placeholder="Optional remarks..."
          :aria-describedby="describedby"
        />
      </FormField>

      <div class="flex items-center gap-2 pt-1">
        <Checkbox
          id="partner-active"
          :model-value="form.isActive"
          @update:model-value="(v) => { form.isActive = v === true }"
        />
        <Label for="partner-active" class="text-xs font-semibold">
          Active (available in referral booth dropdown)
        </Label>
      </div>
    </form>

    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="referral-partner-form" variant="success" :disabled="saving">
        <PhFloppyDisk aria-hidden="true" /> {{ saving ? 'Saving…' : 'Save' }}
      </Button>
    </template>
  </AppDialog>
</template>
