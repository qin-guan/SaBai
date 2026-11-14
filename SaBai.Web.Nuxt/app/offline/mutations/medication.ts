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
} from '@sabai/sdk/clients'
import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationBrandsListGetResponse,
  SaBaiAPIEndpointsMedicationBrandsWithIngredientGetResponse,
  SaBaiAPIEndpointsMedicationIngredientsListGetResponse,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
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

// The consult form picks medication offline, so a device needs all four lists.
registerOfflinePrefetch('medication', async (queryClient) => {
  await Promise.all([
    queryClient.fetchQuery({ queryKey: queryKeys.medicationIngredients(), queryFn: ({ signal }) => fetchMedicationIngredients(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrands(), queryFn: ({ signal }) => fetchMedicationBrands(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationBrandsWithIngredient(), queryFn: ({ signal }) => fetchMedicationBrandsWithIngredient(signal) }),
    queryClient.fetchQuery({ queryKey: queryKeys.medicationStock(), queryFn: ({ signal }) => fetchMedicationStock(signal) }),
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
