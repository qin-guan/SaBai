<script setup lang="ts">
import { PhPencilSimple, PhPlus, PhX } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import type { SaBaiAPIEndpointsPatientsGetResponse, SaBaiAPIEndpointsPatientsVisitsGetItem } from '@sabai/sdk/types'
import type { CreateOrderContext, MedicationOrder } from '~/offline/mutations/medication'
import { ORDER_ROLES } from '~/offline/mutations/medication'

/**
 * Consults already saved for this visit (a visit can accumulate several),
 * shown alongside the form for recording a new one, with their medication
 * orders. A pending order can still be changed or cancelled, and an order can
 * be added to a saved consult; dispensed and cancelled orders are read-only.
 */
const props = defineProps<{
  visitId: string
  highlightConsultId?: string | null
  patient?: SaBaiAPIEndpointsPatientsGetResponse
  visit?: SaBaiAPIEndpointsPatientsVisitsGetItem
}>()
const { data, isLoading } = useVisitConsults(() => props.visitId)
const consults = computed(() => data.value ?? [])

const { user } = useCurrentUser()
const canOrder = computed(() => !!user.value?.roles.some(r => ORDER_ROLES.includes(r)))

const { data: allOrders } = useOrders(undefined, { enabled: canOrder })
const ordersByConsultId = computed(() => {
  const map = new Map<string, MedicationOrder[]>()
  if (!canOrder.value)
    return map
  for (const o of allOrders.value ?? []) {
    const list = map.get(o.consultId) ?? []
    list.push(o)
    map.set(o.consultId, list)
  }
  return map
})

const { data: availability } = useIngredientAvailability({ enabled: canOrder })
const unitOf = (ingredientId: string) => availability.value?.find(a => a.ingredientId === ingredientId)?.unitOfMeasurement

const adding = ref<string | null>(null)
const changing = ref<MedicationOrder | null>(null)
const cancelling = ref<MedicationOrder | null>(null)

const orderContext = computed<CreateOrderContext | undefined>(() => {
  const { patient, visit } = props
  if (!patient || !visit)
    return undefined
  return {
    patientId: patient.id,
    patientDisplayId: patient.displayId,
    patientName: patient.name,
    villageCode: visit.villageCode ?? patient.villageCode ?? null,
    visitId: visit.id,
    visitDate: visit.date,
    diagnoses: [],
    isLowStock: false,
  }
})

const hasHighlightedConsult = computed(() =>
  !!props.highlightConsultId && consults.value.some(c => c.id === props.highlightConsultId),
)

const consultRefs = ref<Record<string, HTMLElement | null>>({})
function setConsultRef(id: string, el: Element | ComponentPublicInstance | null) {
  consultRefs.value[id] = el instanceof HTMLElement ? el : null
}

watch(
  [consults, () => props.highlightConsultId],
  async ([, targetId]) => {
    if (!targetId)
      return
    await nextTick()
    consultRefs.value[targetId]?.scrollIntoView({ behavior: 'smooth', block: 'nearest' })
  },
  { immediate: true },
)

const fields = [
  { key: 'pastMedicalHistory', label: 'Past Medical History' },
  { key: 'consultation', label: 'Consultation' },
] as const
const trailing = [
  { key: 'treatmentPlan', label: 'Plan' },
  { key: 'remarks', label: 'Remarks' },
] as const
</script>

<template>
  <StateMessage v-if="isLoading" kind="loading" message="Loading previous consults..." />
  <AppPanel
    v-else-if="consults.length > 0"
    :title="`Previous consults from this visit (${consults.length})`"
    description="Earlier notes for this visit"
    collapsible
    :force-open="hasHighlightedConsult"
    collapse-key="previous-consults"
    flush
  >
    <article
      v-for="consult in consults"
      :id="`consult-${consult.id}`"
      :key="consult.id"
      :ref="(el) => setConsultRef(consult.id, el)"
      class="flex flex-col gap-2 border-b border-border px-3 py-2 last:border-b-0 scroll-mt-28 transition-colors"
      :class="consult.id === props.highlightConsultId && 'border-l-4 border-l-primary bg-accent/40'"
    >
      <div class="flex items-center justify-between gap-2">
        <div>
          <div class="flex flex-wrap items-center gap-1.5">
            <p class="text-xs font-semibold text-muted-foreground touch:text-sm">
              {{ formatVisitDate(consult.date) }}
            </p>
            <span
              v-if="consult.id === props.highlightConsultId"
              class="border border-band-border bg-banner px-1.5 py-0.5 text-[0.6875rem] font-semibold text-chrome"
            >
              Referred consult
            </span>
          </div>
          <p v-if="consult.doctorNickname" class="text-xs text-muted-foreground">
            Dr. {{ consult.doctorNickname }}
          </p>
        </div>
        <ConsultPdfLink :consult-id="consult.id" size="sm" />
      </div>
      <template v-for="f in fields" :key="f.key">
        <div v-if="consult[f.key]">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ f.label }}
          </p>
          <p class="whitespace-pre-wrap break-words">
            {{ consult[f.key] }}
          </p>
        </div>
      </template>
      <div v-if="consult.diagnoses.length > 0">
        <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
          Diagnoses
        </p>
        <ul class="mt-0.5 flex flex-col gap-0.5">
          <li v-for="d in consult.diagnoses" :key="d.id" class="break-words">
            <span class="font-semibold">{{ d.category ?? '-' }}:</span> {{ d.details }}
          </li>
        </ul>
      </div>
      <div v-if="consult.referredFor" class="border border-band-border bg-accent/30 p-2 text-foreground">
        <p class="text-xs font-semibold text-chrome">
          Referred for: {{ consult.referredFor }}
        </p>
        <p v-if="consult.referralNotes" class="mt-0.5 text-xs text-muted-foreground whitespace-pre-wrap break-words">
          {{ consult.referralNotes }}
        </p>
      </div>
      <div v-if="canOrder" class="flex flex-col gap-1 border-t border-border pt-2">
        <div class="flex items-center justify-between gap-2">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            Medication orders
          </p>
          <div class="flex items-center gap-2">
            <NuxtLink
              v-if="ordersByConsultId.get(consult.id)?.some(o => o.status === 'PENDING')"
              :to="paths.orders({ visit: props.visitId })"
              class="text-xs font-medium text-primary underline hover:text-primary/80"
            >
              Pharmacy queue →
            </NuxtLink>
            <Button size="sm" variant="outline" type="button" :aria-label="`Add a medication order to the consult of ${formatVisitDate(consult.date)}`" @click="adding = consult.id">
              <PhPlus aria-hidden="true" /> Add order
            </Button>
          </div>
        </div>
        <ul v-if="ordersByConsultId.get(consult.id)?.length" class="flex flex-col gap-1.5">
          <li
            v-for="order in ordersByConsultId.get(consult.id)"
            :key="order.id"
            class="flex flex-col gap-1 border border-border bg-stripe p-1.5 text-xs"
            :data-order-status="order.status"
          >
            <div class="flex flex-wrap items-center justify-between gap-2">
              <div class="min-w-0">
                <span class="font-semibold">{{ order.activeIngredientName }}</span>
                <span class="tabular-nums"> · {{ formatQuantity(order.quantity, unitOf(order.medicationActiveIngredientId)) }}</span>
              </div>
              <div class="flex items-center gap-1">
                <OrderStatusBadge :status="order.status" />
                <template v-if="order.status === 'PENDING'">
                  <Button size="sm" variant="ghost" type="button" :aria-label="`Change order ${order.activeIngredientName}`" @click="changing = order">
                    <PhPencilSimple aria-hidden="true" /> Change
                  </Button>
                  <Button size="sm" variant="ghost" class="text-destructive" type="button" :aria-label="`Cancel order ${order.activeIngredientName}`" @click="cancelling = order">
                    <PhX aria-hidden="true" /> Cancel
                  </Button>
                </template>
              </div>
            </div>
            <p v-if="order.dosageInstructions" class="text-muted-foreground">
              {{ order.dosageInstructions }}
            </p>
            <p v-if="order.remarks" class="italic text-muted-foreground">
              Remarks: {{ order.remarks }}
            </p>
            <p v-if="order.status === 'DISPENSED' && order.dispensedAt" class="text-muted-foreground">
              Dispensed {{ formatVisitDate(order.dispensedAt) }}
            </p>
          </li>
        </ul>
        <p v-else class="text-xs text-muted-foreground">
          No medication ordered.
        </p>
      </div>

      <template v-for="f in trailing" :key="f.key">
        <div v-if="consult[f.key]">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">
            {{ f.label }}
          </p>
          <p class="whitespace-pre-wrap break-words">
            {{ consult[f.key] }}
          </p>
        </div>
      </template>
    </article>
  </AppPanel>
  <OrderDialog
    v-if="adding"
    :consult-id="adding"
    :context="orderContext"
    :visit-id="props.visitId"
    :patient-name="props.patient?.name"
    @close="adding = null"
  />
  <OrderDialog
    v-if="changing"
    :order="changing"
    :visit-id="props.visitId"
    :patient-name="props.patient?.name"
    @close="changing = null"
  />
  <CancelOrderDialog
    v-if="cancelling"
    :order="cancelling"
    :unit="unitOf(cancelling.medicationActiveIngredientId)"
    :visit-id="props.visitId"
    :patient-name="props.patient?.name"
    @close="cancelling = null"
  />
</template>
