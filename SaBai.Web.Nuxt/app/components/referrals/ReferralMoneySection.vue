<script setup lang="ts">
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'

/** Step 3, what was paid for the referral and by whom. Validated and saved by the page. */
const model = defineModel<ReferralFormValues['money']>({ required: true })
const props = defineProps<{
  savedMemberId: string
  errors: ReferralMoneyErrors
}>()

const pnMemberOptions = usePnMemberOptions(() => props.savedMemberId)
</script>

<template>
  <div class="grid grid-cols-1 gap-3 sm:grid-cols-3">
    <FormField v-slot="{ id, describedby, invalid }" label="Paid in US dollars" :error="props.errors.amountPaidUsd">
      <NumericInput
        :id="id"
        v-model="model.amountPaidUsd"
        unit="USD"
        step="0.01"
        min="0"
        placeholder="0.00"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
      />
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="Paid in riels" :error="props.errors.amountPaidRiels">
      <NumericInput
        :id="id"
        v-model="model.amountPaidRiels"
        unit="KHR"
        step="1"
        min="0"
        placeholder="0"
        :aria-describedby="describedby"
        :aria-invalid="invalid || undefined"
      />
    </FormField>
    <FormField v-slot="{ id }" label="PN member who paid">
      <NativeSelect :id="id" v-model="model.memberWhoPaidId">
        <NativeSelectOption value="">
          Not chosen
        </NativeSelectOption>
        <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
          {{ opt.label }}
        </NativeSelectOption>
      </NativeSelect>
    </FormField>
  </div>
</template>
