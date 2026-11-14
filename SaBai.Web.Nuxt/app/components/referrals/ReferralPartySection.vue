<script setup lang="ts">
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'

/** Step 2, the referral party (the trip to the partner clinic). Saved by the page. */
const model = defineModel<ReferralFormValues['party']>({ required: true })
const props = defineProps<{ savedMemberId: string }>()

const pnMemberOptions = usePnMemberOptions(() => props.savedMemberId)

const checks = [
  { key: 'presentAtReferralParty', id: 'party-present', label: 'Patient came to the referral party' },
  { key: 'followUp', id: 'party-follow-up', label: 'Needs follow-up' },
  { key: 'doctorsUpdated', id: 'party-doctors-updated', label: 'Doctors updated' },
] as const
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <FormField v-slot="{ id }" label="PN member on the run">
        <NativeSelect :id="id" v-model="model.memberOnRunId">
          <NativeSelectOption value="">
            Not chosen
          </NativeSelectOption>
          <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>
    </div>

    <FormField v-slot="{ id }" label="Results from the referral party">
      <Textarea :id="id" v-model="model.referralPartyResults" :rows="3" placeholder="What the partner clinic found or did" />
    </FormField>

    <fieldset class="flex flex-col gap-2 sm:flex-row sm:flex-wrap sm:gap-x-6">
      <legend class="sr-only">
        Referral party checks
      </legend>
      <div v-for="c in checks" :key="c.key" class="flex items-center gap-2 touch:min-h-11">
        <Checkbox
          :id="c.id"
          :model-value="model[c.key]"
          @update:model-value="(v) => { model[c.key] = v === true }"
        />
        <Label :for="c.id" class="font-semibold">{{ c.label }}</Label>
      </div>
    </fieldset>
  </div>
</template>
