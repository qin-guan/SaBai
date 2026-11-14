import { toast } from 'vue-sonner'
import type { Ref } from 'vue'

/**
 * The referral page's workflow form: status, booth, party, payment and outcome are
 * edited together like a station form and saved with one action (Save or
 * Ctrl/⌘+S from anywhere on the page). Only the steps that changed are sent,
 * each to its own endpoint, so a save never overwrites someone else's edit to
 * another step. Unsaved input is kept as a draft like every clinical form.
 */
export function useReferralForm(referral: Ref<ReferralDetail | null | undefined>, formEl: Ref<HTMLElement | undefined>) {
  const form = ref<ReferralFormValues>()
  /** What the server holds, per step; a step is changed when the form differs from it. */
  const saved = ref<ReferralFormValues>()
  const draft = useFormDraft(() => `referral:${referral.value?.id ?? 'pending'}`, form)
  const moneyErrors = ref<ReferralMoneyErrors>({})

  const changedSteps = computed<ReferralFormPart[]>(() =>
    form.value && saved.value ? changedReferralParts(form.value, saved.value) : [],
  )
  const isDirty = computed(() => changedSteps.value.length > 0)
  const isAdult = computed(() => !!referral.value && isAdultAt(referral.value.patientDateOfBirth, referral.value.createdAt))

  const clone = <T>(v: T): T => JSON.parse(JSON.stringify(v)) as T

  // Load once, then adopt server updates only for steps without local edits.
  watch(referral, (next) => {
    if (!next)
      return
    const fresh = referralFormValues(next)
    if (!form.value || !saved.value) {
      saved.value = clone(fresh)
      form.value = draft.resume(fresh)
      return
    }
    for (const step of REFERRAL_FORM_PARTS) {
      const unchanged = JSON.stringify(form.value[step]) === JSON.stringify(saved.value[step])
      if (unchanged)
        (form.value[step] as unknown) = clone(fresh[step])
      ;(saved.value[step] as unknown) = clone(fresh[step])
    }
  }, { immediate: true })

  // Clear a payment error as soon as the amount is edited.
  watch(() => form.value?.money.amountPaidUsd, () => { moneyErrors.value.amountPaidUsd = undefined })
  watch(() => form.value?.money.amountPaidRiels, () => { moneyErrors.value.amountPaidRiels = undefined })

  const booth = useUpdateReferralBooth()
  const party = useUpdateReferralParty()
  const money = useUpdateReferralMoney()
  const outcome = useUpdateReferralOutcome()
  const status = useUpdateReferralState()
  const isSaving = ref(false)

  function send(step: ReferralFormPart, id: string, values: ReferralFormValues) {
    const opts = { isAdult: isAdult.value }
    switch (step) {
      case 'status': return status.mutateAsync({ id, ...referralStepPayload('status', values, opts) })
      case 'booth': return booth.mutateAsync({ id, ...referralStepPayload('booth', values, opts) })
      case 'party': return party.mutateAsync({ id, ...referralStepPayload('party', values, opts) })
      case 'money': return money.mutateAsync({ id, ...referralStepPayload('money', values, opts) })
      case 'outcome': return outcome.mutateAsync({ id, ...referralStepPayload('outcome', values, opts) })
    }
  }

  async function save() {
    if (isSaving.value || !form.value || !saved.value || !referral.value)
      return
    const steps = changedSteps.value
    if (steps.length === 0) {
      toast('No changes to save.')
      return
    }

    if (steps.includes('money')) {
      moneyErrors.value = validateReferralMoney(form.value.money)
      if (moneyErrors.value.amountPaidUsd || moneyErrors.value.amountPaidRiels) {
        focusFirstInvalid(formEl.value)
        return
      }
    }

    const id = referral.value.id
    const sent = clone(form.value)
    isSaving.value = true
    const results = await Promise.allSettled(steps.map(step => send(step, id, sent)))
    isSaving.value = false

    const failed: { step: ReferralFormPart, error: unknown }[] = []
    results.forEach((result, i) => {
      const step = steps[i]!
      if (result.status === 'rejected') {
        failed.push({ step, error: result.reason })
        return
      }
      // Store what was sent as the server would echo it (trimmed, blanks → empty),
      // and tidy the field the same way unless it was edited while saving.
      const stored = referralFormValues({ ...referral.value!, ...referralStepPayload(step, sent, { isAdult: isAdult.value }) })[step]
      ;(saved.value![step] as unknown) = clone(stored)
      if (JSON.stringify(form.value![step]) === JSON.stringify(sent[step]))
        (form.value![step] as unknown) = clone(stored)
    })

    draft.clear(saved.value)

    if (failed.length === 0) {
      toast.success(steps.length === 1 ? `${REFERRAL_PART_LABELS[steps[0]!]} saved` : 'Referral saved')
      return
    }
    // Saved steps stay saved; failed ones keep their edits for another try.
    for (const { step, error } of failed)
      toast.error(`${REFERRAL_PART_LABELS[step]} not saved: ${getApiErrorMessage(error, 'the server did not accept it. Try again.')}`)
  }

  /** Puts every step back to what is saved. */
  function reset() {
    if (!saved.value)
      return
    draft.clear(saved.value)
    form.value = clone(saved.value)
    moneyErrors.value = {}
  }

  return { form, saved, changedSteps, isDirty, isSaving, isAdult, moneyErrors, draft, save, reset }
}
