<script setup lang="ts">
import { PhCaretDown } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { SaBaiAPIDomainReferralStateKey } from '@sabai/sdk/types'
import { REFERRAL_STATE_OPTIONS, referralStateBadgeClass } from '@/utils/referral'

const props = withDefaults(defineProps<{
  referralId: string
  modelValue: SaBaiAPIDomainReferralStateKey
  disabled?: boolean
  /** Accessible name; name the patient when several appear on one screen. */
  label?: string
  /** Names the patient in the pending-changes list while the change waits to be sent. */
  patientName?: string
  /** Save as soon as a status is picked (lists). Off on the referral page, which saves it with the rest of the form. */
  saveOnChange?: boolean
}>(), { saveOnChange: true })

const emit = defineEmits<{
  'update:modelValue': [value: SaBaiAPIDomainReferralStateKey]
}>()

const updateState = useUpdateReferralState()

function onChange(event: Event) {
  const target = event.target as HTMLSelectElement
  const newState = target.value as SaBaiAPIDomainReferralStateKey
  if (newState === props.modelValue)
    return
  if (!props.saveOnChange) {
    emit('update:modelValue', newState)
    return
  }

  updateState.mutate(
    { id: props.referralId, patientName: props.patientName, referralState: newState },
    {
      onSuccess: (outcome) => {
        emit('update:modelValue', newState)
        notifySaved(outcome, 'Referral status updated')
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
      :aria-label="props.label ?? 'Referral status'"
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
