<script setup lang="ts">
import { toast } from 'vue-sonner'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { REFERRAL_STATE_OPTIONS } from '@/utils/referral'

const props = defineProps<{
  referralId: number
  modelValue: SaBaiAPIDomainReferralStateKey
  disabled?: boolean
}>()

const emit = defineEmits<{
  'update:modelValue': [value: SaBaiAPIDomainReferralStateKey]
}>()

const updateState = useUpdateReferralState()

function onChange(event: Event) {
  const target = event.target as HTMLSelectElement
  const newState = target.value as SaBaiAPIDomainReferralStateKey
  if (newState === props.modelValue)
    return

  updateState.mutate(
    {
      id: props.referralId,
      referralState: newState,
    },
    {
      onSuccess: () => {
        emit('update:modelValue', newState)
        toast.success('Referral status updated')
      },
      onError: (err) => {
        // Reset back to previous value
        target.value = props.modelValue
        toast.error(getApiErrorMessage(err, 'Failed to update referral status'))
      },
    },
  )
}
</script>

<template>
  <div class="relative min-w-[130px]">
    <NativeSelect
      :model-value="props.modelValue"
      :disabled="props.disabled || updateState.isPending.value"
      aria-label="Referral status"
      class="w-full text-xs"
      @change="onChange"
    >
      <NativeSelectOption
        v-for="opt in REFERRAL_STATE_OPTIONS"
        :key="opt.value"
        :value="opt.value"
      >
        {{ opt.label }}
      </NativeSelectOption>
    </NativeSelect>
  </div>
</template>
