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

      <!-- Patient context banner (storyboard) -->
      <section aria-label="Patient" class="border border-band-border bg-banner p-3">
        <div class="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
          <div class="flex items-center gap-3 min-w-0">
            <PatientPhoto
              :patient-id="referral.patientId"
              :has-photo="referral.hasPhoto"
              :name="referral.patientName"
              :size="52"
              zoomable
            />
            <div class="min-w-0">
              <div class="flex items-center gap-1.5">
                <span
                  class="size-2.5 shrink-0 border border-border/40"
                  :style="{ backgroundColor: referral.villageColorHex }"
                  :title="referral.villageCode ?? undefined"
                  aria-hidden="true"
                />
                <span class="font-mono text-xs font-semibold text-muted-foreground">
                  {{ patientCode }}
                </span>
                <span class="text-xs text-muted-foreground">· Village {{ referral.villageCode }}</span>
              </div>
              <h2 class="text-base font-bold text-foreground sm:text-lg">
                {{ referral.patientName }}
              </h2>
            </div>
          </div>

          <div class="flex flex-wrap items-center gap-2 max-sm:[&>*]:flex-1">
            <Button as-child variant="outline" size="sm" class="touch:h-10">
              <NuxtLink :to="paths.patientDetail(referral.patientId)">
                <PhUser class="size-4" aria-hidden="true" />
                <span>Patient record</span>
              </NuxtLink>
            </Button>

            <Button as-child variant="outline" size="sm" class="touch:h-10">
              <NuxtLink :to="paths.consultsDetail(referral.patientId)">
                <PhStethoscope class="size-4" aria-hidden="true" />
                <span>Consults</span>
              </NuxtLink>
            </Button>

            <ConsultPdfLink
              :consult-id="referral.consultId"
              variant="default"
              size="sm"
              label="Consult PDF"
              class="touch:h-10"
            />
          </div>
        </div>
      </section>

      <!-- Referral Details Card -->
      <AppPanel title="Referral Information" flush>
        <dl class="divide-y divide-border text-xs touch:text-sm">
          <!-- Status -->
          <div class="grid grid-cols-1 gap-1.5 px-3 py-3 sm:grid-cols-[12rem_1fr] sm:items-center">
            <dt class="font-semibold text-muted-foreground">
              Referral Status
            </dt>
            <dd class="w-full sm:max-w-xs">
              <ReferralStateDropdown
                :referral-id="referral.id"
                v-model="referral.referralState"
              />
            </dd>
          </div>

          <!-- Referral Date -->
          <div class="grid grid-cols-1 gap-1 px-3 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referral Date
            </dt>
            <dd class="text-foreground">
              {{ formatVisitDate(referral.createdAt) }}
            </dd>
          </div>

          <!-- Referred For -->
          <div class="grid grid-cols-1 gap-1 px-3 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referred For
            </dt>
            <dd class="font-bold text-chrome">
              {{ referral.referredFor }}
            </dd>
          </div>

          <!-- Notes -->
          <div class="grid grid-cols-1 gap-1 px-3 py-3 sm:grid-cols-[12rem_1fr]">
            <dt class="font-semibold text-muted-foreground">
              Referral Notes
            </dt>
            <dd class="text-foreground whitespace-pre-wrap">
              {{ referral.referralNotes || 'No notes entered' }}
            </dd>
          </div>

          <!-- Outcome -->
          <div class="grid grid-cols-1 gap-1.5 px-3 py-3 sm:grid-cols-[12rem_1fr]">
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
                  class="text-xs touch:text-base border-input"
                />
                <div class="flex items-center gap-2 max-sm:[&>*]:flex-1">
                  <Button
                    size="sm"
                    variant="success"
                    class="touch:h-10"
                    :disabled="updateOutcome.isPending.value"
                    @click="saveOutcome"
                  >
                    <PhFloppyDisk class="size-3.5" aria-hidden="true" />
                    <span>{{ updateOutcome.isPending.value ? 'Saving…' : 'Save Outcome' }}</span>
                  </Button>
                  <Button
                    size="sm"
                    variant="outline"
                    class="touch:h-10"
                    :disabled="updateOutcome.isPending.value"
                    @click="cancelEditingOutcome"
                  >
                    <PhX class="size-3.5" aria-hidden="true" />
                    <span>Cancel</span>
                  </Button>
                </div>
              </div>

              <div v-else class="flex flex-col sm:flex-row sm:items-start justify-between gap-3 max-w-xl">
                <div class="flex-1 min-w-0">
                  <p v-if="referral.referralOutcome" class="whitespace-pre-wrap text-foreground border-l-2 border-success/60 bg-success-subtle/30 px-3 py-2 text-xs touch:text-sm">
                    {{ referral.referralOutcome }}
                  </p>
                  <p v-else class="text-muted-foreground italic text-xs touch:text-sm">
                    No outcome recorded yet
                  </p>
                </div>
                <Button
                  variant="outline"
                  size="sm"
                  class="h-7 text-xs shrink-0 self-start touch:h-10 max-sm:w-full"
                  @click="startEditingOutcome"
                >
                  <PhPencilSimple class="size-3.5" aria-hidden="true" />
                  <span>Edit outcome</span>
                </Button>
              </div>
            </dd>
          </div>

          <!-- Consultation Origin & PDF -->
          <div class="grid grid-cols-1 gap-1.5 px-3 py-3 sm:grid-cols-[12rem_1fr] sm:items-center">
            <dt class="font-semibold text-muted-foreground">
              Consultation Origin
            </dt>
            <dd class="flex flex-col items-start gap-2 sm:flex-row sm:flex-wrap sm:items-center sm:gap-3">
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
                class="max-sm:w-full touch:h-10"
              />
            </dd>
          </div>
        </dl>
      </AppPanel>

      <!-- Back link -->
      <div class="mt-2">
        <Button as-child variant="ghost" size="sm" class="touch:h-10">
          <NuxtLink :to="paths.referrals()">
            <PhArrowLeft class="size-4" aria-hidden="true" />
            <span>Back to Referrals</span>
          </NuxtLink>
        </Button>
      </div>
    </template>
  </div>
</template>
