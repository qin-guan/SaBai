<script setup lang="ts">
import { PhFunnel, PhMagnifyingGlass, PhX } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { NativeSelect, NativeSelectOption } from '@/components/ui/native-select'
import type { MedicationOrder, OrderSnapshot, PharmacyOrderItem, PharmacyVisitBundle } from '~/offline/mutations/medication'

useHead({ title: "Orders & Dispensing · Sa'Bai" })

/**
 * The pharmacist's orders. First the queue: pending orders by patient and
 * visit, each with the batches it will be dispensed from (the system's, unless
 * the pharmacist chose others with "Change batch"), to dispense, change or
 * cancel. Then the history: dispensed, cancelled, or every order.
 *
 * The page owns the data and the writes; `OrderQueueCard` and
 * `OrderHistoryTable` show them and report what was asked.
 */

const route = useRoute()
const router = useRouter()
const { user } = useCurrentUser()
const { villageCodes } = useVillageCode()

const { data: pharmacyQueue, isPending: queuePending, isError: queueError, error: queueErr } = usePharmacyOrders()
const { data: allOrders, isPending: ordersPending, isError: ordersError, error: ordersErr } = useOrders()
const { data: patientsData } = usePatientsWithLatestVisit()

const updateOrder = useUpdateOrder()
const deleteOrder = useDeleteOrder()

type View = 'queue' | 'dispensed' | 'cancelled' | 'all'
const VIEWS: View[] = ['queue', 'dispensed', 'cancelled', 'all']
/** The view lives in the URL (`?view=dispensed`), so a reload or a shared link keeps it. */
const view = computed<View>({
  get: () => (VIEWS.includes(route.query.view as View) ? (route.query.view as View) : 'queue'),
  set: (value) => {
    const { view: _view, ...query } = route.query
    router.replace({ query: value === 'queue' ? query : { ...query, view: value } })
  },
})

const search = ref('')
const villageCode = ref('')
const lowStockOnly = ref(false)

interface OrderContext { order: MedicationOrder, visitId?: string, patientName?: string }
const changing = ref<OrderContext | null>(null)
const cancelling = ref<OrderContext | null>(null)
const rebatching = ref<{
  order: { orderId: string, medicationName: string, quantity: number, unit?: string }
  before: OrderSnapshot
  visitId?: string
  patientName?: string
} | null>(null)
const dispensing = ref(new Set<string>())

// Deleting an order is for the doctor roles (the API's DoctorWork).
const canDelete = computed(() => !!user.value?.roles.some(r => ['admin', 'member', 'doctor'].includes(r)))

// Deep link from a patient or visit (?patient= or ?visit=).
const filterPatient = computed(() => (route.query.patient ? String(route.query.patient) : null))
const filterVisit = computed(() => (route.query.visit ? String(route.query.visit) : null))

// Lookups
const ordersById = computed(() => new Map((allOrders.value ?? []).map(o => [o.id, o])))

type PatientRow = NonNullable<typeof patientsData.value>['patients'][number]
const patientsByDisplayId = computed(() => {
  const map = new Map<string, PatientRow>()
  for (const p of patientsData.value?.patients ?? []) {
    if (p.displayId)
      map.set(String(p.displayId), p)
  }
  return map
})
const patientOf = (displayId: string) => patientsByDisplayId.value.get(String(displayId)) ?? null

/** Who each pending order is for (the order list itself does not say). */
const patientNameByOrder = computed(() => {
  const names = new Map<string, string>()
  for (const group of pharmacyQueue.value ?? []) {
    for (const visit of group.visits) {
      for (const order of visit.orders) names.set(order.orderId, group.patient.name)
    }
  }
  return names
})
const visitByOrder = computed(() => {
  const visits = new Map<string, string>()
  for (const group of pharmacyQueue.value ?? []) {
    for (const visit of group.visits) {
      for (const order of visit.orders) visits.set(order.orderId, visit.visitId)
    }
  }
  return visits
})

const queueCounts = computed(() => countPharmacyQueue(pharmacyQueue.value ?? []))
const countByStatus = (status: string) => (allOrders.value ?? []).filter(o => o.status === status).length

const cells = computed<{ key: View, label: string, count: number | null, detail: string }[]>(() => [
  {
    key: 'queue',
    label: 'To dispense',
    count: pharmacyQueue.value ? queueCounts.value.orders : null,
    detail: pharmacyQueue.value
      ? `for ${queueCounts.value.patients} ${queueCounts.value.patients === 1 ? 'patient' : 'patients'}`
      : 'pending orders',
  },
  { key: 'dispensed', label: 'Dispensed', count: allOrders.value ? countByStatus('DISPENSED') : null, detail: 'handed out' },
  { key: 'cancelled', label: 'Cancelled', count: allOrders.value ? countByStatus('CANCELLED') : null, detail: 'not dispensed' },
  { key: 'all', label: 'All orders', count: allOrders.value ? allOrders.value.length : null, detail: 'every status' },
])

const filteredQueue = computed(() => filterPharmacyQueue(pharmacyQueue.value ?? [], {
  query: search.value,
  villageCode: villageCode.value,
  lowStockOnly: lowStockOnly.value,
  visitId: filterVisit.value,
  patient: filterPatient.value,
}, {
  remarksOf: id => ordersById.value.get(id)?.remarks,
  patientIdOf: displayId => patientOf(displayId)?.id?.toString(),
}))
const shownQueue = computed(() => countPharmacyQueue(filteredQueue.value))

const filteredHistory = computed(() => {
  let orders = allOrders.value ?? []
  if (view.value === 'dispensed') orders = orders.filter(o => o.status === 'DISPENSED')
  else if (view.value === 'cancelled') orders = orders.filter(o => o.status === 'CANCELLED')

  const query = search.value.trim().toLowerCase()
  if (query) {
    orders = orders.filter(o =>
      o.activeIngredientName.toLowerCase().includes(query)
      || o.dosageInstructions.toLowerCase().includes(query)
      || (o.remarks ?? '').toLowerCase().includes(query)
      || (patientNameByOrder.value.get(o.id) ?? '').toLowerCase().includes(query)
      || o.allocations.some(a => a.brandName.toLowerCase().includes(query) || a.location.toLowerCase().includes(query)),
    )
  }
  return orders
})

const hasFilters = computed(() => !!search.value.trim() || (view.value === 'queue' && (!!villageCode.value || lowStockOnly.value)))
const isDeepLinked = computed(() => !!filterPatient.value || !!filterVisit.value)

function clearFilters() {
  search.value = ''
  villageCode.value = ''
  lowStockOnly.value = false
}
function clearDeepLink() {
  router.replace(paths.orders())
}

const historyTitle = computed(() => ({ queue: '', dispensed: 'Dispensed orders', cancelled: 'Cancelled orders', all: 'All orders' })[view.value])

const orderActionButtons = (root: ParentNode | null = document.querySelector('[data-orders-queue], [data-orders-history]')) =>
  [...(root?.querySelectorAll<HTMLTableRowElement>('tr[data-order-id]') ?? [])]
  .map(row => row.querySelector<HTMLButtonElement>('button:not(:disabled)'))
  .filter((button): button is HTMLButtonElement => !!button)

/** Search ↓ enters the worklist; arrows and Home/End move between order actions. */
function onOrderSearchDown(event: KeyboardEvent) {
  const first = orderActionButtons()[0]
  if (!first)
    return
  event.preventDefault()
  first.focus()
}

function onOrderListKeydown(event: KeyboardEvent) {
  const target = event.target as HTMLElement
  const row = target.closest<HTMLTableRowElement>('tr[data-order-id]')
  if (!row || event.altKey || event.ctrlKey || event.metaKey)
    return
  const buttons = orderActionButtons(event.currentTarget as HTMLElement)
  const index = buttons.findIndex(button => button.closest('tr[data-order-id]') === row)
  if (index < 0)
    return
  let next: HTMLElement | undefined
  if (event.key === 'ArrowDown')
    next = buttons[index + 1]
  else if (event.key === 'ArrowUp')
    next = index === 0 ? document.querySelector<HTMLInputElement>('[data-orders-search] input') ?? undefined : buttons[index - 1]
  else if (event.key === 'Home')
    next = buttons[0]
  else if (event.key === 'End')
    next = buttons.at(-1)
  else
    return
  event.preventDefault()
  next?.focus()
  next?.scrollIntoView({ block: 'nearest' })
}

/** What the optimistic edits need: the listed order, with the queue's (fresher) allocations. */
function snapshotOf(item: PharmacyOrderItem): OrderSnapshot {
  const full = ordersById.value.get(item.orderId)
  return {
    medicationActiveIngredientId: item.medicationActiveIngredientId || full?.medicationActiveIngredientId || '',
    ingredientName: item.medicationName,
    unit: item.unitOfMeasurement || full?.unitOfMeasurement || undefined,
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
    notifySaved(outcome, `Dispensed ${formatQuantity(before.quantity, before.unit)} of ${before.ingredientName}${patientName ? ` for ${patientName}` : ''}.`)
    return true
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, `${before.ingredientName} could not be dispensed.`))
    return false
  }
  finally {
    dispensing.value.delete(orderId)
  }
}

async function dispenseAll(visit: PharmacyVisitBundle, patientName: string) {
  for (const item of [...visit.orders])
    await dispense(item.orderId, snapshotOf(item), visit.visitId, patientName)
}

function startChange(orderId: string, visitId?: string, patientName?: string) {
  const order = ordersById.value.get(orderId)
  if (order)
    changing.value = { order, visitId, patientName }
}

function startCancel(orderId: string, visitId?: string, patientName?: string) {
  const order = ordersById.value.get(orderId)
  if (order)
    cancelling.value = { order, visitId, patientName }
}

function startRebatch(item: PharmacyOrderItem, visitId: string, patientName: string) {
  rebatching.value = {
    order: { orderId: item.orderId, medicationName: item.medicationName, quantity: item.quantity, unit: item.unitOfMeasurement || undefined },
    before: snapshotOf(item),
    visitId,
    patientName,
  }
}

function startRebatchFromHistory(order: MedicationOrder) {
  rebatching.value = {
    order: { orderId: order.id, medicationName: order.activeIngredientName, quantity: order.quantity, unit: order.unitOfMeasurement || undefined },
    before: orderSnapshot(order),
    visitId: visitByOrder.value.get(order.id),
    patientName: patientNameByOrder.value.get(order.id),
  }
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
    toast.error(getApiErrorMessage(err, 'The order could not be deleted.'))
  }
}
</script>

<template>
  <div class="flex flex-col gap-3 pb-8">
    <PageHeader
      title="Orders"
      description="Pick each pending order from its batches, hand it over, then mark it dispensed."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Orders' }]"
      class="mb-0!"
    />

    <!-- One cell per view: picks the list below and shows how many are in it. -->
    <div role="group" aria-label="Order views" class="grid grid-cols-2 gap-px border border-border bg-border sm:grid-cols-4">
      <button
        v-for="cell in cells"
        :key="cell.key"
        type="button"
        class="flex min-w-0 flex-col gap-0.5 bg-card px-3 py-1.5 text-left hover:bg-accent focus-visible:bg-accent focus-visible:outline-none"
        :class="view === cell.key && 'bg-accent shadow-[inset_0_-3px_0_var(--color-primary)]'"
        :aria-pressed="view === cell.key"
        :data-orders-view="cell.key"
        @click="view = cell.key"
      >
        <span class="flex items-baseline justify-between gap-2">
          <span class="truncate text-xs font-bold text-chrome touch:text-sm">{{ cell.label }}</span>
          <span class="text-lg leading-tight font-bold tabular-nums" :class="cell.count === 0 && 'text-muted-foreground'">
            {{ cell.count ?? '–' }}
          </span>
        </span>
        <span class="truncate text-xs text-muted-foreground touch:text-[0.8125rem]">{{ cell.detail }}</span>
      </button>
    </div>

    <!-- Deep link from a patient or visit. -->
    <div
      v-if="isDeepLinked && view === 'queue'"
      class="flex flex-wrap items-center justify-between gap-2 border border-primary/40 bg-accent px-3 py-1.5 text-xs touch:text-sm"
      role="status"
    >
      <p class="flex items-center gap-1.5 font-semibold text-chrome">
        <PhFunnel class="size-4 shrink-0" aria-hidden="true" />
        Showing the orders of one {{ filterVisit ? 'visit' : 'patient' }} only.
      </p>
      <Button variant="outline" size="sm" @click="clearDeepLink">
        Show the whole queue
      </Button>
    </div>

    <!-- Search and filters -->
    <div class="flex flex-col gap-2 border border-border bg-card p-2 sm:flex-row sm:flex-wrap sm:items-center">
      <div class="relative sm:w-80" data-orders-search>
        <PhMagnifyingGlass class="pointer-events-none absolute top-1/2 left-2.5 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <Input
          v-model="search"
          type="search"
          :aria-label="view === 'queue' ? 'Search the queue' : 'Search orders'"
          :placeholder="view === 'queue' ? 'Patient, medication, brand or diagnosis' : 'Medication, brand, instructions or remarks'"
          class="pr-8 pl-8"
          aria-keyshortcuts="ArrowDown"
          @keydown.down="onOrderSearchDown"
        />
        <button
          v-if="search"
          type="button"
          aria-label="Clear search"
          class="absolute top-1/2 right-1.5 -translate-y-1/2 p-1 text-muted-foreground hover:text-foreground"
          @click="search = ''"
        >
          <PhX class="size-3.5" aria-hidden="true" />
        </button>
      </div>
      <template v-if="view === 'queue'">
        <NativeSelect v-model="villageCode" aria-label="Village" class="sm:w-48">
          <NativeSelectOption value="">
            All villages
          </NativeSelectOption>
          <NativeSelectOption v-for="v in villageCodes" :key="v.code" :value="v.code">
            {{ v.name }} ({{ v.code }})
          </NativeSelectOption>
        </NativeSelect>
        <label class="flex cursor-pointer items-center gap-2 text-xs font-semibold select-none touch:min-h-11 touch:text-sm">
          <input v-model="lowStockOnly" type="checkbox" class="size-4 accent-primary touch:size-5">
          Low stock only
        </label>
      </template>
      <Button v-if="hasFilters" variant="ghost" size="sm" class="text-xs touch:h-10" @click="clearFilters">
        Reset
      </Button>
      <p class="text-xs text-muted-foreground sm:ml-auto touch:text-sm" role="status">
        <template v-if="view === 'queue' && pharmacyQueue">
          {{ shownQueue.orders }} {{ shownQueue.orders === 1 ? 'order' : 'orders' }} · {{ shownQueue.patients }} {{ shownQueue.patients === 1 ? 'patient' : 'patients' }}
        </template>
        <template v-else-if="view !== 'queue' && allOrders">
          {{ filteredHistory.length }} {{ filteredHistory.length === 1 ? 'order' : 'orders' }}
        </template>
      </p>
    </div>

    <!-- The queue: pending orders by patient and visit. -->
    <div
      v-if="view === 'queue'"
      class="flex flex-col gap-3"
      data-orders-queue
      aria-keyshortcuts="ArrowDown ArrowUp Home End"
      @keydown="onOrderListKeydown"
    >
      <StateMessage v-if="queuePending && !pharmacyQueue" kind="loading" message="Loading the dispensing queue…" class="border border-border bg-card" />
      <StateMessage v-else-if="queueError && !pharmacyQueue" kind="error" :message="describeLoadError(queueErr, 'The queue could not be loaded. Check the connection and reload.')" />
      <div
        v-else-if="filteredQueue.length === 0"
        class="flex flex-col items-center gap-2 border border-border bg-card px-3 py-10 text-center"
        role="status"
      >
        <template v-if="hasFilters || isDeepLinked">
          <p class="text-xs text-muted-foreground touch:text-sm">
            No pending orders match{{ isDeepLinked ? ' for this ' + (filterVisit ? 'visit' : 'patient') : ' these filters' }}.
          </p>
          <Button variant="outline" size="sm" @click="clearFilters(); isDeepLinked && clearDeepLink()">
            Show the whole queue
          </Button>
        </template>
        <template v-else>
          <p class="font-semibold">
            Nothing to dispense
          </p>
          <p class="text-xs text-muted-foreground touch:text-sm">
            Orders appear here as soon as a doctor places them in a consult.
          </p>
          <Button variant="outline" size="sm" @click="view = 'dispensed'">
            See dispensed orders
          </Button>
        </template>
      </div>
      <OrderQueueCard
        v-for="group in filteredQueue"
        v-else
        :key="group.patient.displayId"
        :group="group"
        :patient="patientOf(group.patient.displayId)"
        :orders="ordersById"
        :dispensing="dispensing"
        @dispense="(order, visit) => dispense(order.orderId, snapshotOf(order), visit.visitId, group.patient.name)"
        @dispense-all="visit => dispenseAll(visit, group.patient.name)"
        @change="(order, visit) => startChange(order.orderId, visit.visitId, group.patient.name)"
        @cancel="(order, visit) => startCancel(order.orderId, visit.visitId, group.patient.name)"
        @change-batch="(order, visit) => startRebatch(order, visit.visitId, group.patient.name)"
      />
    </div>

    <!-- History: dispensed, cancelled or every order. -->
    <AppPanel
      v-else
      :title="historyTitle"
      flush
      data-orders-history
      aria-keyshortcuts="ArrowDown ArrowUp Home End"
      @keydown="onOrderListKeydown"
    >
      <StateMessage v-if="ordersPending && !allOrders" kind="loading" message="Loading orders…" />
      <StateMessage v-else-if="ordersError && !allOrders" kind="error" :message="describeLoadError(ordersErr, 'The orders could not be loaded. Check the connection and reload.')" />
      <div v-else-if="filteredHistory.length === 0" class="flex flex-col items-center gap-2 px-3 py-10 text-center" role="status">
        <p class="text-xs text-muted-foreground touch:text-sm">
          {{ hasFilters ? 'No orders match this search.' : view === 'dispensed' ? 'No orders have been dispensed yet.' : view === 'cancelled' ? 'No orders have been cancelled.' : 'No orders yet.' }}
        </p>
        <Button v-if="hasFilters" variant="outline" size="sm" @click="clearFilters">
          Clear search
        </Button>
      </div>
      <OrderHistoryTable
        v-else
        :orders="filteredHistory"
        :can-delete="canDelete"
        :dispensing="dispensing"
        :patient-names="patientNameByOrder"
        :batches-label="view === 'dispensed' ? 'Dispensed from' : 'Batches'"
        @dispense="order => dispense(order.id, orderSnapshot(order), visitByOrder.get(order.id), patientNameByOrder.get(order.id))"
        @change="order => startChange(order.id, visitByOrder.get(order.id), patientNameByOrder.get(order.id))"
        @cancel="order => startCancel(order.id, visitByOrder.get(order.id), patientNameByOrder.get(order.id))"
        @change-batch="startRebatchFromHistory"
        @delete="handleDelete"
      />
    </AppPanel>

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
      :visit-id="cancelling.visitId"
      :patient-name="cancelling.patientName"
      @close="cancelling = null"
    />
    <ChangeBatchDialog
      v-if="rebatching"
      :order="rebatching.order"
      :before="rebatching.before"
      :visit-id="rebatching.visitId"
      :patient-name="rebatching.patientName"
      @close="rebatching = null"
    />
  </div>
</template>
