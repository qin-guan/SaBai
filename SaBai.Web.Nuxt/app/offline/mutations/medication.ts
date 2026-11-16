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
import { CLINIC_STAFF_ROLES, registerOfflinePrefetch } from '~/offline/prefetch'
import { apiConfig, queryKeys } from '~/utils/api'
import { getApiErrorStatus } from '~/utils/apiErrors'
import {
  applySplit,
  patchStock,
  upsertById,
  upsertStock,
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
 */

export type MedicationIngredient = SaBaiAPIEndpointsMedicationIngredientsListGetResponse
export type MedicationBrand = SaBaiAPIEndpointsMedicationBrandsListGetResponse
export type MedicationBrandWithIngredient = SaBaiAPIEndpointsMedicationBrandsWithIngredientGetResponse
export type MedicationStockLine = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse
export type StockStatusKey = SaBaiAPIDomainStockStatusKey

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

export function fetchOrders(status?: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsOrdersListGetEndpoint({ ...apiConfig, query: status ? { status } : undefined, signal }).unwrap()
}

export function fetchPharmacyOrders(signal?: AbortSignal) {
  return saBaiAPIEndpointsPharmacyOrdersGetEndpoint({ ...apiConfig, signal }).unwrap()
}

export function fetchMedicationHistory(brandId: string, signal?: AbortSignal) {
  return saBaiAPIEndpointsMedicationHistoryGetEndpoint({ ...apiConfig, query: { brandId }, signal }).unwrap()
}

// The consult form picks medication offline, so a device needs all four lists.
registerOfflinePrefetch('medication', async (queryClient) => {
  await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.medicationIngredients(), queryFn: ({ signal }) => fetchMedicationIngredients(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrands(), queryFn: ({ signal }) => fetchMedicationBrands(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrandsWithIngredient(), queryFn: ({ signal }) => fetchMedicationBrandsWithIngredient(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationStock(), queryFn: ({ signal }) => fetchMedicationStock(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.ordersList(), queryFn: ({ signal }) => fetchOrders(undefined, signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.pharmacyOrders(), queryFn: ({ signal }) => fetchPharmacyOrders(signal) }),
  ])
}, { roles: CLINIC_STAFF_ROLES })

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
    location: vars.location,
    stockStatus: vars.stockStatus,
    remarks: vars.remarks,
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
 * Splits a lot into child lots. Idempotent by construction: the parent ends at
 * zero quantity and each child line is upserted under the client id in
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

// ── Orders & Pharmacy Queue ──────────────────────────────────────────────

import {
  upsertOrderInPharmacyQueue,
  patchOrderInPharmacyQueue,
  removeOrderFromPharmacyQueue,
  deductStockFifo,
} from '~/utils/medicationCache'

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
  medicationBrandId: string
  brandName: string
  quantity: number
  dosageInstructions: string
  remarks: string | null
  createdAt?: string
  context?: CreateOrderContext
}

export interface UpdateOrderVars {
  id: string
  status: OrderStatusKey
  dosageInstructions?: string | null
  remarks?: string | null
  medicationBrandId?: string
  brandName?: string
  quantity?: number
  visitId?: string
  patientName?: string
  approvedById?: string | null
  approvedAt?: string | null
  /** Snapshot of active stock lots for this brand when the user approved, so optimistic FIFO deduction is idempotent. */
  stockSnapshot?: MedicationStockLine[]
}

export interface DeleteOrderVars {
  id: string
  brandName?: string
  visitId?: string
}

const journeyAppliedActions = new WeakMap<object, Set<string>>()

function adjustVisitPharmacyJourney(
  queryClient: QueryClient,
  visitId: string,
  actionKey: string,
  adjust: (journey: { pharmacyPending: number; pharmacyOrders: number; pharmacyAt?: string | null }) => any,
) {
  queryClient.setQueriesData<{ visits?: Array<{ id: string; journey?: any }> }>(
    { queryKey: ['patients', 'visits'] },
    (old) => {
      if (!old || !Array.isArray(old.visits)) return old
      if (!old.visits.some(v => v.id === visitId && v.journey)) return old
      return {
        ...old,
        visits: old.visits.map(v => {
          if (v.id === visitId && v.journey) {
            const journey = v.journey
            let set = journeyAppliedActions.get(journey)
            if (!set) {
              set = new Set()
              journeyAppliedActions.set(journey, set)
            }
            if (set.has(actionKey)) return v
            const nextSet = new Set(set)
            nextSet.add(actionKey)
            const updatedJourney = adjust(journey)
            journeyAppliedActions.set(updatedJourney, nextSet)
            return { ...v, journey: updatedJourney }
          }
          return v
        }),
      }
    },
  )

  queryClient.setQueryData<{ patients?: Array<{ id: string; lastVisitId?: string | null; lastVisitJourney?: any }> }>(
    queryKeys.patientsWithLatestVisit(),
    (old) => {
      if (!old || !Array.isArray(old.patients)) return old
      if (!old.patients.some(p => p.lastVisitId === visitId && p.lastVisitJourney)) return old
      return {
        ...old,
        patients: old.patients.map(p => {
          if (p.lastVisitId === visitId && p.lastVisitJourney) {
            const journey = p.lastVisitJourney
            let set = journeyAppliedActions.get(journey)
            if (!set) {
              set = new Set()
              journeyAppliedActions.set(journey, set)
            }
            if (set.has(actionKey)) return p
            const nextSet = new Set(set)
            nextSet.add(actionKey)
            const updatedJourney = adjust(journey)
            journeyAppliedActions.set(updatedJourney, nextSet)
            return { ...p, lastVisitJourney: updatedJourney }
          }
          return p
        }),
      }
    },
  )
}

export const createOrder = defineOfflineMutation({
  key: 'medication.order.create',
  creates: true,
  describe: (vars: CreateOrderVars) => `Order ${vars.brandName} × ${vars.quantity}${vars.context?.patientName ? ` for ${vars.context.patientName}` : ''}`,
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.consultId, vars.medicationBrandId, ...(vars.context?.visitId ? [vars.context.visitId] : []), ...(vars.context?.patientId ? [vars.context.patientId] : [])],
  send: (vars, ctx) =>
    saBaiAPIEndpointsOrdersCreatePostEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      body: {
        id: vars.id,
        consultId: vars.consultId,
        medicationBrandId: vars.medicationBrandId,
        quantity: vars.quantity,
        dosageInstructions: vars.dosageInstructions,
        remarks: vars.remarks,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const existing = queryClient.getQueryData<MedicationOrder[]>(queryKeys.ordersList())?.find(o => o.id === vars.id)
    const order: MedicationOrder = {
      id: vars.id,
      consultId: vars.consultId,
      medicationBrandId: vars.medicationBrandId,
      brandName: vars.brandName,
      quantity: vars.quantity,
      dosageInstructions: vars.dosageInstructions,
      remarks: vars.remarks,
      status: 'PENDING',
      createdAt: vars.createdAt ?? existing?.createdAt ?? new Date().toISOString(),
      approvedById: null,
      approvedAt: null,
    }

    // Upsert into orders list & PENDING orders list
    queryClient.setQueryData<MedicationOrder[]>(queryKeys.ordersList(), old => upsertById(old ?? [], order, 'start'))
    queryClient.setQueryData<MedicationOrder[]>(queryKeys.ordersList('PENDING'), old => upsertById(old ?? [], order, 'start'))

    const c = vars.context
    if (c) {
      queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old =>
        upsertOrderInPharmacyQueue(old ?? [], {
          orderId: vars.id,
          medicationName: vars.brandName,
          quantity: vars.quantity,
          isLowStock: c.isLowStock,
          dosageInstructions: vars.dosageInstructions,
          patientDisplayId: c.patientDisplayId,
          patientName: c.patientName,
          villageCode: c.villageCode,
          visitId: c.visitId,
          visitDate: c.visitDate,
          diagnoses: c.diagnoses,
        }),
      )
      adjustVisitPharmacyJourney(queryClient, c.visitId, `${vars.id}:create`, journey => ({
        ...journey,
        pharmacyPending: (journey.pharmacyPending ?? 0) + 1,
        pharmacyOrders: (journey.pharmacyOrders ?? 0) + 1,
      }))
    }
  },
  revert: (vars, queryClient) => {
    queryClient.setQueryData<MedicationOrder[]>(queryKeys.ordersList(), old => old?.filter(o => o.id !== vars.id))
    queryClient.setQueryData<MedicationOrder[]>(queryKeys.ordersList('PENDING'), old => old?.filter(o => o.id !== vars.id))
    queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old => old ? removeOrderFromPharmacyQueue(old, vars.id) : old)
    if (vars.context?.visitId) {
      adjustVisitPharmacyJourney(queryClient, vars.context.visitId, `${vars.id}:create:revert`, journey => ({
        ...journey,
        pharmacyPending: Math.max(0, (journey.pharmacyPending ?? 0) - 1),
        pharmacyOrders: Math.max(0, (journey.pharmacyOrders ?? 0) - 1),
      }))
    }
  },
  onSuccess: (_saved, _vars, queryClient) => {
    queryClient.invalidateQueries({ queryKey: queryKeys.orders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.pharmacyOrders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  },
})

export const updateOrder = defineOfflineMutation({
  key: 'medication.order.update',
  describe: (vars: UpdateOrderVars) => vars.status === 'APPROVED' ? `Dispense ${vars.brandName ?? 'order'}${vars.patientName ? ` for ${vars.patientName}` : ''}` : vars.status === 'CANCELLED' ? `Cancel order ${vars.brandName ?? ''}`.trim() : `Edit order ${vars.brandName ?? ''}`.trim(),
  recordIds: vars => [vars.id],
  dependsOn: vars => [vars.id],
  send: (vars, ctx) =>
    saBaiAPIEndpointsOrdersUpdatePatchEndpoint({
      ...apiConfig,
      ...requestOptions(ctx),
      path: { id: vars.id },
      body: {
        status: vars.status,
        dosageInstructions: vars.dosageInstructions ?? undefined,
        remarks: vars.remarks ?? undefined,
      },
    }).unwrap(),
  optimistic: (vars, queryClient) => {
    const patch: Partial<MedicationOrder> = {
      status: vars.status,
    }
    if (vars.dosageInstructions != null) patch.dosageInstructions = vars.dosageInstructions
    if (vars.remarks !== undefined) patch.remarks = vars.remarks
    if (vars.status === 'APPROVED') {
      patch.approvedAt = vars.approvedAt ?? new Date().toISOString()
    }

    queryClient.setQueriesData<MedicationOrder[]>({ queryKey: ['orders', 'list'] }, old => {
      if (!old) return old
      return old.map(o => {
        if (o.id === vars.id) {
          return { ...o, ...patch }
        }
        return o
      }).filter(o => {
        // If queryKey is status-specific (e.g. ['orders', 'list', 'PENDING']), filter out if status changed
        // but wait, setQueriesData with queryKey: ['orders', 'list'] matches all starting with ['orders', 'list']
        return true
      })
    })

    // Also specifically handle status-filtered lists if cached
    for (const statusKey of ['PENDING', 'APPROVED', 'CANCELLED']) {
      const qKey = queryKeys.ordersList(statusKey)
      queryClient.setQueryData<MedicationOrder[]>(qKey, old => {
        if (!old) return old
        return old.map(o => o.id === vars.id ? { ...o, ...patch } : o).filter(o => o.status === statusKey)
      })
    }

    queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old =>
      old ? patchOrderInPharmacyQueue(old, vars.id, { status: vars.status, dosageInstructions: vars.dosageInstructions }) : old,
    )

    if (vars.status === 'APPROVED' && vars.medicationBrandId && vars.quantity) {
      editStock(queryClient, list => deductStockFifo(list, vars.medicationBrandId!, vars.quantity!, vars.stockSnapshot))
    }

    if (vars.visitId && (vars.status === 'APPROVED' || vars.status === 'CANCELLED')) {
      const isApproved = vars.status === 'APPROVED'
      adjustVisitPharmacyJourney(queryClient, vars.visitId, `${vars.id}:${vars.status}`, journey => ({
        ...journey,
        pharmacyPending: Math.max(0, (journey.pharmacyPending ?? 0) - 1),
        ...(isApproved ? { pharmacyAt: vars.approvedAt ?? new Date().toISOString() } : { pharmacyOrders: Math.max(0, (journey.pharmacyOrders ?? 0) - 1) }),
      }))
    }
  },
  onSuccess: (_saved, _vars, queryClient) => {
    queryClient.invalidateQueries({ queryKey: queryKeys.orders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.pharmacyOrders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.medicationStock() })
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  },
})

export const deleteOrder = defineOfflineMutation({
  key: 'medication.order.delete',
  describe: (vars: DeleteOrderVars) => `Delete order ${vars.brandName ?? ''}`.trim(),
  recordIds: vars => [vars.id],
  send: async (vars, ctx) => {
    try {
      await saBaiAPIEndpointsOrdersDeleteDeleteEndpoint({
        ...apiConfig,
        ...requestOptions(ctx),
        path: { id: vars.id },
      }).unwrap()
    } catch (error) {
      if (getApiErrorStatus(error) !== 404) throw error
    }
  },
  optimistic: (vars, queryClient) => {
    queryClient.setQueriesData<MedicationOrder[]>({ queryKey: ['orders', 'list'] }, old =>
      old?.filter(o => o.id !== vars.id))
    queryClient.setQueryData<PharmacyPatientOrders[]>(queryKeys.pharmacyOrders(), old =>
      old ? removeOrderFromPharmacyQueue(old, vars.id) : old)
  },
  onSuccess: (_saved, _vars, queryClient) => {
    queryClient.invalidateQueries({ queryKey: queryKeys.orders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.pharmacyOrders() })
    queryClient.invalidateQueries({ queryKey: queryKeys.patients() })
  },
})

