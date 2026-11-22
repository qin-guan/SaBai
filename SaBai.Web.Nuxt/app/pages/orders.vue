<script setup lang="ts">
import {
  PhCheck,
  PhMagnifyingGlass,
  PhPencilSimple,
  PhTrash,
  PhWarning,
  PhX,
} from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { calculateAge } from '@/utils/patient'
import { formatVisitDate } from '@/utils/visit'
import type { MedicationOrder, PharmacyOrderItem } from '~/offline/mutations/medication'
import type { OrderSnapshot } from '~/offline/mutations/medication'

useHead({ title: "Orders & Dispensing · Sa'Bai" })

/**
 * The pharmacist's orders: the queue of pending orders by patient and visit,
 * each with the batches the server reserved for it (what to pick), to dispense,
 * change or cancel; and the log of every order. Dispensed and cancelled orders
 * are read-only.
 */

const route = useRoute()
const router = useRouter()
const { user } = useCurrentUser()
const { villageCodes } = useVillageCode()

const { data: pharmacyQueue, isPending: queuePending, isError: queueError, error: queueErr } = usePharmacyOrders()
const { data: allOrders, isPending: ordersPending, isError: ordersError, error: ordersErr } = useOrders()
const { data: availability } = useIngredientAvailability()
const { data: patientsData } = usePatientsWithLatestVisit()

const updateOrder = useUpdateOrder()
const deleteOrder = useDeleteOrder()

type Tab = 'queue' | 'ALL' | 'DISPENSED' | 'CANCELLED'
const activeTab = ref<Tab>('queue')
const search = ref('')
const villageId = ref('')
const lowStockOnly = ref(false)

const changing = ref<{ order: MedicationOrder, visitId?: string, patientName?: string } | null>(null)
const cancelling = ref<{ order: MedicationOrder, visitId?: string, patientName?: string } | null>(null)
const dispensing = ref(new Set<string>())

// Deleting an order is for the doctor roles (the API's DoctorWork).
const canDelete = computed(() => !!user.value?.roles.some(r => ['admin', 'member', 'doctor'].includes(r)))

// Sync deep link query params (?patient= or ?visit=)
const filterPatient = computed(() => route.query.patient ? String(route.query.patient) : null)
const filterVisit = computed(() => route.query.visit ? String(route.query.visit) : null)

// Lookups
const ordersById = computed(() => new Map((allOrders.value ?? []).map(o => [o.id, o])))
const unitByIngredient = computed(() => new Map((availability.value ?? []).map(a => [a.ingredientId, a.unitOfMeasurement])))
const unitOfOrder = (orderId: string) => {
  const order = ordersById.value.get(orderId)
  return order ? unitByIngredient.value.get(order.medicationActiveIngredientId) : undefined
}

type PatientRow = NonNullable<typeof patientsData.value>['patients'][number]
const patientsByDisplayIdOrName = computed(() => {
  const map = new Map<string, PatientRow>()
  for (const p of patientsData.value?.patients ?? []) {
    map.set(String(p.id), p)
    if (p.displayId) map.set(String(p.displayId), p)
    if (p.name) map.set(p.name.toLowerCase(), p)
  }
  return map
})

function getPatientForGroup(group: { patient: { displayId: string, name: string } }) {
  return patientsByDisplayIdOrName.value.get(String(group.patient.displayId))
    ?? patientsByDisplayIdOrName.value.get(group.patient.name.toLowerCase())
    ?? null
}

// Counts for status strip
const queuePatientsCount = computed(() => (pharmacyQueue.value ?? []).length)
const pendingOrdersCount = computed(() => (pharmacyQueue.value ?? []).reduce((n, g) => n + g.visits.reduce((m, v) => m + v.orders.length, 0), 0))
const allOrdersCount = computed(() => (allOrders.value ?? []).length)
const dispensedOrdersCount = computed(() => (allOrders.value ?? []).filter(o => o.status === 'DISPENSED').length)
const cancelledOrdersCount = computed(() => (allOrders.value ?? []).filter(o => o.status === 'CANCELLED').length)

// Filtered Pharmacy Queue
const filteredPharmacyQueue = computed(() => {
  let queue = pharmacyQueue.value ?? []

  if (filterPatient.value) {
    queue = queue.filter(g =>
      String(g.patient.displayId) === filterPatient.value
      || getPatientForGroup(g)?.id?.toString() === filterPatient.value
      || g.patient.name.toLowerCase().includes(filterPatient.value!.toLowerCase()),
    )
  }

  if (filterVisit.value) {
    queue = queue.map(g => ({
      ...g,
      visits: g.visits.filter(v => String(v.visitId) === filterVisit.value),
    })).filter(g => g.visits.length > 0)
  }

  const query = search.value.trim().toLowerCase()
  if (query) {
    queue = queue.map((g) => {
      const patientMatches = g.patient.name.toLowerCase().includes(query) || String(g.patient.displayId).toLowerCase().includes(query)
      const visits = g.visits.map((v) => {
        const diagnosesMatch = v.diagnoses.some(d => d.details.toLowerCase().includes(query) || (d.category && d.category.toLowerCase().includes(query)))
        const orders = v.orders.filter(o =>
          patientMatches
          || diagnosesMatch
          || o.medicationName.toLowerCase().includes(query)
          || o.dosageInstructions.toLowerCase().includes(query)
          || o.allocations.some(a => a.brandName.toLowerCase().includes(query) || a.location.toLowerCase().includes(query))
          || (ordersById.value.get(o.orderId)?.remarks?.toLowerCase() ?? '').includes(query),
        )
        return { ...v, orders }
      }).filter(v => v.orders.length > 0)
      return { ...g, visits }
    }).filter(g => g.visits.length > 0)
  }

  if (villageId.value) {
    queue = queue.filter(g => g.patient.villageCode === villageId.value)
  }

  if (lowStockOnly.value) {
    queue = queue.map(g => ({
      ...g,
      visits: g.visits.map(v => ({
        ...v,
        orders: v.orders.filter(o => o.isLowStock),
      })).filter(v => v.orders.length > 0),
    })).filter(g => g.visits.length > 0)
  }

  return queue
})

// Filtered Order Log
const filteredOrderLog = computed(() => {
  let orders = allOrders.value ?? []

  if (activeTab.value === 'DISPENSED') orders = orders.filter(o => o.status === 'DISPENSED')
  else if (activeTab.value === 'CANCELLED') orders = orders.filter(o => o.status === 'CANCELLED')

  const query = search.value.trim().toLowerCase()
  if (query) {
    orders = orders.filter(o =>
      o.activeIngredientName.toLowerCase().includes(query)
      || o.dosageInstructions.toLowerCase().includes(query)
      || (o.remarks && o.remarks.toLowerCase().includes(query))
      || o.allocations.some(a => a.brandName.toLowerCase().includes(query))
      || o.id.toLowerCase().includes(query),
    )
  }

  return orders
})

function clearFilters() {
  search.value = ''
  villageId.value = ''
  lowStockOnly.value = false
  if (filterPatient.value || filterVisit.value) {
    router.replace(paths.orders())
  }
}

/** What the optimistic edits need: the listed order, with the queue's (fresher) allocations. */
function snapshotOf(item: PharmacyOrderItem): OrderSnapshot {
  const full = ordersById.value.get(item.orderId)
  return {
    medicationActiveIngredientId: full?.medicationActiveIngredientId ?? '',
    ingredientName: item.medicationName,
    quantity: item.quantity,
    allocations: [...item.allocations],
  }
}

async function dispense(orderId: string, before: OrderSnapshot, visitId?: string, patientName?: string) {
  if (dispensing.value.has(orderId))
    return false
  dispensing.value.add(orderId)
  try {
    const outcome = await updateOrder.mutateAsync({
      id: orderId,
      changeId: uuidv7(),
      status: 'DISPENSED',
      before,
      visitId,
      patientName,
      dispensedById: user.value?.id ?? null,
      dispensedAt: new Date().toISOString(),
    })
    notifySaved(outcome, `Dispensed ${before.ingredientName} × ${before.quantity}${patientName ? ` for ${patientName}` : ''}.`)
    return true
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, `Failed to dispense ${before.ingredientName}.`))
    return false
  }
  finally {
    dispensing.value.delete(orderId)
  }
}

async function dispenseAll(visit: { visitId: string, orders: PharmacyOrderItem[] }, patientName: string) {
  for (const item of [...visit.orders]) {
    await dispense(item.orderId, snapshotOf(item), visit.visitId, patientName)
  }
}

function startChange(orderId: string, visitId?: string, patientName?: string) {
  const order = ordersById.value.get(orderId)
  if (order) changing.value = { order, visitId, patientName }
}

function startCancel(orderId: string, visitId?: string, patientName?: string) {
  const order = ordersById.value.get(orderId)
  if (order) cancelling.value = { order, visitId, patientName }
}

async function handleDelete(order: MedicationOrder) {
  try {
    const outcome = await deleteOrder.mutateAsync({
      id: order.id,
      before: { ...orderSnapshot(order), status: order.status },
    })
    notifySaved(outcome, 'Order deleted.')
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Failed to delete the order.'))
  }
}
</script>

<template>
  <div class="flex flex-col gap-5 pb-12">
    <PageHeader
      title="Pharmacist Orders & Dispensing"
      description="Pick each pending order from the batches reserved for it, then mark it dispensed."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Orders' }]"
    />

    <!-- Deep-link filter banner -->
    <div v-if="filterPatient || filterVisit" class="flex items-center justify-between rounded-lg border border-primary/30 bg-primary/5 px-4 py-3 text-sm">
      <div class="flex items-center gap-2 font-medium text-primary">
        <PhWarning class="size-5 shrink-0" />
        <span>Filtered to specific patient/visit context.</span>
      </div>
      <Button variant="outline" size="sm" @click="clearFilters">
        Show all orders
      </Button>
    </div>

    <!-- Status Strip / Tabs -->
    <div role="group" aria-label="Order views" class="grid grid-cols-2 gap-px border border-border bg-border sm:grid-cols-4 rounded-lg overflow-hidden shadow-xs">
      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'queue' }"
        :aria-pressed="activeTab === 'queue'"
        @click="activeTab = 'queue'"
      >
        <span class="text-xs font-medium text-muted-foreground">Dispensing Queue</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ pendingOrdersCount }}</span>
          <span class="text-xs text-muted-foreground">pending for {{ queuePatientsCount }} {{ queuePatientsCount === 1 ? 'patient' : 'patients' }}</span>
        </span>
      </button>

      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'ALL' }"
        :aria-pressed="activeTab === 'ALL'"
        @click="activeTab = 'ALL'"
      >
        <span class="text-xs font-medium text-muted-foreground">All Orders</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ allOrdersCount }}</span>
          <span class="text-xs text-muted-foreground">all prescriptions</span>
        </span>
      </button>

      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'DISPENSED' }"
        :aria-pressed="activeTab === 'DISPENSED'"
        @click="activeTab = 'DISPENSED'"
      >
        <span class="text-xs font-medium text-muted-foreground">Dispensed</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ dispensedOrdersCount }}</span>
          <span class="text-xs text-muted-foreground">handed out</span>
        </span>
      </button>

      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'CANCELLED' }"
        :aria-pressed="activeTab === 'CANCELLED'"
        @click="activeTab = 'CANCELLED'"
      >
        <span class="text-xs font-medium text-muted-foreground">Cancelled</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ cancelledOrdersCount }}</span>
          <span class="text-xs text-muted-foreground">cancelled orders</span>
        </span>
      </button>
    </div>

    <!-- Search & Filters Bar -->
    <div class="flex flex-wrap items-center gap-3 bg-card p-4 rounded-lg border border-border shadow-xs">
      <div class="relative flex-1 min-w-[200px]">
        <PhMagnifyingGlass class="absolute left-3 top-1/2 size-4 -translate-y-1/2 text-muted-foreground" />
        <Input
          v-model="search"
          aria-label="Search orders"
          placeholder="Search patient, ID, medication, brand or instructions..."
          class="pl-9 pr-9"
        />
        <button
          v-if="search"
          type="button"
          aria-label="Clear search"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          @click="search = ''"
        >
          <PhX class="size-4" />
        </button>
      </div>

      <div class="w-48">
        <NativeSelect v-model="villageId" aria-label="Village">
          <NativeSelectOption value="">
            All Villages
          </NativeSelectOption>
          <NativeSelectOption v-for="v in villageCodes" :key="v.code" :value="v.code">
            {{ v.name }} ({{ v.code }})
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <label class="flex items-center gap-2 text-sm font-medium cursor-pointer select-none">
        <input v-model="lowStockOnly" type="checkbox" class="rounded border-input text-primary focus:ring-ring size-4">
        Low stock items only
      </label>

      <Button v-if="search || villageId || lowStockOnly" variant="ghost" size="sm" @click="clearFilters">
        Reset filters
      </Button>
    </div>

    <!-- View 1: Dispensing Queue -->
    <div v-if="activeTab === 'queue'" class="flex flex-col gap-6">
      <StateMessage v-if="queuePending" kind="loading" message="Loading pharmacy dispensing queue..." />
      <StateMessage v-else-if="queueError" kind="error" :message="describeLoadError(queueErr, 'Failed to load pharmacy orders.')" />
      <StateMessage v-else-if="filteredPharmacyQueue.length === 0" message="No pending orders in the dispensing queue." />

      <div v-for="group in filteredPharmacyQueue" :key="group.patient.displayId" class="rounded-xl border border-border bg-card overflow-hidden shadow-xs">
        <!-- Patient Header -->
        <div class="flex flex-wrap items-center justify-between gap-4 border-b border-border bg-muted/30 px-5 py-4">
          <div class="flex items-center gap-3">
            <span class="flex size-11 shrink-0 overflow-hidden rounded-full bg-background border border-border">
              <PatientPhoto
                v-if="getPatientForGroup(group)"
                :patient-id="getPatientForGroup(group)!.id"
                :has-photo="getPatientForGroup(group)!.hasPhoto"
                :size="44"
                alt=""
              />
            </span>
            <div class="flex flex-col">
              <div class="flex items-center gap-2">
                <NuxtLink
                  v-if="getPatientForGroup(group)"
                  :to="paths.patientDetail(getPatientForGroup(group)!.id)"
                  class="font-semibold text-foreground hover:underline text-base"
                >
                  {{ group.patient.name }}
                </NuxtLink>
                <span v-else class="font-semibold text-foreground text-base">{{ group.patient.name }}</span>

                <PatientCode
                  :id="group.patient.displayId"
                  :village-code="group.patient.villageCode"
                  :display-id="group.patient.displayId"
                />
              </div>

              <div class="flex items-center gap-3 text-xs text-muted-foreground mt-0.5">
                <span v-if="getPatientForGroup(group)?.dateOfBirth">
                  {{ calculateAge(getPatientForGroup(group)!.dateOfBirth) }} yrs
                  ({{ getPatientForGroup(group)!.gender === 'female' ? 'F' : 'M' }})
                </span>
              </div>
            </div>
          </div>

          <!-- Allergy Flag -->
          <div>
            <AllergyFlag :allergy="getPatientForGroup(group)?.drugAllergy" />
          </div>
        </div>

        <!-- Visit Bundles -->
        <div class="flex flex-col divide-y divide-border">
          <div v-for="visit in group.visits" :key="visit.visitId" class="p-5 flex flex-col gap-4">
            <!-- Visit Subheader -->
            <div class="flex flex-wrap items-center justify-between gap-3 bg-muted/20 p-3 rounded-lg border border-border/50">
              <div class="flex items-center gap-3">
                <span class="text-sm font-semibold text-foreground">Visit: {{ formatVisitDate(visit.visitDate) }}</span>
                <Button
                  v-if="getPatientForGroup(group)"
                  variant="link"
                  size="sm"
                  class="h-auto p-0 text-xs"
                  as-child
                >
                  <NuxtLink :to="paths.consultsDetail(getPatientForGroup(group)!.id, visit.visitId)">
                    View Consult →
                  </NuxtLink>
                </Button>
              </div>

              <Button
                v-if="visit.orders.length >= 2"
                variant="success"
                size="sm"
                @click="dispenseAll(visit, group.patient.name)"
              >
                <PhCheck class="mr-1.5 size-4" /> Dispense All ({{ visit.orders.length }})
              </Button>
            </div>

            <!-- Diagnoses Strip -->
            <div v-if="visit.diagnoses.length > 0" class="flex flex-wrap gap-2 items-center text-xs">
              <span class="font-medium text-muted-foreground">Diagnoses:</span>
              <span v-for="(diag, idx) in visit.diagnoses" :key="idx" class="inline-flex items-center gap-1 rounded bg-secondary px-2 py-1 text-secondary-foreground">
                <span v-if="diag.category" class="font-semibold uppercase tracking-wider text-[10px] opacity-75">[{{ diag.category }}]</span>
                <span>{{ diag.details }}</span>
              </span>
            </div>

            <!-- Orders -->
            <div class="rounded-lg border border-border overflow-hidden">
              <Table class="grid-table stack-table">
                <TableHeader>
                  <TableRow>
                    <TableHead>Medication</TableHead>
                    <TableHead class="text-right">
                      Quantity
                    </TableHead>
                    <TableHead>Pick from</TableHead>
                    <TableHead>Dosage Instructions & Remarks</TableHead>
                    <TableHead class="text-right">
                      Actions
                    </TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  <TableRow v-for="order in visit.orders" :key="order.orderId" :data-order-id="order.orderId">
                    <TableCell data-stack="title" class="font-medium">
                      <div class="flex items-center gap-2">
                        <span>{{ order.medicationName }}</span>
                        <Badge v-if="order.isLowStock" variant="outline" class="border-warning/40 bg-warning-subtle text-warning text-[10px]">
                          Low Stock
                        </Badge>
                      </div>
                    </TableCell>

                    <TableCell data-label="Quantity" class="text-right font-semibold tabular-nums">
                      × {{ order.quantity }}{{ formatQuantityUnit(unitOfOrder(order.orderId)) }}
                    </TableCell>

                    <TableCell data-label="Pick from" class="min-w-56">
                      <OrderAllocations :allocations="order.allocations" :unit="unitOfOrder(order.orderId)" pending />
                    </TableCell>

                    <TableCell data-label="Dosage">
                      <div class="flex flex-col">
                        <span class="text-sm text-foreground">{{ order.dosageInstructions }}</span>
                        <span v-if="ordersById.get(order.orderId)?.remarks" class="text-xs text-muted-foreground italic">
                          Remarks: {{ ordersById.get(order.orderId)?.remarks }}
                        </span>
                      </div>
                    </TableCell>

                    <TableCell data-stack="actions" class="text-right">
                      <div class="flex flex-wrap items-center justify-end gap-1.5">
                        <Button
                          variant="ghost"
                          size="sm"
                          :disabled="!ordersById.get(order.orderId)"
                          :aria-label="`Change order ${order.medicationName} for ${group.patient.name}`"
                          @click="startChange(order.orderId, visit.visitId, group.patient.name)"
                        >
                          <PhPencilSimple aria-hidden="true" /> Change
                        </Button>

                        <Button
                          variant="ghost"
                          size="sm"
                          class="text-destructive hover:text-destructive"
                          :disabled="!ordersById.get(order.orderId)"
                          :aria-label="`Cancel order ${order.medicationName} for ${group.patient.name}`"
                          @click="startCancel(order.orderId, visit.visitId, group.patient.name)"
                        >
                          <PhX aria-hidden="true" /> Cancel
                        </Button>

                        <Button
                          variant="success"
                          size="sm"
                          :disabled="dispensing.has(order.orderId)"
                          :aria-label="`Mark ${order.medicationName} dispensed for ${group.patient.name}`"
                          @click="dispense(order.orderId, snapshotOf(order), visit.visitId, group.patient.name)"
                        >
                          <PhCheck aria-hidden="true" /> Mark dispensed
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                </TableBody>
              </Table>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- View 2: Order Log (ALL, DISPENSED, CANCELLED) -->
    <div v-else class="flex flex-col gap-4">
      <StateMessage v-if="ordersPending" kind="loading" message="Loading orders..." />
      <StateMessage v-else-if="ordersError" kind="error" :message="describeLoadError(ordersErr, 'Failed to load orders log.')" />
      <StateMessage v-else-if="filteredOrderLog.length === 0" message="No orders found." />

      <AppPanel v-else flush>
        <Table class="grid-table stack-table">
          <TableHeader>
            <TableRow>
              <TableHead>Medication</TableHead>
              <TableHead class="text-right">
                Quantity
              </TableHead>
              <TableHead>Batches</TableHead>
              <TableHead>Dosage Instructions</TableHead>
              <TableHead>Remarks</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Date Created</TableHead>
              <TableHead class="text-right">
                Actions
              </TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="order in filteredOrderLog" :key="order.id" :data-order-id="order.id">
              <TableCell data-stack="title" class="font-medium">
                {{ order.activeIngredientName }}
              </TableCell>
              <TableCell data-label="Quantity" class="text-right tabular-nums">
                × {{ order.quantity }}{{ formatQuantityUnit(unitByIngredient.get(order.medicationActiveIngredientId)) }}
              </TableCell>
              <TableCell data-label="Batches" class="min-w-48">
                <OrderAllocations :allocations="order.allocations" :unit="unitByIngredient.get(order.medicationActiveIngredientId)" :pending="order.status === 'PENDING'" />
              </TableCell>
              <TableCell data-label="Dosage" class="text-sm">
                {{ order.dosageInstructions }}
              </TableCell>
              <TableCell data-label="Remarks" class="text-xs text-muted-foreground">
                {{ order.remarks || '—' }}
              </TableCell>
              <TableCell data-label="Status">
                <OrderStatusBadge :status="order.status" />
                <p v-if="order.status === 'DISPENSED' && order.dispensedAt" class="mt-0.5 text-xs text-muted-foreground">
                  {{ formatVisitDate(order.dispensedAt) }}
                </p>
              </TableCell>
              <TableCell data-label="Created" class="text-xs text-muted-foreground">
                {{ formatVisitDate(order.createdAt) }}
              </TableCell>
              <TableCell data-stack="actions" class="text-right">
                <div class="flex flex-wrap items-center justify-end gap-1.5">
                  <template v-if="order.status === 'PENDING'">
                    <Button variant="ghost" size="sm" :aria-label="`Change order ${order.activeIngredientName}`" @click="startChange(order.id)">
                      <PhPencilSimple aria-hidden="true" /> Change
                    </Button>
                    <Button
                      variant="ghost"
                      size="sm"
                      class="text-destructive hover:text-destructive"
                      :aria-label="`Cancel order ${order.activeIngredientName}`"
                      @click="startCancel(order.id)"
                    >
                      <PhX aria-hidden="true" /> Cancel
                    </Button>
                    <Button
                      variant="success"
                      size="sm"
                      :disabled="dispensing.has(order.id)"
                      :aria-label="`Mark ${order.activeIngredientName} dispensed`"
                      @click="dispense(order.id, orderSnapshot(order))"
                    >
                      <PhCheck aria-hidden="true" /> Mark dispensed
                    </Button>
                  </template>
                  <Button
                    v-else-if="order.status === 'CANCELLED' && canDelete"
                    variant="ghost"
                    size="icon"
                    class="text-destructive hover:text-destructive"
                    :aria-label="`Delete cancelled order ${order.activeIngredientName}`"
                    @click="handleDelete(order)"
                  >
                    <PhTrash class="size-4" />
                  </Button>
                </div>
              </TableCell>
            </TableRow>
          </TableBody>
        </Table>
      </AppPanel>
    </div>

    <OrderDialog
      v-if="changing"
      :order="changing.order"
      :visit-id="changing.visitId"
      :patient-name="changing.patientName"
      @close="changing = null"
    />
    <CancelOrderDialog
      v-if="cancelling"
      :order="cancelling.order"
      :unit="unitByIngredient.get(cancelling.order.medicationActiveIngredientId)"
      :visit-id="cancelling.visitId"
      :patient-name="cancelling.patientName"
      @close="cancelling = null"
    />
  </div>
</template>
