<script setup lang="ts">
import { Badge } from '@/components/ui/badge'
import { calculateAge } from '@/utils/patient'
import type { ReferralDetail } from '@/utils/referral'

const props = defineProps<{
  referral: ReferralDetail
}>()

const cards = computed(() =>
  [
    { label: 'POOR', held: !!props.referral.patientHasPoorCard },
    { label: 'BS2', held: !!props.referral.patientHasBS2Card },
    { label: 'Sabai Card', held: !!props.referral.patientHasSabaiCard },
  ].filter(c => c.held),
)

const dobLabel = computed(() => {
  if (!props.referral.patientDateOfBirth) return null
  const date = new Date(props.referral.patientDateOfBirth)
  if (Number.isNaN(date.getTime())) return null
  return `${date.toLocaleDateString()} (${calculateAge(date)} years)`
})
</script>

<template>
  <dl class="divide-y divide-border text-xs touch:text-sm">
    <div class="grid grid-cols-1 gap-1 py-2.5 sm:grid-cols-[12rem_1fr]">
      <dt class="font-semibold text-muted-foreground">Date of Birth</dt>
      <dd class="text-foreground">
        <span v-if="dobLabel">{{ dobLabel }}</span>
        <span v-else class="text-muted-foreground italic">Not recorded</span>
      </dd>
    </div>

    <div class="grid grid-cols-1 gap-1 py-2.5 sm:grid-cols-[12rem_1fr]">
      <dt class="font-semibold text-muted-foreground">Phone Number</dt>
      <dd class="text-foreground">
        <span v-if="referral.patientContactNo">{{ referral.patientContactNo }}</span>
        <span v-else class="text-muted-foreground italic">Not recorded</span>
      </dd>
    </div>

    <div class="grid grid-cols-1 gap-1 py-2.5 sm:grid-cols-[12rem_1fr] sm:items-center">
      <dt class="font-semibold text-muted-foreground">Cards</dt>
      <dd>
        <ul v-if="cards.length > 0" class="flex flex-wrap gap-1.5">
          <li v-for="card in cards" :key="card.label">
            <Badge variant="outline" class="border-success/40 bg-success-subtle text-success">
              {{ card.label }}
            </Badge>
          </li>
        </ul>
        <span v-else class="text-muted-foreground italic">No cards</span>
      </dd>
    </div>

    <div class="grid grid-cols-1 gap-1 py-2.5 sm:grid-cols-[12rem_1fr]">
      <dt class="font-semibold text-muted-foreground">Referral Type</dt>
      <dd class="font-bold text-chrome">{{ referral.referredFor }}</dd>
    </div>

    <div class="grid grid-cols-1 gap-1 py-2.5 sm:grid-cols-[12rem_1fr]">
      <dt class="font-semibold text-muted-foreground">Referral Notes</dt>
      <dd class="text-foreground whitespace-pre-wrap">
        <span v-if="referral.referralNotes">{{ referral.referralNotes }}</span>
        <span v-else class="text-muted-foreground italic">No notes entered</span>
      </dd>
    </div>
  </dl>
</template>
