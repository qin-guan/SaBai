<script setup lang="ts">
import { PhCaretDown } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { REFERRAL_STATE_OPTIONS, referralStateBadgeClass } from '@/utils/referral'

const props = defineProps<{
  referralId: string
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
  <div class="relative inline-flex w-full sm:w-auto sm:min-w-[172px]">
    <select
      :value="props.modelValue"
      :disabled="props.disabled || updateState.isPending.value"
      aria-label="Referral status"
      class="h-7 w-full appearance-none rounded-none border px-2 py-0.5 pr-7 text-xs font-semibold transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring disabled:cursor-not-allowed disabled:opacity-50 touch:h-10 touch:text-sm"
      :class="referralStateBadgeClass(props.modelValue)"
      @change="onChange"
    >
      <option
        v-for="opt in REFERRAL_STATE_OPTIONS"
        :key="opt.value"
        :value="opt.value"
        class="bg-popover text-popover-foreground font-normal"
      >
        {{ opt.label }}
      </option>
    </select>
    <PhCaretDown
      class="pointer-events-none absolute right-2 top-1/2 size-3.5 -translate-y-1/2 opacity-70"
      aria-hidden="true"
    />
  </div>
</template>
