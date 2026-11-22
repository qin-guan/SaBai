import type { QueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsMedicationBrandsCreatePostEndpoint,
  saBaiAPIEndpointsMedicationBrandsDeleteDeleteEndpoint,
  saBaiAPIEndpointsMedicationBrandsListGetEndpoint,
  saBaiAPIEndpointsMedicationBrandsUpdatePatchEndpoint1,
  saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpoint,
  saBaiAPIEndpointsMedicationIngredientsCreatePostEndpoint,
  saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpoint,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpoint,
  saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpoint,
  saBaiAPIEndpointsMedicationStockByIngredientGetEndpoint,
  saBaiAPIEndpointsMedicationStockCreatePostEndpoint,
  saBaiAPIEndpointsMedicationStockSplitsPostEndpoint,
  saBaiAPIEndpointsMedicationStockUpdatePatchEndpoint,
  saBaiAPIEndpointsMedicationStockWithBrandGetEndpoint,
  saBaiAPIEndpointsMedicationHistoryGetEndpoint,
  saBaiAPIEndpointsOrdersCreatePostEndpoint,
  saBaiAPIEndpointsOrdersDeleteDeleteEndpoint,
  saBaiAPIEndpointsOrdersListGetEndpoint,
  saBaiAPIEndpointsOrdersUpdatePatchEndpoint,
  saBaiAPIEndpointsPharmacyOrdersGetEndpoint,
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationStockByIngredientGetResponse,
  SaBaiAPIEndpointsMedicationBrandsListGetResponse,
  SaBaiAPIEndpointsMedicationBrandsWithIngredientGetResponse,
  SaBaiAPIEndpointsMedicationIngredientsListGetResponse,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
  SaBaiAPIDomainOrderStatusKey,
  SaBaiAPIEndpointsMedicationHistoryGetResponse,
  SaBaiAPIEndpointsOrdersListGetResponse,
  SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem,
  SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem,
  SaBaiAPIEndpointsPharmacyOrdersGetResponse,
  SaBaiAPIEndpointsPharmacyOrdersGetVisitBundle,
} from '@sabai/sdk/types'
import { defineOfflineMutation, requestOptions } from '~/offline/defineOfflineMutation'
import { registerOfflinePrefetch } from '~/offline/prefetch'
import { apiConfig, queryKeys } from '~/utils/api'
import { getApiErrorStatus } from '~/utils/apiErrors'
import {
  adjustAvailability,
  applyAllocations,
  applySplit,
  patchOrderInPharmacyQueue,
  patchStock,
  removeOrderFromPharmacyQueue,
  upsertById,
  upsertOrderInPharmacyQueue,
  upsertStock,
  type OrderAllocation,
  type StockSplitData,
} from '~/utils/medicationCache'

/**
 * Medication: reads, offline-capable writes and offline download for active
 * ingredients, brands and stock lots. Follows `villageCodes.ts`: one fetch
 * function per query, pure idempotent cache edits by id, one definition per
 * REST write with everything `send` needs in `vars`, and a prefetch.
 *
 * Four lists are cached and edited together, because one change shows in
 * several: ingredients, brands, brands with their ingredient name (the stock
 * form's picker) and stock lines with brand and ingredient names.
 *
 * Orders are for an active ingredient. The server reserves stock for an order
 * when it is placed (earliest expiry first, across brands: the order's
 * `allocations`), takes the reserved units off hand when it is dispensed, and
 * releases them when it is cancelled or deleted. The optimistic edits follow
 * the same steps as deltas keyed by the write (see `withPendingDelta`), so a
 * queued write re-applied after a refetch never counts twice.
 */

export type MedicationIngredient = SaBaiAPIEndpointsMedicationIngredientsListGetResponse
export type MedicationBrand = SaBaiAPIEndpointsMedicationBrandsListGetResponse
export type MedicationBrandWithIngredient = SaBaiAPIEndpointsMedicationBrandsWithIngredientGetResponse
export type MedicationStockLine = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse
export type StockStatusKey = SaBaiAPIDomainStockStatusKey

export type IngredientAvailability = SaBaiAPIEndpointsMedicationStockByIngredientGetResponse
export type MedicationOrder = SaBaiAPIEndpointsOrdersListGetResponse
export type OrderStatusKey = SaBaiAPIDomainOrderStatusKey
export type PharmacyPatientOrders = SaBaiAPIEndpointsPharmacyOrdersGetResponse
export type PharmacyVisitBundle = SaBaiAPIEndpointsPharmacyOrdersGetVisitBundle
export type PharmacyOrderItem = SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem
export type PharmacyDiagnosisItem = SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem
export type MedicationHistoryEntry = SaBaiAPIEndpointsMedicationHistoryGetResponse

// ── Reads ────────────────────────────────────────────────────────────────

export function fetchMedicationIngredients(signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationIngredientsListGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchMedicationBrands(signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationBrandsListGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchMedicationBrandsWithIngredient(signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchMedicationStock(signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationStockWithBrandGetEndpoint({ ...apiConfig, signal }).unwrap()
}

/** Every active ingredient with the units that can still be ordered (0 when none). */
export function fetchIngredientAvailability(signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationStockByIngredientGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchOrders(status?: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsOrdersListGetEndpoint({ ...apiConfig, query: status ? { status } : undefined, signal }).unwrap()
}

export function fetchPharmacyOrders(signal?: AbortSignal) {
  return saBaiAPIEndpointsPharmacyOrdersGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchMedicationHistory(brandId: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationHistoryGetEndpoint({ ...apiConfig, query: { brandId }, signal }).unwrap()
}

/** The roles the API lets manage medication, stock and the pharmacy queue (PharmacyWork). */
export const PHARMACY_ROLES: readonly string[] = ['admin', 'member', 'pharmacist']
/** The roles the API lets place, change and list orders (OrderWork). */
export const ORDER_ROLES: readonly string[] = ['admin', 'member', 'doctor', 'pharmacist']

// The stock pages and the pharmacy queue work offline for the pharmacy.
registerOfflinePrefetch('medication', async (queryClient) => {
  await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.medicationIngredients(), queryFn: ({ signal }) => fetchMedicationIngredients(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrands(), queryFn: ({ signal }) => fetchMedicationBrands(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrandsWithIngredient(), queryFn: ({ signal }) => fetchMedicationBrandsWithIngredient(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationStock(), queryFn: ({ signal }) => fetchMedicationStock(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.pharmacyOrders(), queryFn: ({ signal }) => fetchPharmacyOrders(signal) }),
  ])
}, { roles: PHARMACY_ROLES })

// The consult form orders offline from what was last known to be available.
registerOfflinePrefetch('medication.orders', async (queryClient) => {
  await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.medicationAvailability(), queryFn: ({ signal }) => fetchIngredientAvailability(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.ordersList(), queryFn: ({ signal }) => fetchOrders(undefined, signal) }),
  ])
}, { roles: ORDER_ROLES })

// ── Cache edits ──────────────────────────────────────────────────────────

function editIngredients(queryClient: QueryClient, edit: (list: MedicationIngredient[]) => MedicationIngredient[]) {
  queryClient.setQueryData<MedicationIngredient[]>(queryKeys.medicationIngredients(), old => old && edit(old))
}

function editBrands(queryClient: QueryClient, edit: (list: MedicationBrand[]) => MedicationBrand[]) {
  queryClient.setQueryData<MedicationBrand[]>(queryKeys.medicationBrands(), old => old && edit(old))
}

function editBrandsWithIngredient(queryClient: QueryClient, edit: (list: MedicationBrandWithIngredient[]) => MedicationBrandWithIngredient[]) {
  queryClient.setQueryData<MedicationBrandWithIngredient[]>(queryKeys.medicationBrandsWithIngredient(), old => old && edit(old))
}

function editStock(queryClient: QueryClient, edit: (list: MedicationStockLine[]) => MedicationStockLine[]) {
  queryClient.setQueryData<MedicationStockLine[]>(queryKeys.medicationStock(), old => old && edit(old))
}

/** Adds or replaces an ingredient and carries its name and "fall below" level to the lines that show them. */
function upsertIngredient(queryClient: QueryClient, ingredient: MedicationIngredient) {
  editIngredients(queryClient, list => upsertById(list, ingredient))
  const brandIds = new Set(
    (queryClient.getQueryData<MedicationBrand[]>(queryKeys.medicationBrands()) ?? [])
      .filter(b => b.activeIngredientId === ingredient.id)
      .map(b => b.id),
  )
  editBrandsWithIngredient(queryClient, list =>
    list.map(b => (brandIds.has(b.id) && b.activeIngredientName !== ingredient.name
      ? { ...b, activeIngredientName: ingredient.name }
      : b)))
  editStock(queryClient, list =>
    patchAll(list, row => row.ingredientId === ingredient.id, { ingredientName: ingredient.name, fallBelow: ingredient.fallBelow }))
}

function removeIngredient(queryClient: QueryClient, id: string) {
  editIngredients(queryClient, list => list.filter(i => i.id !== id))
}

/** Adds or replaces a brand and carries its name and ingredient to the lists and stock lines that show them. */
function upsertBrand(
  queryClient: QueryClient,
  brand: MedicationBrand,
  ingredient: { name: string, fallBelow: number },
) {
  editBrands(queryClient, list => upsertById(list, brand))
  editBrandsWithIngredient(queryClient, list =>
    upsertById(list, { id: brand.id, name: brand.name, activeIngredientName: ingredient.name }))
  editStock(queryClient, list =>
    patchAll(list, row => row.medicationBrandId === brand.id, {
      brandName: brand.name,
      ingredientId: brand.activeIngredientId,
      ingredientName: ingredient.name,
      fallBelow: ingredient.fallBelow,
    }))
}

function removeBrand(queryClient: QueryClient, id: string) {
  editBrands(queryClient, list => list.filter(b => b.id !== id))
  editBrandsWithIngredient(queryClient, list => list.filter(b => b.id !== id))
}

function patchAll(
  list: MedicationStockLine[],
  matches: (row: MedicationStockLine) => boolean,
  patch: Partial<MedicationStockLine>,
): MedicationStockLine[] {
  let result = list
  for (const row of list.filter(matches)) result = patchStock(result, row.id, patch)
  return result
}

const invalidateIngredients = (queryClient: QueryClient) => Promise.all([
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationIngredients() }),
  // Brand and stock lines show the ingredient's name.
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationBrands() }),
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() }),
]).then(() => {})

const invalidateBrands = (queryClient: QueryClient) => Promise.all([
  // Covers the with-ingredient list too (same key prefix).
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationBrands() }),
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() }),
]).then(() => {})

const invalidateStock = (queryClient: QueryClient) =>
  queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() })

// ── Ingredients ──────────────────────────────────────────────────────────

export interface SaveIngredientVars {
  id: string
  name: string
  unitOfMeasurement: string
  fallBelow: number
  remarks: string | null
}

export interface DeleteIngredientVars {
  id: string
  name: string
}

function ingredientRecord(vars: SaveIngredientVars): MedicationIngredient {
  return {
    id: vars.id,
    name: vars.name,
    unitOfMeasurement: vars.unitOfMeasurement,
    fallBelow: vars.fallBelow,
    remarks: vars.remarks,
  }
}

export const createMedicationIngredient = defineOfflineMutation({
  key: 'medication.ingredient.create',
  creates: true,
  describe: (vars: SaveIngredientVars) => `New active ingredient ${vars.name}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationIngredientsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      // The client-chosen id makes a replay of a create whose response was lost harmless.
      body: { id: vars.id, name: vars.name, unitOfMeasurement: vars.unitOfMeasurement, fallBelow: vars.fallBelow, remarks: vars.remarks },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertIngredient(queryClient, ingredientRecord(vars)),
  onSuccess: async (saved, _vars, queryClient) => {
    upsertIngredient(queryClient, saved)
    await invalidateIngredients(queryClient)
  },
})

export const updateMedicationIngredient = defineOfflineMutation({
  key: 'medication.ingredient.update',
  describe: (vars: SaveIngredientVars) => `Edit active ingredient ${vars.name}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationIngredientsUpdatePutEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { name: vars.name, unitOfMeasurement: vars.unitOfMeasurement, fallBelow: vars.fallBelow, remarks: vars.remarks },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertIngredient(queryClient, ingredientRecord(vars)),
  onSuccess: async (saved, _vars, queryClient) => {
    upsertIngredient(queryClient, saved)
    await invalidateIngredients(queryClient)
  },
})

export const deleteMedicationIngredient = defineOfflineMutation({
  key: 'medication.ingredient.delete',
  describe: (vars: DeleteIngredientVars) => `Delete active ingredient ${vars.name}`,
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsMedicationIngredientsDeleteDeleteEndpoint({ ...apiConfig, ...requestOptions(ctx), path: { id: vars.id } }).unwrap()
    }
    catch (error) {
      // Already gone: done.
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => removeIngredient(queryClient, vars.id),
  onSuccess: (_result, _vars, queryClient) => invalidateIngredients(queryClient),
})

// ── Brands ───────────────────────────────────────────────────────────────

export interface SaveBrandVars {
  id: string
  name: string
  activeIngredientId: string
  /** Empty string when there are none (the API requires the field). */
  remarks: string
  /** The chosen ingredient's name and level, so the lists that show them can be updated without a lookup. */
  ingredientName: string
  ingredientFallBelow: number
}

export interface DeleteBrandVars {
  id: string
  name: string
}

function brandRecord(vars: SaveBrandVars): MedicationBrand {
  return {
    id: vars.id,
    name: vars.name,
    activeIngredientId: vars.activeIngredientId,
    remarks: vars.remarks || null,
  }
}

const brandIngredient = (vars: SaveBrandVars) => ({ name: vars.ingredientName, fallBelow: vars.ingredientFallBelow })

export const createMedicationBrand = defineOfflineMutation({
  key: 'medication.brand.create',
  creates: true,
  describe: (vars: SaveBrandVars) => `New brand ${vars.name} (${vars.ingredientName})`,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.activeIngredientId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationBrandsCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: { id: vars.id, name: vars.name, activeIngredientId: vars.activeIngredientId, remarks: vars.remarks },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertBrand(queryClient, brandRecord(vars), brandIngredient(vars)),
  onSuccess: async (saved, vars, queryClient) => {
    upsertBrand(queryClient, saved, brandIngredient(vars))
    await invalidateBrands(queryClient)
  },
})

export const updateMedicationBrand = defineOfflineMutation({
  key: 'medication.brand.update',
  describe: (vars: SaveBrandVars) => `Edit brand ${vars.name}`,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.activeIngredientId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationBrandsUpdatePatchEndpoint1({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { name: vars.name, activeIngredientId: vars.activeIngredientId, remarks: vars.remarks },
    }).unwrap(),
  optimistic: (vars, queryClient) => upsertBrand(queryClient, brandRecord(vars), brandIngredient(vars)),
  onSuccess: async (saved, vars, queryClient) => {
    upsertBrand(queryClient, saved, brandIngredient(vars))
    await invalidateBrands(queryClient)
  },
})

export const deleteMedicationBrand = defineOfflineMutation({
  key: 'medication.brand.delete',
  describe: (vars: DeleteBrandVars) => `Delete brand ${vars.name}`,
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsMedicationBrandsDeleteDeleteEndpoint({ ...apiConfig, ...requestOptions(ctx), path: { id: vars.id } }).unwrap()
    }
    catch (error) {
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => removeBrand(queryClient, vars.id),
  onSuccess: (_result, _vars, queryClient) => invalidateBrands(queryClient),
})

// ── Stock lots ───────────────────────────────────────────────────────────

/** The names the stock list shows for a lot's brand and ingredient, captured when the user acts. */
export interface StockBrandInfo {
  medicationBrandId: string
  brandName: string
  ingredientId: string
  ingredientName: string
  fallBelow: number
}

export interface CreateStockVars extends StockBrandInfo {
  id: string
  quantity: number
  /** `YYYY-MM-DD` from the date input. */
  expiry: string
  location: string
  stockStatus: StockStatusKey
  remarks: string | null
}

export interface UpdateStockVars {
  id: string
  location: string
  stockStatus: StockStatusKey
  remarks: string | null
  /** For the pending-changes list. */
  brandName: string
}

export interface SplitStockVars {
  stockId: string
  /** The line as it was when the user split it; the optimistic result is built from this, not from the cache. */
  parent: MedicationStockLine
  splits: StockSplitData[]
}

function stockRecord(vars: CreateStockVars): MedicationStockLine {
  return {
    id: vars.id,
    medicationBrandId: vars.medicationBrandId,
    quantity: vars.quantity,
    expiry: vars.expiry,
    reservedQuantity: 0,
    availableQuantity: vars.quantity,
    location: vars.location,
    stockStatus: vars.stockStatus,
    remarks: vars.remarks,
    parentStockId: null,
    brandName: vars.brandName,
    ingredientName: vars.ingredientName,
    ingredientId: vars.ingredientId,
    fallBelow: vars.fallBelow,
    isLowStock: false,
  }
}

export const createMedicationStock = defineOfflineMutation({
  key: 'medication.stock.create',
  creates: true,
  describe: (vars: CreateStockVars) => `New stock ${vars.brandName} × ${vars.quantity} at ${vars.location}`,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.medicationBrandId],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationStockCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        medicationBrandId: vars.medicationBrandId,
        quantity: vars.quantity,
        expiry: vars.expiry,
        location: vars.location,
        stockStatus: vars.stockStatus,
        remarks: vars.remarks,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => editStock(queryClient, list => upsertStock(list, stockRecord(vars))),
  // The response has no brand or ingredient names, so the list is refetched rather than patched.
  onSuccess: (_saved, _vars, queryClient) => invalidateStock(queryClient),
})

export const updateMedicationStock = defineOfflineMutation({
  key: 'medication.stock.update',
  describe: (vars: UpdateStockVars) => `Edit stock ${vars.brandName} at ${vars.location}`,
  recordIds: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationStockUpdatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: { location: vars.location, stockStatus: vars.stockStatus, remarks: vars.remarks ?? '' },
    }).unwrap(),
  optimistic: (vars, queryClient) =>
    editStock(queryClient, list => patchStock(list, vars.id, {
      location: vars.location,
      stockStatus: vars.stockStatus,
      remarks: vars.remarks,
    })),
  onSuccess: (_saved, _vars, queryClient) => invalidateStock(queryClient),
})

/**
 * Splits a lot's unreserved units into child lots (the parent keeps what is
 * reserved for pending orders). Idempotent by construction: the parent ends at
 * its reserved quantity and each child line is upserted under the client id in
 * `vars.splits`, built from `vars.parent` (a snapshot), so re-applying yields
 * the same list. The API replays safely on the same ids.
 */
export const splitMedicationStock = defineOfflineMutation({
  key: 'medication.stock.split',
  describe: (vars: SplitStockVars) =>
    `Split stock ${vars.parent.brandName} at ${vars.parent.location} into ${vars.splits.length}`,
  recordIds: vars => [vars.stockId, ...vars.splits.map(s => s.id)],
  send: (vars, ctx) =>
    saBaiAPIEndpointsMedicationStockSplitsPostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.stockId },
      body: {
        splits: vars.splits.map(s => ({
          id: s.id,
          quantity: s.quantity,
          location: s.location,
          stockStatus: s.stockStatus,
          remarks: s.remarks ?? undefined,
        })),
      },
    }).unwrap(),
  optimistic: (vars, queryClient) =>
    editStock(queryClient, list => applySplit(list, vars.parent, vars.splits)),
  onSuccess: (_saved, _vars, queryClient) => invalidateStock(queryClient),
})

// ── Orders & pharmacy queue ──────────────────────────────────────────────

export interface CreateOrderContext {
  patientId?: string
  patientDisplayId: string
  patientName: string
  villageCode: string | null
  visitId: string
  visitDate: string
  diagnoses: PharmacyDiagnosisItem[]
  isLowStock: boolean
}

export interface CreateOrderVars {
  id: string
  consultId: string
  medicationActiveIngredientId: string
  /** For the lists and the pending-changes list. */
  ingredientName: string
  quantity: number
  dosageInstructions: string
  remarks: string | null
  createdAt?: string
  /** The patient and visit, so the order shows in the pharmacy queue before it reaches the server. */
  context?: CreateOrderContext
}

/** The order as the user saw it when acting on it: what the optimistic stock edits undo or move. */
export interface OrderSnapshot {
  medicationActiveIngredientId: string
  ingredientName: string
  quantity: number
  allocations: OrderAllocation[]
}

/** A change to a pending order: new fields, then dispensing or cancelling it (as `PATCH /orders/{id}`). */
export interface UpdateOrderVars {
  id: string
  /** Unique per change, so two queued edits of one order each count once. */
  changeId: string
  /** Omitted: only the fields change and the order stays pending. */
  status?: Exclude<OrderStatusKey, 'PENDING'>
  dosageInstructions?: string
  remarks?: string | null
  quantity?: number
  medicationActiveIngredientId?: string
  /** The new ingredient's name, with `medicationActiveIngredientId`. */
  ingredientName?: string
  before: OrderSnapshot
  visitId?: string
  patientName?: string
  dispensedById?: string | null
  /** When the user dispensed it (device time; the server records its own). */
  dispensedAt?: string
}

export interface DeleteOrderVars {
  id: string
  before: OrderSnapshot & { status: OrderStatusKey }
  visitId?: string
}

/** Edits every cached order list: all orders (`status` undefined) and the per-status lists. */
function editOrderLists(queryClient: QueryClient, edit: (list: MedicationOrder[], status: string | undefined) => MedicationOrder[]) {
  for (const query of queryClient.getQueryCache().findAll({ queryKey: queryKeys.ordersList() })) {
    const status = typeof query.queryKey[2] === 'string' ? query.queryKey[2] : undefined
    queryClient.setQueryData<MedicationOrder[]>(query.queryKey, old => old && edit(old, status))
  }
}

/**
 * Puts `order` in every cached order list it belongs in (by status) and takes
 * it out of the others. `ensureLists` also starts the all-orders and status
 * lists when they are not cached (a new order shows before any list was read).
 */
function placeOrderInLists(queryClient: QueryClient, order: MedicationOrder, ensureLists = false) {
  if (ensureLists) {
    for (const key of [queryKeys.ordersList(), queryKeys.ordersList(order.status)])
      queryClient.setQueryData<MedicationOrder[]>(key, old => old ?? [])
  }
  editOrderLists(queryClient, (list, status) =>
    !status || status === order.status
      ? upsertById(list, order, 'start')
      : list.filter(o => o.id !== order.id))
}

function findOrder(queryClient: QueryClient, id: string): MedicationOrder | undefined {
  for (const [, list] of queryClient.getQueriesData<MedicationOrder[]>({ queryKey: queryKeys.ordersList() })) {
    const found = list?.find(o => o.id === id)
    if (found) return found
  }
  return undefined
}

function editAvailability(queryClient: QueryClient, edit: (rows: IngredientAvailability[]) => IngredientAvailability[]) {
  queryClient.setQueryData<IngredientAvailability[]>(queryKeys.medicationAvailability(), old => old && edit(old))
}

function editPharmacyQueue(queryClient: QueryClient, edit: (queue: PharmacyPatientOrders[]) => PharmacyPatientOrders[]) {
  queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old => old && edit(old))
}

/** After any order write: the server's orders, reservations and stock numbers replace the optimistic ones. */
function refreshAfterOrderWrite(queryClient: QueryClient, saved?: MedicationOrder) {
  if (saved) placeOrderInLists(queryClient, saved)
  return Promise.all([
    queryClient.invalidateQueries({ queryKey: queryKeys.orders() }),
    queryClient.invalidateQueries({ queryKey: queryKeys.pharmacyOrders() }),
    // Covers the stock list and the by-ingredient availability.
    queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() }),
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() }),
  ]).then(() => {})
}

type PharmacyJourney = {
  pharmacyPending?: number
  pharmacyOrders?: number
  pharmacyAt?: string | null
  /** Keys of the optimistic order actions already counted in this journey (not on server data). */
  pendingOrderActions?: string[]
}

/**
 * Applies `adjust` to a journey once per action key. The keys are kept on the
 * journey itself (the query cache copies objects, so they cannot be tracked by
 * identity); fresh data from the server has none, so a queued action counts
 * once on top of it.
 */
function adjustJourneyOnce(journey: PharmacyJourney, actionKey: string, adjust: (journey: PharmacyJourney) => PharmacyJourney): PharmacyJourney {
  const seen = journey.pendingOrderActions ?? []
  if (seen.includes(actionKey)) return journey
  return { ...adjust(journey), pendingOrderActions: [...seen, actionKey] }
}

function adjustVisitPharmacyJourney(
  queryClient: QueryClient,
  visitId: string,
  actionKey: string,
  adjust: (journey: PharmacyJourney) => PharmacyJourney,
) {
  queryClient.setQueriesData<{ visits?: Array<{ id: string, journey?: PharmacyJourney | null }> }>(
    { queryKey: ['patients', 'visits'] },
    (old) => {
      if (!old || !Array.isArray(old.visits)) return old
      if (!old.visits.some(v => v.id === visitId && v.journey)) return old
      return {
        ...old,
        visits: old.visits.map(v =>
          v.id === visitId && v.journey ? { ...v, journey: adjustJourneyOnce(v.journey, actionKey, adjust) } : v),
      }
    },
  )

  queryClient.setQueryData<{ patients?: Array<{ id: string, lastVisitId?: string | null, lastVisitJourney?: PharmacyJourney | null }> }>(
    queryKeys.patientsWithLatestVisit(),
    (old) => {
      if (!old || !Array.isArray(old.patients)) return old
      if (!old.patients.some(p => p.lastVisitId === visitId && p.lastVisitJourney)) return old
      return {
        ...old,
        patients: old.patients.map(p =>
          p.lastVisitId === visitId && p.lastVisitJourney
            ? { ...p, lastVisitJourney: adjustJourneyOnce(p.lastVisitJourney, actionKey, adjust) }
            : p),
      }
    },
  )
}

const orderKey = (id: string) => `order:${id}`

export const createOrder = defineOfflineMutation({
  key: 'medication.order.create',
  creates: true,
  describe: (vars: CreateOrderVars) => `Order ${vars.ingredientName} × ${vars.quantity}${vars.context?.patientName ? ` for ${vars.context.patientName}` : ''}`,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.consultId, ...(vars.context?.visitId ? [vars.context.visitId] : []), ...(vars.context?.patientId ? [vars.context.patientId] : [])],
  send: (vars, ctx) =>
    saBaiAPIEndpointsOrdersCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        consultId: vars.consultId,
        medicationActiveIngredientId: vars.medicationActiveIngredientId,
        quantity: vars.quantity,
        dosageInstructions: vars.dosageInstructions,
        remarks: vars.remarks,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const existing = findOrder(queryClient, vars.id)
    placeOrderInLists(queryClient, {
      id: vars.id,
      consultId: vars.consultId,
      medicationActiveIngredientId: vars.medicationActiveIngredientId,
      activeIngredientName: vars.ingredientName,
      quantity: vars.quantity,
      status: 'PENDING',
      dosageInstructions: vars.dosageInstructions,
      remarks: vars.remarks,
      dispensedById: null,
      dispensedAt: null,
      createdAt: vars.createdAt ?? existing?.createdAt ?? new Date().toISOString(),
      // Which batches it reserves is decided by the server.
      allocations: existing?.allocations ?? [],
    }, true)
    // The server reserves the units when it places the order.
    editAvailability(queryClient, rows => adjustAvailability(rows, vars.medicationActiveIngredientId, orderKey(vars.id), -vars.quantity))

    const c = vars.context
    if (c) {
      queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old =>
        upsertOrderInPharmacyQueue(old ?? [], {
          orderId: vars.id,
          medicationName: vars.ingredientName,
          quantity: vars.quantity,
          isLowStock: c.isLowStock,
          dosageInstructions: vars.dosageInstructions,
          allocations: [],
          patientDisplayId: c.patientDisplayId,
          patientName: c.patientName,
          villageCode: c.villageCode,
          visitId: c.visitId,
          visitDate: c.visitDate,
          diagnoses: c.diagnoses,
        }))
      adjustVisitPharmacyJourney(queryClient, c.visitId, `${vars.id}:create`, journey => ({
        ...journey,
        pharmacyPending: (journey.pharmacyPending ?? 0) + 1,
        pharmacyOrders: (journey.pharmacyOrders ?? 0) + 1,
      }))
    }
  },
  revert: (vars, queryClient) => {
    editOrderLists(queryClient, list => list.filter(o => o.id !== vars.id))
    editAvailability(queryClient, rows => adjustAvailability(rows, vars.medicationActiveIngredientId, orderKey(vars.id), null))
    editPharmacyQueue(queryClient, queue => removeOrderFromPharmacyQueue(queue, vars.id))
    if (vars.context?.visitId) {
      adjustVisitPharmacyJourney(queryClient, vars.context.visitId, `${vars.id}:create:revert`, journey => ({
        ...journey,
        pharmacyPending: Math.max(0, (journey.pharmacyPending ?? 0) - 1),
        pharmacyOrders: Math.max(0, (journey.pharmacyOrders ?? 0) - 1),
      }))
    }
  },
  onSuccess: (saved, _vars, queryClient) => refreshAfterOrderWrite(queryClient, saved),
})

function describeOrderUpdate(vars: UpdateOrderVars) {
  const name = vars.ingredientName ?? vars.before.ingredientName
  const forPatient = vars.patientName ? ` for ${vars.patientName}` : ''
  if (vars.status === 'DISPENSED') return `Dispense ${name} × ${vars.quantity ?? vars.before.quantity}${forPatient}`
  if (vars.status === 'CANCELLED') return `Cancel order ${name}${forPatient}`
  return `Change order ${name}${forPatient}`
}

export const updateOrder = defineOfflineMutation({
  key: 'medication.order.update',
  describe: describeOrderUpdate,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsOrdersUpdatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        status: vars.status,
        dosageInstructions: vars.dosageInstructions,
        remarks: vars.remarks,
        quantity: vars.quantity,
        medicationActiveIngredientId: vars.medicationActiveIngredientId,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const key = `${vars.id}:${vars.changeId}`
    const { before } = vars
    const ingredientId = vars.medicationActiveIngredientId ?? before.medicationActiveIngredientId
    const quantity = vars.quantity ?? before.quantity
    const reallocates = ingredientId !== before.medicationActiveIngredientId || quantity !== before.quantity

    // Field changes first, as on the server: a new quantity or ingredient moves the reservation.
    if (reallocates) {
      editAvailability(queryClient, (rows) => {
        if (ingredientId === before.medicationActiveIngredientId)
          return adjustAvailability(rows, ingredientId, key, before.quantity - quantity)
        const released = adjustAvailability(rows, before.medicationActiveIngredientId, key, before.quantity)
        return adjustAvailability(released, ingredientId, key, -quantity)
      })
      // The old batches are released; the new ones are chosen by the server.
      editStock(queryClient, list => applyAllocations(list, before.allocations, key, 'release'))
    }
    const allocations = reallocates ? [] : before.allocations

    if (vars.status === 'DISPENSED')
      editStock(queryClient, list => applyAllocations(list, allocations, `${key}:dispense`, 'dispense'))
    if (vars.status === 'CANCELLED') {
      editAvailability(queryClient, rows => adjustAvailability(rows, ingredientId, `${key}:cancel`, quantity))
      editStock(queryClient, list => applyAllocations(list, allocations, `${key}:cancel`, 'release'))
    }

    const current = findOrder(queryClient, vars.id)
    if (current) {
      placeOrderInLists(queryClient, {
        ...current,
        medicationActiveIngredientId: ingredientId,
        activeIngredientName: vars.ingredientName ?? current.activeIngredientName,
        quantity,
        dosageInstructions: vars.dosageInstructions ?? current.dosageInstructions,
        remarks: vars.remarks !== undefined ? vars.remarks : current.remarks,
        status: vars.status ?? current.status,
        dispensedById: vars.status === 'DISPENSED' ? (vars.dispensedById ?? null) : current.dispensedById,
        dispensedAt: vars.status === 'DISPENSED' ? (vars.dispensedAt ?? current.dispensedAt) : current.dispensedAt,
        // A cancelled order holds nothing; a dispensed one keeps what it took.
        allocations: vars.status === 'CANCELLED' ? [] : allocations,
      })
    }

    editPharmacyQueue(queryClient, queue => patchOrderInPharmacyQueue(queue, vars.id, {
      status: vars.status,
      dosageInstructions: vars.dosageInstructions,
      medicationName: vars.ingredientName,
      quantity: vars.quantity,
      allocations: reallocates ? [] : undefined,
    }))

    if (vars.visitId && vars.status) {
      const dispensed = vars.status === 'DISPENSED'
      adjustVisitPharmacyJourney(queryClient, vars.visitId, `${vars.id}:${vars.status}`, journey => ({
        ...journey,
        pharmacyPending: Math.max(0, (journey.pharmacyPending ?? 0) - 1),
        ...(dispensed
          ? { pharmacyAt: vars.dispensedAt ?? new Date().toISOString() }
          : { pharmacyOrders: Math.max(0, (journey.pharmacyOrders ?? 0) - 1) }),
      }))
    }
  },
  onSuccess: (saved, _vars, queryClient) => refreshAfterOrderWrite(queryClient, saved),
})

/** Deletes an order. A pending order's reservation is released; a dispensed order cannot be deleted (409). */
export const deleteOrder = defineOfflineMutation({
  key: 'medication.order.delete',
  describe: (vars: DeleteOrderVars) => `Delete order ${vars.before.ingredientName}`,
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsOrdersDeleteDeleteEndpoint({
        ...apiConfig,
        ...requestOptions(ctx),
        path: { id: vars.id },
      }).unwrap()
    }
    catch (error) {
      // Already gone: done.
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => {
    const key = `${vars.id}:delete`
    editOrderLists(queryClient, list => list.filter(o => o.id !== vars.id))
    editPharmacyQueue(queryClient, queue => removeOrderFromPharmacyQueue(queue, vars.id))
    if (vars.before.status === 'PENDING') {
      editAvailability(queryClient, rows => adjustAvailability(rows, vars.before.medicationActiveIngredientId, key, vars.before.quantity))
      editStock(queryClient, list => applyAllocations(list, vars.before.allocations, key, 'release'))
      if (vars.visitId) {
        adjustVisitPharmacyJourney(queryClient, vars.visitId, key, journey => ({
          ...journey,
          pharmacyPending: Math.max(0, (journey.pharmacyPending ?? 0) - 1),
          pharmacyOrders: Math.max(0, (journey.pharmacyOrders ?? 0) - 1),
        }))
      }
    }
  },
  onSuccess: (_saved, _vars, queryClient) => refreshAfterOrderWrite(queryClient),
})

/** The snapshot `updateOrder` and `deleteOrder` need, from an order as listed. */
export function orderSnapshot(order: Pick<MedicationOrder, 'medicationActiveIngredientId' | 'activeIngredientName' | 'quantity' | 'allocations'>): OrderSnapshot {
  return {
    medicationActiveIngredientId: order.medicationActiveIngredientId,
    ingredientName: order.activeIngredientName,
    quantity: order.quantity,
    allocations: [...order.allocations],
  }
}
