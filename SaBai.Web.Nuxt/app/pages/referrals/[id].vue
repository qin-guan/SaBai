<script setup lang="ts">
import { PhArrowCounterClockwise, PhCheck, PhFloppyDisk, PhStethoscope } from '@phosphor-icons/vue'
import { usePreferredReducedMotion } from '@vueuse/core'
import { Button } from '@/components/ui/button'
import { Kbd } from '@/components/ui/kbd'
import { Textarea } from '@/components/ui/textarea'

/**
 * One referral, worked like a station form: the four steps (booth → party →
 * payment → outcome) are always editable, Ctrl/⌘+S saves every step that
 * changed from anywhere on the page, Enter moves to the next field, and 1–4
 * jump to a step. The status is part of the same save.
 */
const referralId = useRouteId('id')
const { data: referral, isLoading, isError } = useReferral(referralId)
const { data: patient } = usePatient(() => referral.value?.patientId ?? null)
const { data: partners } = useReferralPartnersList()
const { data: staff } = useStaffUsersList()

useHead({
  title: () => (referral.value ? `Referral #${referral.value.id} · ${referral.value.patientName} · Sa'Bai` : "Referral · Sa'Bai"),
})

const formEl = ref<HTMLFormElement>()
const { form, saved, changedSteps, isDirty, isSaving, isAdult, moneyErrors, draft, save, reset } = useReferralForm(referral, formEl)
useSaveShortcut(save, { enabled: () => !!form.value })

// Tells the list which row to return focus to (Esc / back).
const lastOpened = useState<string | null>('referrals:last-opened', () => null)
watch(referralId, (id) => { lastOpened.value = id }, { immediate: true })

const names = {
  partner: (id: string) => partners.value?.find(p => p.id === id)?.name,
  member: (id: string) => {
    const user = staff.value?.find(u => u.id === id)
    return user ? user.nickname || user.userName : undefined
  },
}

const steps = computed(() =>
  REFERRAL_STEPS.map((step, i) => ({
    step,
    number: i + 1,
    label: REFERRAL_STEP_LABELS[step],
    anchor: `referral-step-${step}`,
    started: !!saved.value && isReferralStepStarted(step, saved.value),
    changed: changedSteps.value.includes(step),
    summary: form.value ? referralStepSummary(step, form.value, names) : '',
  })),
)

const prefersReducedMotion = usePreferredReducedMotion()

/** Scrolls to a step and puts the cursor in its first field. */
function goToStep(step: ReferralStep) {
  const panel = document.getElementById(`referral-step-${step}`)
  if (!panel)
    return
  panel.scrollIntoView({ block: 'start', behavior: prefersReducedMotion.value === 'reduce' ? 'auto' : 'smooth' })
  panel.querySelector<HTMLElement>('input, select, textarea, button[role="checkbox"]')?.focus({ preventScroll: true })
}
REFERRAL_STEPS.forEach((step, i) => useKeyShortcut(String(i + 1), () => goToStep(step), { enabled: () => !!form.value }))

const unsavedLabel = computed(() => changedSteps.value.map(s => REFERRAL_PART_LABELS[s].toLowerCase()).join(', '))
</script>

<template>
  <div class="flex flex-col">
    <StateMessage v-if="isLoading" kind="loading" message="Loading referral…" />
    <StateMessage v-else-if="isError || !referral" kind="error" message="This referral could not be loaded. Check the link, or go back to Referrals." />

    <template v-else>
      <PageHeader
        :title="`Referral #${referral.id} for ${referral.patientName}`"
        hide-title
        :breadcrumbs="[
          { label: 'Home', to: '/' },
          { label: 'Referrals', to: paths.referrals() },
          { label: `Referral #${referral.id}` },
        ]"
      />

      <PatientBanner v-if="patient" :patient="patient" :to="paths.patientDetail(referral.patientId)" />
      <!-- Same footprint while the patient loads, so the page does not shift. -->
      <section v-else aria-label="Patient" class="mb-3 flex items-center gap-3 border border-band-border bg-banner px-3 py-2">
        <PatientPhoto :patient-id="referral.patientId" :has-photo="referral.hasPhoto" :size="48" alt="" :name="referral.patientName" />
        <div class="min-w-0">
          <p class="text-base font-bold sm:text-lg">
            {{ referral.patientName }}
          </p>
          <p class="text-xs touch:text-sm">
            <PatientCode :village-code="referral.villageCode" :village-color-hex="referral.villageColorHex" :id="referral.patientNumber" />
          </p>
        </div>
      </section>

      <div class="grid gap-3 lg:grid-cols-[minmax(0,1fr)_19rem] lg:items-start">
        <!-- Summary first on phones (status is what people come to change); a side rail on laptops. -->
        <aside
          aria-label="Referral summary"
          class="flex flex-col gap-3 lg:sticky lg:top-[calc(var(--app-header-h,3rem)+0.75rem)] lg:order-2"
        >
          <AppPanel title="Referral" flush>
            <dl class="divide-y divide-border text-xs touch:text-sm">
              <div class="flex flex-col gap-1 px-3 py-2">
                <dt class="flex items-center gap-2 font-semibold text-muted-foreground">
                  Status
                  <span v-if="changedSteps.includes('status')" class="text-warning">Unsaved</span>
                </dt>
                <dd v-if="form">
                  <ReferralStateDropdown v-model="form.status.referralState" :referral-id="referral.id" :save-on-change="false" class="w-full!" />
                </dd>
              </div>
              <div class="flex flex-col gap-0.5 px-3 py-2">
                <dt class="font-semibold text-muted-foreground">
                  Referred for
                </dt>
                <dd class="text-sm font-bold text-chrome touch:text-base">
                  {{ referral.referredFor }}
                </dd>
                <dd v-if="referral.referralNotes" class="whitespace-pre-wrap break-words text-foreground">
                  {{ referral.referralNotes }}
                </dd>
              </div>
              <div class="flex flex-col gap-0.5 px-3 py-2">
                <dt class="font-semibold text-muted-foreground">
                  Referred
                </dt>
                <dd class="tabular-nums">
                  {{ formatVisitDate(referral.createdAt) }}<template v-if="referral.doctorNickname">
                    by Dr. {{ referral.doctorNickname }}
                  </template>
                </dd>
                <dd class="mt-1 flex flex-wrap gap-2">
                  <Button as-child variant="outline" size="sm">
                    <NuxtLink :to="paths.consultsDetail(referral.patientId, undefined, referral.consultId)">
                      <PhStethoscope aria-hidden="true" /> Consult
                    </NuxtLink>
                  </Button>
                  <ConsultPdfLink :consult-id="referral.consultId" />
                </dd>
              </div>
              <div class="flex flex-col gap-0.5 px-3 py-2">
                <dt class="font-semibold text-muted-foreground">
                  Patient phone
                </dt>
                <dd v-if="referral.patientContactNo" class="font-mono tabular-nums">
                  <a :href="`tel:${referral.patientContactNo.replace(/[^\d+]/g, '')}`" class="text-primary underline-offset-2 hover:underline">
                    {{ referral.patientContactNo }}
                  </a>
                </dd>
                <dd v-else class="text-muted-foreground">
                  Not recorded. Add it on the patient record.
                </dd>
              </div>
            </dl>
          </AppPanel>

          <!-- The workflow at a glance; each step jumps to its section (keys 1–4). -->
          <nav v-if="form" aria-label="Referral steps" class="border border-border bg-card">
            <ol class="divide-y divide-border">
              <li v-for="s in steps" :key="s.step">
                <button
                  type="button"
                  class="flex w-full items-start gap-2.5 px-3 py-2 text-left hover:bg-accent/60 focus-visible:bg-accent focus-visible:outline-none touch:min-h-11"
                  :aria-keyshortcuts="String(s.number)"
                  @click="goToStep(s.step)"
                >
                  <span
                    class="mt-px inline-flex size-5 shrink-0 items-center justify-center border text-xs font-bold"
                    :class="s.started ? 'border-chrome bg-chrome text-white' : 'border-band-border bg-card text-chrome'"
                    aria-hidden="true"
                  >
                    <PhCheck v-if="s.started && !s.changed" weight="bold" class="size-3" />
                    <template v-else>{{ s.number }}</template>
                  </span>
                  <span class="flex min-w-0 flex-1 flex-col">
                    <span class="flex items-center gap-2 text-xs font-bold text-chrome touch:text-sm">
                      {{ s.label }}
                      <span v-if="s.changed" class="font-semibold text-warning">Unsaved</span>
                    </span>
                    <span class="truncate text-xs text-muted-foreground touch:text-sm">{{ s.summary }}</span>
                  </span>
                  <Kbd class="mt-px hidden kb:inline-flex">{{ s.number }}</Kbd>
                </button>
              </li>
            </ol>
          </nav>
        </aside>

        <form
          v-if="form"
          ref="formEl"
          novalidate
          aria-label="Referral workflow"
          class="flex min-w-0 flex-col gap-3 lg:order-1"
          @submit.prevent="save"
          @keydown.enter="enterMovesToNextField"
        >
          <DraftNotice :restored-at="draft.restoredAt.value" class="mb-0!" @discard="reset" />

          <AppPanel :id="steps[0]!.anchor" :step="1" :title="steps[0]!.label" description="At the booth: where the patient is going and who is responsible.">
            <template v-if="steps[0]!.changed" #actions>
              <span class="text-xs font-semibold text-warning touch:text-sm">Unsaved</span>
            </template>
            <ReferralBoothSection
              v-model="form.booth"
              :is-adult="isAdult"
              :saved-partner-id="saved?.booth.referredToId ?? ''"
              :saved-member-id="saved?.booth.memberInChargeId ?? ''"
            />
          </AppPanel>

          <AppPanel :id="steps[1]!.anchor" :step="2" :title="steps[1]!.label" description="The trip to the partner clinic and what came of it.">
            <template v-if="steps[1]!.changed" #actions>
              <span class="text-xs font-semibold text-warning touch:text-sm">Unsaved</span>
            </template>
            <ReferralPartySection v-model="form.party" :saved-member-id="saved?.party.memberOnRunId ?? ''" />
          </AppPanel>

          <AppPanel :id="steps[2]!.anchor" :step="3" :title="steps[2]!.label" description="What was paid for the referral, in either currency.">
            <template v-if="steps[2]!.changed" #actions>
              <span class="text-xs font-semibold text-warning touch:text-sm">Unsaved</span>
            </template>
            <ReferralMoneySection v-model="form.money" :saved-member-id="saved?.money.memberWhoPaidId ?? ''" :errors="moneyErrors" />
          </AppPanel>

          <AppPanel :id="steps[3]!.anchor" :step="4" :title="steps[3]!.label" description="The final result. Set the status to completed when it is done.">
            <template v-if="steps[3]!.changed" #actions>
              <span class="text-xs font-semibold text-warning touch:text-sm">Unsaved</span>
            </template>
            <FormField v-slot="{ id }" label="Outcome from the partner clinic">
              <Textarea :id="id" v-model="form.outcome.referralOutcome" :rows="3" placeholder="e.g. Seen on 3 Nov, started treatment, review in 3 months" />
            </FormField>
          </AppPanel>

          <SaveBar class="mt-0!">
            <template #hint>
              <ShortcutHint keys="Ctrl+S" /> saves every step
            </template>
            <span
              class="text-xs touch:text-sm"
              :class="isDirty ? 'font-semibold text-warning' : 'text-muted-foreground'"
              role="status"
            >
              {{ isDirty ? `Unsaved: ${unsavedLabel}` : 'All changes saved' }}
            </span>
            <Button v-if="isDirty" variant="outline" :disabled="isSaving" @click="reset">
              <PhArrowCounterClockwise aria-hidden="true" /> Undo changes
            </Button>
            <Button type="submit" variant="success" :disabled="isSaving || !isDirty">
              <PhFloppyDisk aria-hidden="true" />
              {{ isSaving ? 'Saving…' : 'Save changes' }}
            </Button>
          </SaveBar>
        </form>
      </div>
    </template>
  </div>
</template>
