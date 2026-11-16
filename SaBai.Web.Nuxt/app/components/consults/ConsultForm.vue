<script setup lang="ts">
import { PhFloppyDisk, PhPlus, PhTrash } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { z } from 'zod'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Textarea } from '@/components/ui/textarea'
import { createConsult } from '~/offline/mutations/visitRecords'
import type { CreateReferralContext } from '~/offline/mutations/referrals'
import type { SaBaiAPIEndpointsPatientsGetResponse, SaBaiAPIEndpointsPatientsVisitsGetItem } from '@sabai/sdk/types'
import { REFERRAL_CATEGORY_OPTIONS } from '@/utils/referral'

/**
 * The doctor's consultation form for one visit (each save appends a consult).
 * `patient` and `visit` let a referral made offline show in the referral list
 * before it reaches the server.
 */
const props = defineProps<{
  visitId: string
  patientName?: string
  patient?: SaBaiAPIEndpointsPatientsGetResponse
  visit?: SaBaiAPIEndpointsPatientsVisitsGetItem
}>()

const consultFormSchema = z.object({
  pastMedicalHistory: z.string().trim().optional(),
  consultation: z.string().trim().optional(),
  treatmentPlan: z.string().trim().optional(),
  remarks: z.string().trim().optional(),
  referredFor: z.string().optional(),
  referralNotes: z.string().trim().optional(),
  diagnoses: z.array(
    z.object({
      details: z.string().trim().min(1, 'Diagnosis details are required'),
      category: z.string().min(1, 'Please select a category'),
    }),
  ),
  orders: z.array(
    z.object({
      medicationBrandId: z.string().min(1, 'Please select a medication'),
      quantity: z.string().trim().min(1, 'Quantity is required').refine((val) => {
        const n = Number(val)
        return Number.isInteger(n) && n >= 1
      }, 'Quantity must be a whole number of at least 1'),
      dosageInstructions: z.string().trim().min(1, 'Dosage instructions are required'),
      remarks: z.string().trim().optional(),
    }),
  ).default([]),
})
type ConsultFormValues = z.infer<typeof consultFormSchema>

let nextKey = 1
// Stable key for the first diagnosis so a blank form always compares equal.
const blank = () => ({
  pastMedicalHistory: '',
  consultation: '',
  treatmentPlan: '',
  remarks: '',
  referredFor: 'Not Referred',
  referralNotes: '',
  diagnoses: [{ key: 0, details: '', category: '' }],
  orders: [] as Array<{ key: number, medicationBrandId: string, quantity: string, dosageInstructions: string, remarks: string }>,
})

const form = ref(blank())
// Each save appends a consult, so the draft is the consult being written now.
const draft = useFormDraft(() => `consult:${props.visitId}`, form)
form.value = draft.resume(blank())
form.value.orders ??= []
nextKey = Math.max(0, ...form.value.diagnoses.map(d => d.key), ...form.value.orders.map(o => o.key)) + 1
const errors = ref<Record<string, string>>({})
const formEl = ref<HTMLFormElement>()
// Times this form for the station wait estimate.
const serviceTimer = useServiceTimer('consults', formEl, () => form.value)

function addDiagnosis() {
  form.value.diagnoses.push({ key: nextKey++, details: '', category: '' })
  nextTick(() => {
    const selects = formEl.value?.querySelectorAll<HTMLSelectElement>('[data-diagnosis-category]')
    selects?.[selects.length - 1]?.focus()
  })
}

function removeDiagnosis(index: number) {
  form.value.diagnoses.splice(index, 1)
  errors.value = {}
}

function addOrder() {
  form.value.orders.push({ key: nextKey++, medicationBrandId: '', quantity: '1', dosageInstructions: '', remarks: '' })
  nextTick(() => {
    const selects = formEl.value?.querySelectorAll<HTMLSelectElement>('[data-order-medication]')
    selects?.[selects.length - 1]?.focus()
  })
}

function removeOrder(index: number) {
  form.value.orders.splice(index, 1)
  errors.value = {}
}

const brandsWithIngredient = useMedicationBrandsWithIngredient()
const brandRecords = useMedicationBrands()
const ingredients = useMedicationIngredients()
const stock = useMedicationStock()

const brandMetaById = computed(() => {
  const map = new Map<string, { brandName: string, unitOfMeasurement: string | null, isLowStock: boolean, activeQty: number }>()
  const rawStock = stock.data.value ?? []
  const rawBrands = brandsWithIngredient.data.value ?? []
  const rawIngredients = ingredients.data.value ?? []

  const activeQtyByBrand = new Map<string, number>()
  const activeQtyByIngredient = new Map<string, number>()

  for (const s of rawStock) {
    if (s.stockStatus === 'active' && s.quantity > 0) {
      activeQtyByBrand.set(s.medicationBrandId, (activeQtyByBrand.get(s.medicationBrandId) ?? 0) + s.quantity)
      activeQtyByIngredient.set(s.ingredientId, (activeQtyByIngredient.get(s.ingredientId) ?? 0) + s.quantity)
    }
  }

  const ingredientMap = new Map(rawIngredients.map(i => [i.id, i]))

  for (const b of rawBrands) {
    const fullBrand = (brandRecords.data.value ?? []).find(x => x.id === b.id)
    const ingredientId = fullBrand?.activeIngredientId ?? ''
    const ing = ingredientMap.get(ingredientId)
    const activeQty = activeQtyByBrand.get(b.id) ?? 0
    const ingredientQty = activeQtyByIngredient.get(ingredientId) ?? 0
    const fallBelow = ing?.fallBelow ?? 0
    const isLowStock = ingredientQty <= fallBelow

    map.set(b.id, {
      brandName: b.name,
      unitOfMeasurement: ing?.unitOfMeasurement ?? null,
      isLowStock,
      activeQty,
    })
  }

  return map
})

const medicationOptions = computed(() => {
  const raw = brandsWithIngredient.data.value ?? []
  return [...raw]
    .sort((a, b) => {
      const cmp = a.activeIngredientName.localeCompare(b.activeIngredientName)
      return cmp !== 0 ? cmp : a.name.localeCompare(b.name)
    })
    .map(b => {
      const meta = brandMetaById.value.get(b.id)
      const activeQty = meta?.activeQty ?? 0
      const unit = meta?.unitOfMeasurement
      const isLowStock = meta?.isLowStock ?? false
      const statusSuffix = activeQty === 0 ? ' (Out of stock)' : isLowStock ? ' (Low stock)' : ''
      const label = `${b.activeIngredientName} (${b.name}) — ${activeQty} ${unit ? `${unit} ` : ''}in stock${statusSuffix}`
      return { value: b.id, label }
    })
})

const { user } = useCurrentUser()
const consultMutation = useOfflineMutation(createConsult)
const createReferral = useCreateReferral()
const createOrderMutation = useCreateOrder()
const isSaving = consultMutation.isPending

function referralContext(consultDate: string): CreateReferralContext | undefined {
  const { patient, visit } = props
  if (!patient || !visit)
    return undefined
  return {
    patientId: patient.id,
    patientName: patient.name,
    hasPhoto: patient.hasPhoto,
    villageCodeId: visit.villageCodeId,
    villageCode: visit.villageCode,
    villageColorHex: visit.villageColorHex,
    doctorId: user.value?.id ?? '',
    doctorNickname: user.value?.nickname ?? '',
    consultDate,
    patientDateOfBirth: patient.dateOfBirth,
    patientContactNo: patient.contactNo,
  }
}

async function submit() {
  if (isSaving.value)
    return
  const parsed = consultFormSchema.safeParse(form.value)
  if (!parsed.success) {
    errors.value = Object.fromEntries(parsed.error.issues.map(i => [i.path.join('.'), i.message]))
    toast.error('Please fill in all required fields before saving.')
    focusFirstInvalid(formEl.value)
    return
  }
  errors.value = {}
  const data: ConsultFormValues = parsed.data
  const hasReferral = Boolean(data.referredFor && data.referredFor !== 'Not Referred')
  const consultId = uuidv7()
  const consultDate = new Date().toISOString()

  let outcome
  try {
    outcome = await consultMutation.mutateAsync({
      id: consultId,
      visitId: props.visitId,
      patientId: props.patient?.id,
      patientName: props.patientName,
      doctorId: user.value?.id ?? '',
      doctorNickname: user.value?.nickname ?? '',
      date: consultDate,
      pastMedicalHistory: data.pastMedicalHistory || null,
      consultation: data.consultation || null,
      treatmentPlan: data.treatmentPlan || null,
      remarks: data.remarks || null,
      referredFor: hasReferral ? data.referredFor! : null,
      referralNotes: hasReferral && data.referralNotes ? data.referralNotes : null,
      diagnoses: data.diagnoses.map(d => ({ id: uuidv7(), details: d.details, category: d.category as DiagnosisCategory })),
    })
  }
  catch (error) {
    toast.error(getApiErrorMessage(error, 'Failed to save consult.'))
    return
  }
  // Before the form is cleared for the next consult.
  serviceTimer.saved()
  notifySaved(outcome, 'Consult has been saved successfully!')

  if (hasReferral) {
    try {
      // Queued behind the consult when the consult itself is waiting to be sent.
      await createReferral.mutateAsync({
        consultId,
        referredFor: data.referredFor!,
        referralNotes: data.referralNotes || undefined,
        context: referralContext(consultDate),
      })
      if (!outcome.queued) toast.success('Referral submitted!')
    }
    catch (error) {
      toast.error(getApiErrorMessage(error, 'Failed to create referral.'))
    }
  }

  if (data.orders.length > 0) {
    for (const order of data.orders) {
      try {
        const meta = brandMetaById.value.get(order.medicationBrandId)
        await createOrderMutation.mutateAsync({
          id: uuidv7(),
          consultId,
          medicationBrandId: order.medicationBrandId,
          brandName: meta?.brandName ?? 'Medication',
          quantity: Number(order.quantity),
          dosageInstructions: order.dosageInstructions.trim(),
          remarks: order.remarks?.trim() || null,
          createdAt: consultDate,
          context: props.patient && props.visit
            ? {
                patientId: props.patient.id,
                patientDisplayId: props.patient.displayId,
                patientName: props.patient.name,
                villageCode: props.visit.villageCode ?? props.patient.villageCode ?? null,
                visitId: props.visit.id,
                visitDate: props.visit.date,
                diagnoses: data.diagnoses.map(d => ({ category: d.category as DiagnosisCategory, details: d.details })),
                isLowStock: meta?.isLowStock ?? false,
              }
            : undefined,
        })
      }
      catch (error) {
        toast.error(getApiErrorMessage(error, 'Failed to create medication order.'))
      }
    }
    if (!outcome.queued) {
      toast.success('Medication orders placed!')
    }
  }

  form.value = blank()
  draft.clear(form.value)
}

useSaveShortcut(submit)
</script>

<template>
  <form ref="formEl" novalidate aria-label="Doctor's consult form" class="min-w-0" @submit.prevent="submit"
    @keydown.enter="enterMovesToNextField">
    <DraftNotice :restored-at="draft.restoredAt.value" @discard="form = draft.discard()" />
    <AppPanel title="Doctor's Consult Form">
      <div class="flex flex-col gap-3">
        <FormField v-slot="{ id }" label="Past Medical History">
          <Textarea :id="id" v-model="form.pastMedicalHistory" :rows="3" placeholder="Type the patient's past medical history here..." />
        </FormField>
        <FormField v-slot="{ id }" label="Consultation">
          <Textarea :id="id" v-model="form.consultation" :rows="4" placeholder="Type your consultation here..." />
        </FormField>

        <fieldset class="flex flex-col gap-2">
          <legend class="mb-1 text-xs font-semibold touch:text-sm">
            Diagnoses <span class="text-destructive" aria-hidden="true">*</span><span class="sr-only">(at least one, required)</span>
          </legend>
          <div
            v-for="(diagnosis, index) in form.diagnoses"
            :key="diagnosis.key"
            class="flex flex-col gap-2 border border-border bg-stripe p-2"
          >
            <div class="flex items-center justify-between">
              <p class="text-xs font-bold text-muted-foreground touch:text-sm">
                Diagnosis {{ index + 1 }}
              </p>
              <Button
                v-if="form.diagnoses.length > 1"
                variant="ghost"
                size="sm"
                class="text-destructive"
                :aria-label="`Remove diagnosis ${index + 1}`"
                @click="removeDiagnosis(index)"
              >
                <PhTrash aria-hidden="true" /> Remove
              </Button>
            </div>
            <div class="grid grid-cols-1 gap-2 sm:grid-cols-[14rem_1fr]">
              <FormField v-slot="{ id, describedby, invalid }" label="Category" required :error="errors[`diagnoses.${index}.category`]">
                <NativeSelect
                  :id="id"
                  v-model="diagnosis.category"
                  data-diagnosis-category
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                >
                  <NativeSelectOption value="">
                    Choose a category
                  </NativeSelectOption>
                  <NativeSelectOption v-for="option in DIAGNOSIS_CATEGORY_OPTIONS" :key="option.value" :value="option.value">
                    {{ option.label }}
                  </NativeSelectOption>
                </NativeSelect>
              </FormField>
              <FormField v-slot="{ id, describedby, invalid }" label="Details" required :error="errors[`diagnoses.${index}.details`]">
                <Textarea
                  :id="id"
                  v-model="diagnosis.details"
                  :rows="2"
                  placeholder="Describe the diagnosis..."
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                />
              </FormField>
            </div>
          </div>
          <Button variant="outline" class="self-start" @click="addDiagnosis">
            <PhPlus aria-hidden="true" /> Add Diagnosis
          </Button>
        </fieldset>

        <FormField v-slot="{ id }" label="Plan">
          <Textarea :id="id" v-model="form.treatmentPlan" :rows="3" placeholder="Type your plan here..." />
        </FormField>

        <fieldset class="flex flex-col gap-2">
          <legend class="mb-1 text-xs font-semibold touch:text-sm">Medication Orders</legend>
          <div v-if="form.orders.length === 0">
            <Button variant="outline" class="self-start" type="button" @click="addOrder">
              <PhPlus aria-hidden="true" /> Add Medication
            </Button>
          </div>
          <div
            v-for="(order, index) in form.orders"
            :key="order.key"
            class="flex flex-col gap-2 border border-border bg-stripe p-2"
          >
            <div class="flex items-center justify-between">
              <div class="flex items-center gap-2">
                <p class="text-xs font-bold text-muted-foreground touch:text-sm">
                  Medication {{ index + 1 }}
                </p>
                <template v-if="order.medicationBrandId">
                  <span
                    v-if="brandMetaById.get(order.medicationBrandId)?.activeQty === 0"
                    class="border border-destructive/40 bg-destructive-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-destructive"
                  >
                    Out of stock
                  </span>
                  <span
                    v-else-if="Number(order.quantity) > (brandMetaById.get(order.medicationBrandId)?.activeQty ?? 0)"
                    class="border border-warning/40 bg-warning-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-warning"
                  >
                    Exceeds active stock ({{ brandMetaById.get(order.medicationBrandId)?.activeQty }} available)
                  </span>
                  <span
                    v-else-if="brandMetaById.get(order.medicationBrandId)?.isLowStock"
                    class="border border-warning/40 bg-warning-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-warning"
                  >
                    Low stock
                  </span>
                </template>
              </div>
              <Button
                variant="ghost"
                size="sm"
                class="text-destructive"
                type="button"
                :aria-label="`Remove medication ${index + 1}`"
                @click="removeOrder(index)"
              >
                <PhTrash aria-hidden="true" /> Remove
              </Button>
            </div>
            <div class="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_8rem]">
              <FormField v-slot="{ id: fieldId, describedby, invalid }" label="Medication (Brand + Active Ingredient)" required :error="errors[`orders.${index}.medicationBrandId`]">
                <NativeSelect
                  :id="fieldId"
                  v-model="order.medicationBrandId"
                  data-order-medication
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                >
                  <NativeSelectOption value="">
                    Choose a medication
                  </NativeSelectOption>
                  <NativeSelectOption v-for="opt in medicationOptions" :key="opt.value" :value="opt.value">
                    {{ opt.label }}
                  </NativeSelectOption>
                </NativeSelect>
              </FormField>
              <FormField v-slot="{ id: fieldId, describedby, invalid }" label="Quantity" required :error="errors[`orders.${index}.quantity`]">
                <NumericInput
                  :id="fieldId"
                  v-model="order.quantity"
                  step="1"
                  min="1"
                  :unit="brandMetaById.get(order.medicationBrandId)?.unitOfMeasurement ?? undefined"
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                />
              </FormField>
            </div>
            <div class="grid grid-cols-1 gap-2 sm:grid-cols-[1fr_1fr]">
              <FormField v-slot="{ id: fieldId, describedby, invalid }" label="Dosage instructions" required :error="errors[`orders.${index}.dosageInstructions`]">
                <Input
                  :id="fieldId"
                  v-model="order.dosageInstructions"
                  placeholder="e.g. 1 tablet twice daily after meals"
                  :aria-describedby="describedby"
                  :aria-invalid="invalid || undefined"
                />
              </FormField>
              <FormField v-slot="{ id: fieldId }" label="Order remarks">
                <Input
                  :id="fieldId"
                  v-model="order.remarks"
                  placeholder="Optional notes for the pharmacist..."
                />
              </FormField>
            </div>
          </div>
          <Button v-if="form.orders.length > 0" variant="outline" class="self-start" type="button" @click="addOrder">
            <PhPlus aria-hidden="true" /> Add Medication
          </Button>
        </fieldset>

        <fieldset class="flex flex-col gap-2">
          <legend class="mb-1 text-xs font-semibold touch:text-sm">
            Referral
          </legend>
          <div class="grid grid-cols-1 gap-2 sm:grid-cols-[14rem_1fr]">
            <FormField v-slot="{ id }" label="Referred for">
              <NativeSelect :id="id" v-model="form.referredFor">
                <NativeSelectOption
                  v-for="option in REFERRAL_CATEGORY_OPTIONS"
                  :key="option.value"
                  :value="option.value"
                >
                  {{ option.label }}
                </NativeSelectOption>
              </NativeSelect>
            </FormField>
            <FormField
              v-if="form.referredFor && form.referredFor !== 'Not Referred'"
              v-slot="{ id }"
              label="Referral notes"
            >
              <Textarea
                :id="id"
                v-model="form.referralNotes"
                :rows="2"
                placeholder="Type your referral notes here..."
              />
            </FormField>
          </div>
        </fieldset>

        <FormField v-slot="{ id }" label="Remarks">
          <Textarea :id="id" v-model="form.remarks" :rows="2" placeholder="Type your remarks here..." />
        </FormField>
      </div>
    </AppPanel>
    <SaveBar>
      <Button type="submit" variant="success" :disabled="isSaving">
        <PhFloppyDisk aria-hidden="true" />
        {{ isSaving ? 'Saving…' : 'Save Consult' }}
      </Button>
    </SaveBar>
  </form>
</template>
