<script setup lang="ts">
import { PhArrowsLeftRight, PhCheck, PhChecks, PhPencilSimple, PhX } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { calculateAge } from '@/utils/patient'
import { formatVisitDate } from '@/utils/visit'
import type { MedicationOrder, PharmacyOrderItem, PharmacyPatientOrders, PharmacyVisitBundle } from '~/offline/mutations/medication'

/**
 * One patient in the pharmacist's queue: who they are (with the allergy flag),
 * and per visit the diagnoses and the pending orders, each with its quantity
 * (with the unit), the batches it will be dispensed from, the instructions and
 * its actions. The page owns the writes; this only reports what was asked.
 */
const props = defineProps<{
  group: PharmacyPatientOrders
  /** The patient's record, when the patient list is loaded (photo, age, allergies, link). */
  patient?: {
    id: string
    hasPhoto?: boolean
    dateOfBirth?: string | null
    gender?: string | null
    drugAllergy?: string | null
  } | null
  /** The full orders by id (remarks, and what "Change" and "Cancel" edit). */
  orders: ReadonlyMap<string, MedicationOrder>
  /** Ids of the orders being dispensed right now. */
  dispensing: ReadonlySet<string>
}>()
const emit = defineEmits<{
  dispense: [order: PharmacyOrderItem, visit: PharmacyVisitBundle]
  dispenseAll: [visit: PharmacyVisitBundle]
  change: [order: PharmacyOrderItem, visit: PharmacyVisitBundle]
  cancel: [order: PharmacyOrderItem, visit: PharmacyVisitBundle]
  changeBatch: [order: PharmacyOrderItem, visit: PharmacyVisitBundle]
}>()

const name = computed(() => props.group.patient.name)
const orderCount = computed(() => props.group.visits.reduce((n, v) => n + v.orders.length, 0))
const unitOf = (order: PharmacyOrderItem) => order.unitOfMeasurement || props.orders.get(order.orderId)?.unitOfMeasurement || undefined
const sex = computed(() => (props.patient?.gender === 'female' ? 'F' : props.patient?.gender === 'male' ? 'M' : ''))
</script>

<template>
  <section class="border border-border bg-card" :aria-label="`Orders for ${name}`" :data-queue-patient="group.patient.displayId">
    <header class="flex flex-wrap items-center gap-x-3 gap-y-1.5 border-b border-band-border bg-band px-3 py-1.5">
      <span class="flex size-9 shrink-0 overflow-hidden border border-band-border bg-card [&>*]:size-full!">
        <PatientPhoto v-if="props.patient" :patient-id="props.patient.id" :has-photo="!!props.patient.hasPhoto" :size="36" alt="" />
      </span>
      <div class="min-w-0 flex-1">
        <h2 class="truncate text-sm font-bold text-chrome touch:text-base">
          <NuxtLink v-if="props.patient" :to="paths.patientDetail(props.patient.id)" class="hover:underline">
            {{ name }}
          </NuxtLink>
          <template v-else>
            {{ name }}
          </template>
        </h2>
        <p class="flex flex-wrap items-center gap-x-2 text-xs text-muted-foreground touch:text-[0.8125rem]">
          <PatientCode :id="group.patient.displayId" :village-code="group.patient.villageCode" :display-id="group.patient.displayId" />
          <span v-if="props.patient?.dateOfBirth">{{ calculateAge(props.patient.dateOfBirth) }} y{{ sex ? ` · ${sex}` : '' }}</span>
          <span>{{ orderCount }} {{ orderCount === 1 ? 'order' : 'orders' }} to dispense</span>
        </p>
      </div>
      <AllergyFlag :allergy="props.patient?.drugAllergy" />
    </header>

    <div v-for="visit in group.visits" :key="visit.visitId" class="border-b border-border last:border-b-0" :data-queue-visit="visit.visitId">
      <div class="flex flex-wrap items-center gap-x-3 gap-y-1.5 px-3 py-1.5">
        <p class="text-xs font-semibold touch:text-sm">
          Visit {{ formatVisitDate(visit.visitDate) }}
        </p>
        <ul v-if="visit.diagnoses.length > 0" class="flex min-w-0 flex-wrap items-center gap-1" aria-label="Diagnoses">
          <li
            v-for="(diagnosis, index) in visit.diagnoses"
            :key="index"
            class="border border-border bg-muted px-1.5 py-0.5 text-[0.6875rem] touch:text-xs"
          >
            <span v-if="diagnosis.category" class="font-semibold text-muted-foreground">{{ diagnosis.category }}: </span>{{ diagnosis.details }}
          </li>
        </ul>
        <NuxtLink
          v-if="props.patient"
          :to="paths.consultsDetail(props.patient.id, visit.visitId)"
          class="text-xs font-semibold text-primary underline-offset-2 hover:underline touch:text-sm"
        >
          Open consult
        </NuxtLink>
        <Button
          v-if="visit.orders.length >= 2"
          variant="success"
          size="sm"
          class="ml-auto max-sm:w-full"
          :disabled="visit.orders.some(o => props.dispensing.has(o.orderId))"
          data-dispense-all
          @click="emit('dispenseAll', visit)"
        >
          <PhChecks aria-hidden="true" /> Dispense all {{ visit.orders.length }}
        </Button>
      </div>

      <Table class="grid-table stack-table border-t border-border lg:table-fixed">
        <TableHeader>
          <TableRow>
            <TableHead class="lg:w-[20%]">
              Medication
            </TableHead>
            <TableHead class="text-right lg:w-28">
              Quantity
            </TableHead>
            <TableHead class="lg:w-[31%]">
              Dispense from
            </TableHead>
            <TableHead>Instructions</TableHead>
            <TableHead class="text-right lg:w-[16.5rem]">
              <span class="sr-only">Actions</span>
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="order in visit.orders" :key="order.orderId" :data-order-id="order.orderId">
            <TableCell data-stack="title" class="font-semibold whitespace-normal">
              <span class="flex flex-wrap items-center gap-x-2 gap-y-0.5">
                {{ order.medicationName }}
                <span
                  v-if="order.isLowStock"
                  class="inline-flex items-center border border-warning-border bg-warning-subtle px-1.5 py-0.5 text-[0.6875rem] font-semibold text-warning"
                >Low stock</span>
              </span>
            </TableCell>
            <TableCell data-label="Quantity" class="text-right text-sm font-bold whitespace-nowrap tabular-nums">
              {{ formatQuantity(order.quantity, unitOf(order)) }}
            </TableCell>
            <TableCell data-label="Dispense from" class="whitespace-normal max-lg:min-w-60">
              <div class="flex flex-col items-start gap-1.5">
                <OrderAllocations :allocations="order.allocations" :unit="unitOf(order)" pending class="w-full" />
                <Button
                  v-if="order.allocations.length > 0"
                  variant="outline"
                  size="xs"
                  class="touch:h-10 touch:px-3 touch:text-sm"
                  :aria-label="`Change batch for ${order.medicationName} for ${name}`"
                  data-change-batch-open
                  @click="emit('changeBatch', order, visit)"
                >
                  <PhArrowsLeftRight aria-hidden="true" /> Change batch
                </Button>
              </div>
            </TableCell>
            <TableCell data-label="Instructions" class="min-w-44 whitespace-normal">
              <p>{{ order.dosageInstructions }}</p>
              <p v-if="props.orders.get(order.orderId)?.remarks" class="text-xs text-muted-foreground touch:text-[0.8125rem]">
                <span class="font-semibold">Remarks:</span> {{ props.orders.get(order.orderId)?.remarks }}
              </p>
            </TableCell>
            <TableCell data-stack="actions" class="text-right">
              <div class="flex items-center justify-end gap-1 max-lg:flex-wrap">
                <Button
                  variant="ghost"
                  size="sm"
                  :disabled="!props.orders.get(order.orderId)"
                  :aria-label="`Change order ${order.medicationName} for ${name}`"
                  @click="emit('change', order, visit)"
                >
                  <PhPencilSimple aria-hidden="true" /> Change
                </Button>
                <Button
                  variant="ghost"
                  size="sm"
                  class="text-destructive hover:text-destructive"
                  :disabled="!props.orders.get(order.orderId)"
                  :aria-label="`Cancel order ${order.medicationName} for ${name}`"
                  data-cancel-order
                  @click="emit('cancel', order, visit)"
                >
                  <PhX aria-hidden="true" /> Cancel
                </Button>
                <Button
                  variant="success"
                  size="sm"
                  :disabled="props.dispensing.has(order.orderId)"
                  :aria-label="`Dispense ${formatQuantity(order.quantity, unitOf(order))} of ${order.medicationName} for ${name}`"
                  data-dispense
                  @click="emit('dispense', order, visit)"
                >
                  <PhCheck aria-hidden="true" /> {{ props.dispensing.has(order.orderId) ? 'Dispensing…' : 'Dispense' }}
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </div>
  </section>
</template>
