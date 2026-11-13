<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'
import type { ReferralDetail } from '@/utils/referral'

const props = defineProps<{
  referral: ReferralDetail
}>()

const pnMemberOptions = usePnMemberOptions(() => props.referral.memberOnRunId)
const updateParty = useUpdateReferralParty()

const form = ref({
  memberOnRunId: props.referral.memberOnRunId ?? '',
  presentAtReferralParty: !!props.referral.presentAtReferralParty,
  referralPartyResults: props.referral.referralPartyResults ?? '',
  followUp: !!props.referral.followUp,
  doctorsUpdated: !!props.referral.doctorsUpdated,
})

watch(
  () => props.referral,
  (next) => {
    form.value = {
      memberOnRunId: next.memberOnRunId ?? '',
      presentAtReferralParty: !!next.presentAtReferralParty,
      referralPartyResults: next.referralPartyResults ?? '',
      followUp: !!next.followUp,
      doctorsUpdated: !!next.doctorsUpdated,
    }
  },
)

function submit() {
  if (updateParty.isPending.value) return

  updateParty.mutate(
    {
      id: props.referral.id,
      memberOnRunId: form.value.memberOnRunId || null,
      presentAtReferralParty: form.value.presentAtReferralParty,
      referralPartyResults: form.value.referralPartyResults.trim() || null,
      followUp: form.value.followUp,
      doctorsUpdated: form.value.doctorsUpdated,
    },
    {
      onSuccess: () => toast.success('Referral party details saved'),
      onError: err => toast.error(getApiErrorMessage(err, 'Failed to save referral party details')),
    },
  )
}
</script>

<template>
  <form class="flex flex-col gap-4" novalidate @submit.prevent="submit">
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div class="flex flex-col gap-1.5">
        <Label for="party-member-on-run" class="text-xs font-semibold">PN member on run</Label>
        <NativeSelect id="party-member-on-run" v-model="form.memberOnRunId" class="text-xs touch:h-11">
          <NativeSelectOption value="">Not selected</NativeSelectOption>
          <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <div class="flex items-end pb-2">
        <div class="flex items-center gap-2">
          <Checkbox
            id="party-present"
            :model-value="form.presentAtReferralParty"
            @update:model-value="(v) => { form.presentAtReferralParty = v === true }"
          />
          <Label for="party-present" class="text-xs font-semibold">
            Present at referral party
          </Label>
        </div>
      </div>
    </div>

    <div class="flex flex-col gap-1.5">
      <Label for="party-results" class="text-xs font-semibold">Referral party results</Label>
      <Textarea
        id="party-results"
        v-model="form.referralPartyResults"
        rows="4"
        placeholder="Type the results from the referral party..."
        class="text-xs touch:text-base border-input"
      />
    </div>

    <div class="flex flex-col gap-3 sm:flex-row sm:gap-8">
      <div class="flex items-center gap-2">
        <Checkbox
          id="party-follow-up"
          :model-value="form.followUp"
          @update:model-value="(v) => { form.followUp = v === true }"
        />
        <Label for="party-follow-up" class="text-xs font-semibold">Follow up</Label>
      </div>

      <div class="flex items-center gap-2">
        <Checkbox
          id="party-doctors-updated"
          :model-value="form.doctorsUpdated"
          @update:model-value="(v) => { form.doctorsUpdated = v === true }"
        />
        <Label for="party-doctors-updated" class="text-xs font-semibold">Doctors updated</Label>
      </div>
    </div>

    <div class="flex justify-end">
      <Button
        type="submit"
        variant="success"
        size="sm"
        class="w-full sm:w-44 touch:h-10"
        :disabled="updateParty.isPending.value"
      >
        <PhFloppyDisk class="size-3.5" aria-hidden="true" />
        <span>{{ updateParty.isPending.value ? 'Saving…' : 'Save Party' }}</span>
      </Button>
    </div>
  </form>
</template>
