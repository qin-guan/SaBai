<script setup lang="ts">
import type { MedicationOrder } from '~/offline/mutations/medication'

/**
 * Read-only consults already saved for this visit (a visit can accumulate
 * several), shown alongside the form for recording a new one.
 */
const props = defineProps<{
  visitId: string
  highlightConsultId?: string | null
}>()
const { data, isLoading } = useVisitConsults(() => props.visitId)
const consults = computed(() => data.value ?? [])

const { data: allOrders } = useOrders()
const ordersByConsultId = computed(() => {
  const map = new Map<string, MedicationOrder[]>()
  for (const o of allOrders.value ?? []) {
    const list = map.get(o.consultId) ?? []
    list.push(o)
    map.set(o.consultId, list)
  }
  return map
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
      <div v-if="ordersByConsultId.get(consult.id)?.length" class="flex flex-col gap-1 border-t border-border pt-2">
        <div class="flex items-center justify-between">
          <p class="text-xs touch:text-xs font-semibold text-muted-foreground">Prescriptions</p>
          <NuxtLink
            v-if="ordersByConsultId.get(consult.id)!.some(o => o.status === 'PENDING')"
            :to="paths.orders({ visit: props.visitId })"
            class="text-xs font-medium text-primary underline hover:text-primary/80"
          >
            View in Pharmacy Queue →
          </NuxtLink>
        </div>
        <ul class="flex flex-col gap-1.5">
          <li v-for="order in ordersByConsultId.get(consult.id)" :key="order.id" class="border border-border bg-stripe p-1.5 text-xs">
            <div class="flex items-center justify-between gap-2">
              <div>
                <span class="font-semibold">{{ order.brandName }}</span> × {{ order.quantity }}
              </div>
              <span
                class="border px-1.5 py-0.5 text-[0.6875rem] font-semibold"
                :class="{
                  'border-success/40 bg-success-subtle text-success': order.status === 'APPROVED',
                  'border-destructive/40 bg-destructive-subtle text-destructive': order.status === 'CANCELLED',
                  'border-warning/40 bg-warning-subtle text-warning': order.status === 'PENDING',
                }"
              >
                {{ order.status === 'APPROVED' ? 'Dispensed' : order.status === 'CANCELLED' ? 'Cancelled' : 'Pending' }}
              </span>
            </div>
            <p v-if="order.dosageInstructions" class="mt-0.5 text-muted-foreground">
              {{ order.dosageInstructions }}
            </p>
            <p v-if="order.remarks" class="mt-0.5 italic text-muted-foreground">
              Remarks: {{ order.remarks }}
            </p>
          </li>
        </ul>
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
</template>
