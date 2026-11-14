<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'
import { Tooltip, TooltipContent, TooltipProvider, TooltipTrigger } from '@/components/ui/tooltip'
import { calculateAge } from '@/utils/patient'
import {
  ADULT_AGE,
  REFERRAL_SERVICE_CLASS_OPTIONS,
  type GuardiansContacted,
  type ReferralDetail,
  type ReferralServiceClass,
} from '@/utils/referral'

const props = defineProps<{
  referral: ReferralDetail
}>()

const { data: partners } = useReferralPartnersList()
const pnMemberOptions = usePnMemberOptions(() => props.referral.memberInChargeId)
const updateBooth = useUpdateReferralBooth()

const isAdult = computed(() => {
  if (!props.referral.patientDateOfBirth) return false
  return calculateAge(props.referral.patientDateOfBirth, props.referral.createdAt) >= ADULT_AGE
})

const partnerOptions = computed(() =>
  (partners.value ?? [])
    .filter(p => p.isActive || p.id === props.referral.referredToId)
    .map(p => ({
      value: p.id,
      label: p.isActive ? p.name : `${p.name} (inactive)`,
    })),
)

const form = ref({
  referredToId: props.referral.referredToId ?? '',
  classOfService: (props.referral.classOfService ?? '') as string,
  memberInChargeId: props.referral.memberInChargeId ?? '',
  guardiansContacted: props.referral.guardiansContacted === 'Yes',
  descriptionOfService: props.referral.descriptionOfService ?? '',
  patientSummaryFromScreening: props.referral.patientSummaryFromScreening ?? '',
  remarksAtBooth: props.referral.remarksAtBooth ?? '',
})

watch(
  () => props.referral,
  (next) => {
    form.value = {
      referredToId: next.referredToId ?? '',
      classOfService: next.classOfService ?? '',
      memberInChargeId: next.memberInChargeId ?? '',
      guardiansContacted: next.guardiansContacted === 'Yes',
      descriptionOfService: next.descriptionOfService ?? '',
      patientSummaryFromScreening: next.patientSummaryFromScreening ?? '',
      remarksAtBooth: next.remarksAtBooth ?? '',
    }
  },
)

function submit() {
  if (updateBooth.isPending.value) return

  const guardiansContacted: GuardiansContacted = isAdult.value
    ? 'NotApplicable'
    : form.value.guardiansContacted
      ? 'Yes'
      : 'No'

  updateBooth.mutate(
    {
      id: props.referral.id,
      referredToId: form.value.referredToId || null,
      memberInChargeId: form.value.memberInChargeId || null,
      guardiansContacted,
      classOfService: (form.value.classOfService as ReferralServiceClass) || null,
      descriptionOfService: form.value.descriptionOfService.trim() || null,
      patientSummaryFromScreening: form.value.patientSummaryFromScreening.trim() || null,
      remarksAtBooth: form.value.remarksAtBooth.trim() || null,
    },
    {
      onSuccess: () => toast.success('Referral booth details saved'),
      onError: err => toast.error(getApiErrorMessage(err, 'Failed to save referral booth details')),
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
        <Label for="booth-partner" class="text-xs font-semibold">Clinic referred to</Label>
        <NativeSelect id="booth-partner" v-model="form.referredToId" class="text-xs touch:h-11">
          <NativeSelectOption value="">Not selected</NativeSelectOption>
          <NativeSelectOption v-for="opt in partnerOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="booth-service-class" class="text-xs font-semibold">Class of service</Label>
        <NativeSelect id="booth-service-class" v-model="form.classOfService" class="text-xs touch:h-11">
          <NativeSelectOption value="">Not selected</NativeSelectOption>
          <NativeSelectOption v-for="opt in REFERRAL_SERVICE_CLASS_OPTIONS" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="booth-member-in-charge" class="text-xs font-semibold">PN member in charge</Label>
        <NativeSelect id="booth-member-in-charge" v-model="form.memberInChargeId" class="text-xs touch:h-11">
          <NativeSelectOption value="">Not selected</NativeSelectOption>
          <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <div class="flex items-end pb-2">
        <TooltipProvider v-if="isAdult">
          <Tooltip>
            <TooltipTrigger as-child>
              <div class="flex items-center gap-2 opacity-60">
                <Checkbox id="booth-guardians" :model-value="false" disabled />
                <Label for="booth-guardians" class="text-xs font-semibold cursor-not-allowed">
                  Guardians contacted
                </Label>
              </div>
            </TooltipTrigger>
            <TooltipContent>
              <p>Not applicable as the patient is an adult.</p>
            </TooltipContent>
          </Tooltip>
        </TooltipProvider>
        <div v-else class="flex items-center gap-2">
          <Checkbox
            id="booth-guardians"
            :model-value="form.guardiansContacted"
            @update:model-value="(v) => { form.guardiansContacted = v === true }"
          />
          <Label for="booth-guardians" class="text-xs font-semibold">
            Guardians contacted
          </Label>
        </div>
      </div>
    </div>

    <div class="flex flex-col gap-3">
      <div class="flex flex-col gap-1.5">
        <Label for="booth-service-desc" class="text-xs font-semibold">Description of service</Label>
        <Textarea
          id="booth-service-desc"
          v-model="form.descriptionOfService"
          rows="3"
          placeholder="Describe the service the patient is referred for..."
          class="text-xs touch:text-base border-input"
        />
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="booth-screening-summary" class="text-xs font-semibold">Patient summary from screening</Label>
        <Textarea
          id="booth-screening-summary"
          v-model="form.patientSummaryFromScreening"
          rows="3"
          placeholder="Summarise the patient from screening..."
          class="text-xs touch:text-base border-input"
        />
      </div>

      <div class="flex flex-col gap-1.5">
        <Label for="booth-remarks" class="text-xs font-semibold">Remarks</Label>
        <Textarea
          id="booth-remarks"
          v-model="form.remarksAtBooth"
          rows="3"
          placeholder="Type any remarks from the booth..."
          class="text-xs touch:text-base border-input"
        />
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
        :disabled="updateBooth.isPending.value"
      >
        <PhFloppyDisk class="size-3.5" aria-hidden="true" />
        <span>{{ updateBooth.isPending.value ? 'Saving…' : 'Save Booth' }}</span>
      </Button>
    </div>
  </form>
</template>
