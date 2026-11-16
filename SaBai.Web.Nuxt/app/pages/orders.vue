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

useHead({ title: "Orders & Dispensing · Sa'Bai" })

const route = useRoute()
const router = useRouter()
const { user } = useCurrentUser()
const { villageCodes } = useVillageCode()

const { data: pharmacyQueue, isPending: queuePending, isError: queueError, error: queueErr } = usePharmacyOrders()
const { data: allOrders, isPending: ordersPending, isError: ordersError, error: ordersErr } = useOrders()
const { data: stock } = useMedicationStock()
const { data: patientsData } = usePatientsWithLatestVisit()

const updateOrder = useUpdateOrder()
const deleteOrder = useDeleteOrder()

const activeTab = ref<'queue' | 'ALL' | 'APPROVED' | 'CANCELLED'>('queue')
const search = ref('')
const villageId = ref('')
const lowStockOnly = ref(false)

// Inline editing state for orders: orderId -> { dosageInstructions, remarks }
const editingOrder = ref<Record<string, { dosageInstructions: string; remarks: string }>>({})

// Sync deep link query params (?patient= or ?visit=)
const filterPatient = computed(() => route.query.patient ? String(route.query.patient) : null)
const filterVisit = computed(() => route.query.visit ? String(route.query.visit) : null)

// Lookups
const ordersById = computed(() => new Map((allOrders.value ?? []).map(o => [o.id, o])))

const stockByBrandId = computed(() => {
  const map = new Map<string, { totalQuantity: number; lots: typeof stock.value }>()
  for (const item of (stock.value ?? [])) {
    if (item.stockStatus === 'active' && item.quantity > 0) {
      const existing = map.get(item.medicationBrandId) || { totalQuantity: 0, lots: [] }
      existing.totalQuantity += item.quantity
      existing.lots = existing.lots || []
      existing.lots.push(item)
      map.set(item.medicationBrandId, existing)
    }
  }
  return map
})

const patientsByDisplayIdOrId = computed(() => {
  const map = new Map<string, any>()
  const list = patientsData.value?.patients ?? []
  for (const p of list) {
    map.set(String(p.id), p)
    if (p.displayId) map.set(String(p.displayId), p)
    if (p.name) map.set(p.name.toLowerCase(), p)
  }
  return map
})

function getPatientForGroup(group: { patient: { displayId: string; name: string; villageCode: string | null } }) {
  return patientsByDisplayIdOrId.value.get(String(group.patient.displayId)) ||
         patientsByDisplayIdOrId.value.get(group.patient.name.toLowerCase()) ||
         null
}

// Counts for status strip
const queuePatientsCount = computed(() => (pharmacyQueue.value ?? []).length)
const allOrdersCount = computed(() => (allOrders.value ?? []).length)
const approvedOrdersCount = computed(() => (allOrders.value ?? []).filter(o => o.status === 'APPROVED').length)
const cancelledOrdersCount = computed(() => (allOrders.value ?? []).filter(o => o.status === 'CANCELLED').length)

// Filtered Pharmacy Queue
const filteredPharmacyQueue = computed(() => {
  let queue = pharmacyQueue.value ?? []

  if (filterPatient.value) {
    queue = queue.filter(g =>
      String(g.patient.displayId) === filterPatient.value ||
      getPatientForGroup(g)?.id?.toString() === filterPatient.value ||
      g.patient.name.toLowerCase().includes(filterPatient.value!.toLowerCase())
    )
  }

  if (filterVisit.value) {
    queue = queue.map(g => ({
      ...g,
      visits: g.visits.filter(v => String(v.visitId) === filterVisit.value)
    })).filter(g => g.visits.length > 0)
  }

  const query = search.value.trim().toLowerCase()
  if (query) {
    queue = queue.map(g => {
      const patientMatches = g.patient.name.toLowerCase().includes(query) || String(g.patient.displayId).toLowerCase().includes(query)
      const visits = g.visits.map(v => {
        const diagnosesMatch = v.diagnoses.some(d => d.details.toLowerCase().includes(query) || (d.category && d.category.toLowerCase().includes(query)))
        const orders = v.orders.filter(o =>
          patientMatches ||
          diagnosesMatch ||
          o.medicationName.toLowerCase().includes(query) ||
          o.dosageInstructions.toLowerCase().includes(query) ||
          (ordersById.value.get(o.orderId)?.remarks?.toLowerCase() ?? '').includes(query)
        )
        return { ...v, orders }
      }).filter(v => patientMatches || v.orders.length > 0 || v.diagnoses.some(d => d.details.toLowerCase().includes(query)))
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
        orders: v.orders.filter(o => o.isLowStock)
      })).filter(v => v.orders.length > 0)
    })).filter(g => g.visits.length > 0)
  }

  return queue
})

// Filtered Order Log
const filteredOrderLog = computed(() => {
  let orders = allOrders.value ?? []

  if (activeTab.value === 'APPROVED') orders = orders.filter(o => o.status === 'APPROVED')
  else if (activeTab.value === 'CANCELLED') orders = orders.filter(o => o.status === 'CANCELLED')

  const query = search.value.trim().toLowerCase()
  if (query) {
    orders = orders.filter(o =>
      o.brandName.toLowerCase().includes(query) ||
      o.dosageInstructions.toLowerCase().includes(query) ||
      (o.remarks && o.remarks.toLowerCase().includes(query)) ||
      o.id.toLowerCase().includes(query)
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

function getStockInfo(medicationBrandId?: string, brandName?: string) {
  if (!medicationBrandId && !brandName) return { available: 0, lots: [] }
  for (const [id, info] of stockByBrandId.value.entries()) {
    if (id === medicationBrandId) return { available: info.totalQuantity, lots: info.lots }
  }
  // Fallback match by brand name
  for (const info of stockByBrandId.value.values()) {
    if (info.lots?.some(l => l.brandName?.toLowerCase() === brandName?.toLowerCase())) {
      return { available: info.totalQuantity, lots: info.lots }
    }
  }
  return { available: 0, lots: [] }
}

async function handleApprove(orderItem: any, visitId: string, patientName: string) {
  const fullOrder = ordersById.value.get(orderItem.orderId)
  const stockInfo = getStockInfo(fullOrder?.medicationBrandId, orderItem.medicationName)

  try {
    await updateOrder.mutateAsync({
      id: orderItem.orderId,
      status: 'APPROVED',
      dosageInstructions: editingOrder.value[orderItem.orderId]?.dosageInstructions ?? orderItem.dosageInstructions,
      remarks: editingOrder.value[orderItem.orderId]?.remarks ?? fullOrder?.remarks ?? null,
      medicationBrandId: fullOrder?.medicationBrandId,
      brandName: orderItem.medicationName,
      quantity: orderItem.quantity,
      visitId,
      patientName,
      approvedById: user.value?.id ?? null,
      approvedAt: new Date().toISOString(),
      stockSnapshot: stockInfo.lots as any,
    })
    toast.success(`Dispensed ${orderItem.medicationName} for ${patientName}`)
    delete editingOrder.value[orderItem.orderId]
  } catch (err: any) {
    toast.error(getApiErrorMessage(err, 'Failed to dispense order'))
  }
}

async function handleDispenseAll(visit: any, group: any) {
  for (const orderItem of visit.orders) {
    await handleApprove(orderItem, visit.visitId, group.patient.name)
  }
}

async function handleCancel(orderId: string, patientName: string) {
  try {
    await updateOrder.mutateAsync({
      id: orderId,
      status: 'CANCELLED',
    })
    toast.success('Order cancelled')
  } catch (err: any) {
    toast.error(getApiErrorMessage(err, 'Failed to cancel order'))
  }
}

async function handleDelete(orderId: string) {
  try {
    await deleteOrder.mutateAsync({ id: orderId })
    toast.success('Order deleted')
  } catch (err: any) {
    toast.error(getApiErrorMessage(err, 'Failed to delete order'))
  }
}

function startEditing(orderId: string, currentDosage: string, currentRemarks: string | null) {
  editingOrder.value[orderId] = {
    dosageInstructions: currentDosage,
    remarks: currentRemarks ?? '',
  }
}

async function saveInlineEdit(order: any) {
  const edit = editingOrder.value[order.orderId]
  if (!edit) return
  const fullOrder = ordersById.value.get(order.orderId)
  try {
    await updateOrder.mutateAsync({
      id: order.orderId,
      status: 'PENDING',
      dosageInstructions: edit.dosageInstructions,
      remarks: edit.remarks || null,
      brandName: order.medicationName,
      medicationBrandId: fullOrder?.medicationBrandId,
      quantity: order.quantity,
    })
    toast.success('Updated order instructions')
    delete editingOrder.value[order.orderId]
  } catch (err: any) {
    toast.error(getApiErrorMessage(err, 'Failed to update order'))
  }
}
</script>

<template>
  <div class="flex flex-col gap-5 pb-12">
    <PageHeader
      title="Pharmacist Orders & Dispensing"
      description="Manage and dispense patient medication orders."
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
        @click="activeTab = 'queue'"
      >
        <span class="text-xs font-medium text-muted-foreground">Dispensing Queue</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ queuePatientsCount }}</span>
          <span class="text-xs text-muted-foreground">patients waiting</span>
        </span>
      </button>

      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'ALL' }"
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
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'APPROVED' }"
        @click="activeTab = 'APPROVED'"
      >
        <span class="text-xs font-medium text-muted-foreground">Dispensed</span>
        <span class="mt-1 flex items-baseline gap-2">
          <span class="text-xl font-bold tracking-tight">{{ approvedOrdersCount }}</span>
          <span class="text-xs text-muted-foreground">approved & dispensed</span>
        </span>
      </button>

      <button
        type="button"
        class="flex flex-col items-start bg-card p-3.5 text-left transition hover:bg-muted/50 focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
        :class="{ 'bg-muted ring-2 ring-primary inset-0 z-10': activeTab === 'CANCELLED' }"
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
          placeholder="Search patient, ID, brand, or instructions..."
          class="pl-9 pr-9"
        />
        <button
          v-if="search"
          type="button"
          class="absolute right-3 top-1/2 -translate-y-1/2 text-muted-foreground hover:text-foreground"
          @click="search = ''"
        >
          <PhX class="size-4" />
        </button>
      </div>

      <div class="w-48">
        <NativeSelect v-model="villageId">
          <NativeSelectOption value="">All Villages</NativeSelectOption>
          <NativeSelectOption v-for="v in villageCodes" :key="v.code" :value="v.code">
            {{ v.name }} ({{ v.code }})
          </NativeSelectOption>
        </NativeSelect>
      </div>

      <label class="flex items-center gap-2 text-sm font-medium cursor-pointer select-none">
        <input v-model="lowStockOnly" type="checkbox" class="rounded border-input text-primary focus:ring-ring size-4" />
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
                :patient-id="getPatientForGroup(group).id"
                :has-photo="getPatientForGroup(group).hasPhoto"
                :size="44"
                alt=""
              />
            </span>
            <div class="flex flex-col">
              <div class="flex items-center gap-2">
                <NuxtLink
                  v-if="getPatientForGroup(group)"
                  :to="paths.patientDetail(getPatientForGroup(group).id)"
                  class="font-semibold text-foreground hover:underline text-base"
                >
                  {{ group.patient.name }}
                </NuxtLink>
                <span v-else class="font-semibold text-foreground text-base">{{ group.patient.name }}</span>

                <PatientCode
                  :village-code="group.patient.villageCode"
                  :id="group.patient.displayId"
                  :display-id="group.patient.displayId"
                />
              </div>

              <div class="flex items-center gap-3 text-xs text-muted-foreground mt-0.5">
                <span v-if="getPatientForGroup(group)?.dateOfBirth">
                  {{ calculateAge(getPatientForGroup(group).dateOfBirth) }} yrs
                  ({{ getPatientForGroup(group).gender === 'Female' ? 'F' : getPatientForGroup(group).gender === 'Male' ? 'M' : getPatientForGroup(group).gender }})
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
                  <NuxtLink :to="paths.consultsDetail(getPatientForGroup(group).id, visit.visitId)">
                    View Consult →
                  </NuxtLink>
                </Button>
              </div>

              <Button
                v-if="visit.orders.length >= 2"
                variant="success"
                size="sm"
                @click="handleDispenseAll(visit, group)"
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

            <!-- Orders Table / List -->
            <div class="rounded-lg border border-border overflow-hidden">
              <Table class="grid-table stack-table">
                <TableHeader>
                  <TableRow>
                    <TableHead>Medication</TableHead>
                    <TableHead class="text-right">Quantity</TableHead>
                    <TableHead>Dosage Instructions & Remarks</TableHead>
                    <TableHead class="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  <TableRow v-for="order in visit.orders" :key="order.orderId">
                    <!-- Medication -->
                    <TableCell class="font-medium">
                      <div class="flex items-center gap-2">
                        <span>{{ order.medicationName }}</span>
                        <Badge v-if="order.isLowStock" variant="outline" class="border-warning/40 bg-warning-subtle text-warning text-[10px]">Low Stock</Badge>
                        <span class="text-xs text-muted-foreground">
                          ({{ getStockInfo(ordersById.get(order.orderId)?.medicationBrandId, order.medicationName).available }} in stock)
                        </span>
                      </div>
                    </TableCell>

                    <!-- Quantity -->
                    <TableCell class="text-right font-semibold">
                      <span :class="{ 'text-destructive': getStockInfo(ordersById.get(order.orderId)?.medicationBrandId, order.medicationName).available < order.quantity }">
                        × {{ order.quantity }}
                      </span>
                    </TableCell>

                    <!-- Instructions & Remarks / Inline Edit -->
                    <TableCell>
                      <div v-if="editingOrder[order.orderId]" class="flex flex-col gap-2 py-1">
                        <Input
                          v-model="editingOrder[order.orderId]!.dosageInstructions"
                          placeholder="Dosage instructions..."
                          class="h-8 text-xs"
                        />
                        <Input
                          v-model="editingOrder[order.orderId]!.remarks"
                          placeholder="Remarks..."
                          class="h-8 text-xs"
                        />
                        <div class="flex items-center gap-2">
                          <Button size="sm" variant="default" @click="saveInlineEdit(order)">
                            Save
                          </Button>
                          <Button size="sm" variant="outline" @click="delete editingOrder[order.orderId]">
                            Cancel
                          </Button>
                        </div>
                      </div>
                      <div v-else class="flex flex-col">
                        <span class="text-sm text-foreground">{{ order.dosageInstructions }}</span>
                        <span v-if="ordersById.get(order.orderId)?.remarks" class="text-xs text-muted-foreground italic">
                          Remarks: {{ ordersById.get(order.orderId)?.remarks }}
                        </span>
                      </div>
                    </TableCell>

                    <!-- Actions -->
                    <TableCell class="text-right">
                      <div class="flex items-center justify-end gap-1.5">
                        <Button
                          v-if="!editingOrder[order.orderId]"
                          variant="ghost"
                          size="icon"
                          title="Edit instructions"
                          @click="startEditing(order.orderId, order.dosageInstructions, ordersById.get(order.orderId)?.remarks ?? null)"
                        >
                          <PhPencilSimple class="size-4" />
                        </Button>

                        <Button
                          variant="ghost"
                          size="icon"
                          class="text-destructive hover:text-destructive"
                          title="Cancel order"
                          @click="handleCancel(order.orderId, group.patient.name)"
                        >
                          <PhX class="size-4" />
                        </Button>

                        <Button
                          variant="success"
                          size="sm"
                          :disabled="getStockInfo(ordersById.get(order.orderId)?.medicationBrandId, order.medicationName).available < order.quantity"
                          :title="getStockInfo(ordersById.get(order.orderId)?.medicationBrandId, order.medicationName).available < order.quantity ? 'Insufficient stock' : 'Dispense medication'"
                          @click="handleApprove(order, visit.visitId, group.patient.name)"
                        >
                          <PhCheck class="mr-1 size-3.5" /> Dispense
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

    <!-- View 2: Order Log (ALL, APPROVED, CANCELLED) -->
    <div v-else class="flex flex-col gap-4">
      <StateMessage v-if="ordersPending" kind="loading" message="Loading orders..." />
      <StateMessage v-else-if="ordersError" kind="error" :message="describeLoadError(ordersErr, 'Failed to load orders log.')" />
      <StateMessage v-else-if="filteredOrderLog.length === 0" message="No orders found." />

      <AppPanel v-else flush>
        <Table class="grid-table stack-table">
          <TableHeader>
            <TableRow>
              <TableHead>Medication Brand</TableHead>
              <TableHead class="text-right">Quantity</TableHead>
              <TableHead>Dosage Instructions</TableHead>
              <TableHead>Remarks</TableHead>
              <TableHead>Status</TableHead>
              <TableHead>Date Created</TableHead>
              <TableHead class="text-right">Actions</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            <TableRow v-for="order in filteredOrderLog" :key="order.id">
              <TableCell class="font-medium">{{ order.brandName }}</TableCell>
              <TableCell class="text-right">× {{ order.quantity }}</TableCell>
              <TableCell class="text-sm">{{ order.dosageInstructions }}</TableCell>
              <TableCell class="text-xs text-muted-foreground">{{ order.remarks || '—' }}</TableCell>
              <TableCell>
                <Badge
                  variant="outline"
                  :class="order.status === 'APPROVED' ? 'border-success/40 bg-success-subtle text-success' : order.status === 'CANCELLED' ? 'border-destructive/40 bg-destructive-subtle text-destructive' : 'border-warning/40 bg-warning-subtle text-warning'"
                >
                  {{ order.status === 'APPROVED' ? 'Dispensed' : order.status === 'CANCELLED' ? 'Cancelled' : 'Pending' }}
                </Badge>
              </TableCell>
              <TableCell class="text-xs text-muted-foreground">{{ formatVisitDate(order.createdAt) }}</TableCell>
              <TableCell class="text-right">
                <div class="flex items-center justify-end gap-1.5">
                  <Button
                    v-if="order.status === 'PENDING'"
                    variant="success"
                    size="sm"
                    @click="handleApprove({ orderId: order.id, medicationName: order.brandName, quantity: order.quantity, dosageInstructions: order.dosageInstructions }, order.consultId, 'Patient')"
                  >
                    <PhCheck class="mr-1 size-3.5" /> Dispense
                  </Button>
                  <Button
                    v-if="order.status === 'PENDING'"
                    variant="ghost"
                    size="sm"
                    class="text-destructive hover:text-destructive"
                    @click="handleCancel(order.id, 'Patient')"
                  >
                    Cancel
                  </Button>
                  <Button
                    v-if="order.status === 'CANCELLED'"
                    variant="ghost"
                    size="icon"
                    class="text-destructive hover:text-destructive"
                    title="Delete order"
                    @click="handleDelete(order.id)"
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
  </div>
</template>
