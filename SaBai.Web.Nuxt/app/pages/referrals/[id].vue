<script setup lang="ts">
import { PhArrowLeft, PhCheck, PhFilePdf, PhFloppyDisk, PhPencilSimple, PhStethoscope, PhUser, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Textarea } from '@/components/ui/textarea'
import { formatPatientCode } from '@/utils/patient'
import { formatVisitDate } from '@/utils/visit'

const referralId = useRouteId('id')
const { data: referral, isLoading, isError } = useReferral(referralId)

const isEditingOutcome = ref(false)
const outcomeDraft = ref('')
const updateOutcome = useUpdateReferralOutcome()

const patientCode = computed(() => {
  if (!referral.value) return ''
  return formatPatientCode(referral.value.villageCode, referral.value.patientId)
})

useHead({
  title: () => (referral.value ? `Referral #${referral.value.id} · ${referral.value.patientName} · Sa'Bai` : "Referral · Sa'Bai"),
})

function startEditingOutcome() {
  outcomeDraft.value = referral.value?.referralOutcome ?? ''
  isEditingOutcome.value = true
}

function cancelEditingOutcome() {
  isEditingOutcome.value = false
  outcomeDraft.value = ''
}

function saveOutcome() {
  if (!referral.value || updateOutcome.isPending.value) return

  updateOutcome.mutate(
    {
      id: referral.value.id,
      referralOutcome: outcomeDraft.value.trim(),
    },
    {
      onSuccess: () => {
        isEditingOutcome.value = false
        toast.success('Referral outcome saved')
      },
      onError: (err) => {
        toast.error(getApiErrorMessage(err, 'Failed to save referral outcome.'))
      },
    },
  )
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <StateMessage v-if="isLoading" kind="loading" message="Loading referral details…" />
    <StateMessage v-else-if="isError || !referral" kind="error" message="Referral not found or failed to load." />

    <template v-else>
      <PageHeader
        :title="`Referral #${referral.id}`"
        :description="`${referral.patientName} (${patientCode})`"
        :breadcrumbs="[
          { label: 'Home', to: '/' },
          { label: 'Referrals', to: paths.referrals() },
          { label: `Referral #${referral.id}` },
        ]"
      />

      <!-- Patient context banner -->
      <div class="flex flex-col sm:flex-row sm:items-center justify-between gap-4 rounded border border-border bg-card p-4">
        <div class="flex items-center gap-4">
          <PatientPhoto
            :patient-id="referral.patientId"
            :has-photo="referral.hasPhoto"
            :name="referral.patientName"
            :size="64"
            zoomable
          />
          <div>
            <div class="flex items-center gap-2">
              <span
                class="size-3 rounded-full"
                :style="{ backgroundColor: referral.villageColorHex }"
                :title="referral.villageCode"
                aria-hidden="true"
              />
              <span class="font-mono text-sm font-semibold text-muted-foreground">
                {{ patientCode }}
              </span>
            </div>
            <h2 class="text-lg font-bold text-foreground">
              {{ referral.patientName }}
            </h2>
            <p class="text-xs text-muted-foreground">
              Village: {{ referral.villageCode }}
            </p>
          </div>
        </div>

        <div class="flex flex-wrap items-center gap-2">
          <Button as-child variant="outline" size="sm">
            <NuxtLink :to="paths.patientDetail(referral.patientId)">
              <PhUser class="size-4" aria-hidden="true" />
              <span>Patient record</span>
            </NuxtLink>
          </Button>

          <Button as-child variant="outline" size="sm">
            <NuxtLink :to="paths.consultsDetail(referral.patientId)">
              <PhStethoscope class="size-4" aria-hidden="true" />
              <span>Patient's consults</span>
            </NuxtLink>
          </Button>

          <ConsultPdfLink
            :consult-id="referral.consultId"
            variant="default"
            size="sm"
            label="Consult PDF"
          />
        </div>
      </div>

      <!-- Referral Details Card -->
      <AppPanel title="Referral Information" description="Care tracking and follow-up details">
        <dl class="divide-y divide-border text-xs touch:text-sm">
          <!-- Status -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr] sm:items-center">
            <dt class="font-semibold text-muted-foreground">
              Referral Status
            </dt>
            <dd class="max-w-xs">
              <ReferralStateDropdown
                :referral-id="referral.id"
                v-model="referral.referralState"
              />
            </dd>
          </div>

          <!-- Referral Date -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referral Date
            </dt>
            <dd class="text-foreground">
              {{ formatVisitDate(referral.createdAt) }}
            </dd>
          </div>

          <!-- Referred For -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referred For
            </dt>
            <dd class="font-semibold text-primary">
              {{ referral.referredFor }}
            </dd>
          </div>

          <!-- Notes -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referral Notes
            </dt>
            <dd class="text-foreground whitespace-pre-wrap">
              {{ referral.referralNotes || 'No notes entered' }}
            </dd>
          </div>

          <!-- Outcome -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referral Outcome
            </dt>
            <dd>
              <div v-if="isEditingOutcome" class="flex flex-col gap-2 max-w-xl">
                <Textarea
                  v-model="outcomeDraft"
                  rows="4"
                  placeholder="Enter referral outcome from hospital / external clinic..."
                  aria-label="Referral outcome"
                  class="text-xs touch:text-sm"
                />
                <div class="flex items-center gap-2">
                  <Button
                    size="sm"
                    variant="success"
                    :disabled="updateOutcome.isPending.value"
                    @click="saveOutcome"
                  >
                    <PhFloppyDisk class="size-3.5" aria-hidden="true" />
                    <span>{{ updateOutcome.isPending.value ? 'Saving…' : 'Save Outcome' }}</span>
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    :disabled="updateOutcome.isPending.value"
                    @click="cancelEditingOutcome"
                  >
                    <PhX class="size-3.5" aria-hidden="true" />
                    <span>Cancel</span>
                  </Button>
                </div>
              </div>

              <div v-else class="flex items-start justify-between gap-4 max-w-xl">
                <p class="whitespace-pre-wrap text-foreground">
                  <span v-if="referral.referralOutcome">{{ referral.referralOutcome }}</span>
                  <span v-else class="text-muted-foreground italic">No outcome recorded yet</span>
                </p>
                <Button
                  variant="outline"
                  size="sm"
                  class="h-7 text-xs shrink-0"
                  @click="startEditingOutcome"
                >
                  <PhPencilSimple class="size-3.5" aria-hidden="true" />
                  <span>Edit outcome</span>
                </Button>
              </div>
            </dd>
          </div>

          <!-- Consultation Origin & PDF -->
          <div class="grid grid-cols-1 gap-1 py-3 sm:grid-cols-[12rem_1fr] sm:items-center">
            <dt class="font-semibold text-muted-foreground">
              Consultation Origin
            </dt>
            <dd class="flex flex-wrap items-center gap-3">
              <span class="text-foreground">
                Visited: {{ formatVisitDate(referral.consultDate) }}
                <template v-if="referral.doctorNickname">
                  (Dr. {{ referral.doctorNickname }})
                </template>
              </span>

              <ConsultPdfLink
                :consult-id="referral.consultId"
                variant="outline"
                size="sm"
                label="View Consult PDF"
              />
            </dd>
          </div>
        </dl>
      </AppPanel>

      <!-- Back link -->
      <div class="mt-2">
        <Button as-child variant="ghost" size="sm">
          <NuxtLink :to="paths.referrals()">
            <PhArrowLeft class="size-4" aria-hidden="true" />
            <span>Back to Referrals</span>
          </NuxtLink>
        </Button>
      </div>
    </template>
  </div>
</template>
