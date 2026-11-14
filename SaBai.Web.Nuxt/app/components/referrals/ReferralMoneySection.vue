<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { MAX_RIELS, USD_AMOUNT_PATTERN, type ReferralDetail } from '@/utils/referral'

const props = defineProps<{
  referral: ReferralDetail
}>()

const pnMemberOptions = usePnMemberOptions(() => props.referral.memberWhoPaidId)
const updateMoney = useUpdateReferralMoney()

const form = ref({
  amountPaidUsd: props.referral.amountPaidUsd != null ? String(props.referral.amountPaidUsd) : '',
  amountPaidRiels: props.referral.amountPaidRiels != null ? String(props.referral.amountPaidRiels) : '',
  memberWhoPaidId: props.referral.memberWhoPaidId ?? '',
})

const errors = ref<{ amountPaidUsd?: string; amountPaidRiels?: string }>({})

watch(
  () => props.referral,
  (next) => {
    form.value = {
      amountPaidUsd: next.amountPaidUsd != null ? String(next.amountPaidUsd) : '',
      amountPaidRiels: next.amountPaidRiels != null ? String(next.amountPaidRiels) : '',
      memberWhoPaidId: next.memberWhoPaidId ?? '',
    }
    errors.value = {}
  },
)

function submit() {
  if (updateMoney.isPending.value) return
  errors.value = {}

  const usdRaw = form.value.amountPaidUsd.trim()
  const rielsRaw = form.value.amountPaidRiels.trim()

  if (usdRaw && !USD_AMOUNT_PATTERN.test(usdRaw)) {
    errors.value.amountPaidUsd = 'Enter a USD amount with up to 2 decimals'
  }
  if (rielsRaw && (!/^\d+$/.test(rielsRaw) || Number(rielsRaw) > MAX_RIELS)) {
    errors.value.amountPaidRiels = 'Enter a whole number of riels'
  }

  if (Object.keys(errors.value).length > 0) return

  updateMoney.mutate(
    {
      id: props.referral.id,
      amountPaidUsd: usdRaw ? Number(usdRaw) : null,
      amountPaidRiels: rielsRaw ? Number(rielsRaw) : null,
      memberWhoPaidId: form.value.memberWhoPaidId || null,
    },
    {
      onSuccess: () => toast.success('Payment details saved'),
      onError: err => toast.error(getApiErrorMessage(err, 'Failed to save payment details')),
    },
  )
}

const formEl = ref<HTMLElement>()
useSaveShortcut(submit, { scope: formEl })
</script>

<template>
  <form ref="formEl" class="flex flex-col gap-4" novalidate @submit.prevent="submit">
    <div class="grid grid-cols-1 gap-4 sm:grid-cols-2">
      <div class="flex flex-col gap-1.5">
        <Label for="money-usd" class="text-xs font-semibold">Amount paid (USD)</Label>
        <Input
          id="money-usd"
          v-model="form.amountPaidUsd"
          type="number"
          inputmode="decimal"
          min="0"
          step="0.01"
          placeholder="0.00"
          class="text-xs touch:h-11 touch:text-base"
          :aria-invalid="!!errors.amountPaidUsd || undefined"
        />
        <p v-if="errors.amountPaidUsd" class="text-xs font-medium text-destructive" role="alert">
          {{ errors.amountPaidUsd }}
        </p>
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="money-riels" class="text-xs font-semibold">Amount paid (Riels)</Label>
        <Input
          id="money-riels"
          v-model="form.amountPaidRiels"
          type="number"
          inputmode="numeric"
          min="0"
          step="1"
          placeholder="0"
          class="text-xs touch:h-11 touch:text-base"
          :aria-invalid="!!errors.amountPaidRiels || undefined"
        />
        <p v-if="errors.amountPaidRiels" class="text-xs font-medium text-destructive" role="alert">
          {{ errors.amountPaidRiels }}
        </p>
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="money-member-who-paid" class="text-xs font-semibold">PN member who paid</Label>
        <NativeSelect id="money-member-who-paid" v-model="form.memberWhoPaidId" class="text-xs touch:h-11">
          <NativeSelectOption value="">Not selected</NativeSelectOption>
          <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </div>
    </div>

    <div class="flex items-center justify-end gap-3">
      <span class="mr-auto hidden text-xs text-muted-foreground kb:inline">
        <ShortcutHint keys="Ctrl+S" /> to save
      </span>
      <Button
        type="submit"
        variant="success"
        size="sm"
        class="w-full sm:w-44 touch:h-10"
        :disabled="updateMoney.isPending.value"
      >
        <PhFloppyDisk class="size-3.5" aria-hidden="true" />
        <span>{{ updateMoney.isPending.value ? 'Saving…' : 'Save Payment' }}</span>
      </Button>
    </div>
  </form>
</template>
