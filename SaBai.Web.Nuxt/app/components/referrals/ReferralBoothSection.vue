<script setup lang="ts">
import { Checkbox } from '@/components/ui/checkbox'
import { Label } from '@/components/ui/label'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'

/** Step 1, at the referral booth: where the patient goes and who looks after it. Saved by the page. */
const model = defineModel<ReferralFormValues['booth']>({ required: true })
const props = defineProps<{
  isAdult: boolean
  /** Saved values, so an inactive partner or former PN member still shows by name. */
  savedPartnerId: string
  savedMemberId: string
}>()

const { data: partners } = useReferralPartnersList()
const pnMemberOptions = usePnMemberOptions(() => props.savedMemberId)

const partnerOptions = computed(() =>
  (partners.value ?? [])
    .filter(p => p.isActive || p.id === props.savedPartnerId)
    .map(p => ({ value: p.id, label: p.isActive ? p.name : `${p.name} (inactive)` })),
)
</script>

<template>
  <div class="flex flex-col gap-3">
    <div class="grid grid-cols-1 gap-3 sm:grid-cols-2">
      <FormField v-slot="{ id }" label="Clinic referred to">
        <NativeSelect :id="id" v-model="model.referredToId">
          <NativeSelectOption value="">
            Not chosen
          </NativeSelectOption>
          <NativeSelectOption v-for="opt in partnerOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>

      <FormField v-slot="{ id }" label="Class of service">
        <NativeSelect :id="id" v-model="model.classOfService">
          <NativeSelectOption value="">
            Not chosen
          </NativeSelectOption>
          <NativeSelectOption v-for="opt in REFERRAL_SERVICE_CLASS_OPTIONS" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>

      <FormField v-slot="{ id }" label="PN member in charge">
        <NativeSelect :id="id" v-model="model.memberInChargeId">
          <NativeSelectOption value="">
            Not chosen
          </NativeSelectOption>
          <NativeSelectOption v-for="opt in pnMemberOptions" :key="opt.value" :value="opt.value">
            {{ opt.label }}
          </NativeSelectOption>
        </NativeSelect>
      </FormField>

      <div class="flex flex-col justify-end gap-1 sm:pb-1.5">
        <p v-if="props.isAdult" class="text-xs text-muted-foreground touch:text-sm">
          Guardians: not needed, the patient is an adult.
        </p>
        <div v-else class="flex items-center gap-2">
          <Checkbox
            id="booth-guardians"
            :model-value="model.guardiansContacted"
            @update:model-value="(v) => { model.guardiansContacted = v === true }"
          />
          <Label for="booth-guardians" class="font-semibold">Guardians contacted</Label>
        </div>
      </div>
    </div>

    <FormField v-slot="{ id }" label="Description of service">
      <Textarea :id="id" v-model="model.descriptionOfService" :rows="2" placeholder="What the patient is being referred for, e.g. cardiology review and echo" />
    </FormField>
    <FormField v-slot="{ id }" label="Patient summary from screening">
      <Textarea :id="id" v-model="model.patientSummaryFromScreening" :rows="2" />
    </FormField>
    <FormField v-slot="{ id }" label="Remarks">
      <Textarea :id="id" v-model="model.remarksAtBooth" :rows="2" />
    </FormField>
  </div>
</template>
