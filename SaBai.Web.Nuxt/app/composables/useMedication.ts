import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import {
  fetchIngredientAvailability,
  fetchMedicationBrands,
  fetchMedicationBrandsWithIngredient,
  fetchMedicationIngredients,
  fetchMedicationStock,
  fetchOrders,
  fetchPharmacyOrders,
  fetchMedicationHistory,
  fetchOrderAllocationOptions,
  fetchStockChanges,
  createOrder,
  updateOrder,
  deleteOrder,
} from '~/offline/mutations/medication'

export {
  createOrder,
  updateOrder,
  deleteOrder,
  orderSnapshot,
  type IngredientAvailability,
  type MedicationStockLine,
  type MedicationBrand,
  type MedicationBrandWithIngredient,
  type MedicationIngredient,
  type MedicationOrder,
  type OrderStatusKey,
  type PharmacyPatientOrders,
  type PharmacyOrderItem,
  type MedicationHistoryEntry,
  type CreateOrderContext,
  type CreateOrderVars,
  type UpdateOrderVars,
  type DeleteOrderVars,
  type StockChange,
  type OrderAllocationOptions,
  type OrderBatchOption,
} from '~/offline/mutations/medication'

/**
 * Medication reads. Served from the cache (also offline) and refreshed from the
 * API; the offline writes that edit these same queries live in
 * `app/offline/mutations/medication.ts`.
 */

export function useMedicationStock() {
  return useQuery({
    queryKey: queryKeys.medicationStock(),
    queryFn: ({ signal }) => fetchMedicationStock(signal),
  })
}

/** What can still be ordered of every active ingredient (doctors and pharmacists may read it). */
export function useIngredientAvailability(options: { enabled?: MaybeRefOrGetter<boolean> } = {}) {
  return useQuery({
    queryKey: queryKeys.medicationAvailability(),
    queryFn: ({ signal }) => fetchIngredientAvailability(signal),
    enabled: () => toValue(options.enabled) ?? true,
  })
}

export function useMedicationBrands(options: { enabled?: MaybeRefOrGetter<boolean> } = {}) {
  return useQuery({
    queryKey: queryKeys.medicationBrands(),
    queryFn: ({ signal }) => fetchMedicationBrands(signal),
    enabled: () => toValue(options.enabled) ?? true,
  })
}

export function useMedicationBrandsWithIngredient() {
  return useQuery({
    queryKey: queryKeys.medicationBrandsWithIngredient(),
    queryFn: ({ signal }) => fetchMedicationBrandsWithIngredient(signal),
  })
}

export function useMedicationIngredients() {
  return useQuery({
    queryKey: queryKeys.medicationIngredients(),
    queryFn: ({ signal }) => fetchMedicationIngredients(signal),
  })
}

export function useOrders(
  status?: MaybeRefOrGetter<string | undefined>,
  options: { enabled?: MaybeRefOrGetter<boolean> } = {},
) {
  return useQuery({
    queryKey: computed(() => queryKeys.ordersList(toValue(status))),
    queryFn: ({ signal }) => fetchOrders(toValue(status), signal),
    enabled: () => toValue(options.enabled) ?? true,
  })
}

export function usePharmacyOrders() {
  return useQuery({
    queryKey: queryKeys.pharmacyOrders(),
    queryFn: ({ signal }) => fetchPharmacyOrders(signal),
  })
}

export function useMedicationHistory(brandId: MaybeRefOrGetter<string | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.medicationHistory(toValue(brandId) ?? '')),
    queryFn: ({ signal }) => fetchMedicationHistory(toValue(brandId)!, signal),
    enabled: () => !!toValue(brandId),
  })
}

/**
 * A batch's change log, newest first: only the dispensings (`dispensedOnly`)
 * or every change. Always refetched when opened, so it is never an old copy.
 */
export function useStockChanges(
  stockId: MaybeRefOrGetter<string | null | undefined>,
  dispensedOnly: MaybeRefOrGetter<boolean>,
) {
  return useQuery({
    queryKey: computed(() => queryKeys.stockChangesList(toValue(stockId) ?? '', toValue(dispensedOnly))),
    queryFn: ({ signal }) => fetchStockChanges(toValue(stockId)!, toValue(dispensedOnly), signal),
    enabled: () => !!toValue(stockId),
    staleTime: 0,
  })
}

/**
 * The batches a pending order can be dispensed from. Not kept between
 * openings (`gcTime: 0`) or saved for offline use: other orders move the
 * numbers, so the dialog must start from what the server says now.
 */
export function useOrderAllocationOptions(orderId: MaybeRefOrGetter<string | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.orderAllocationOptions(toValue(orderId) ?? '')),
    queryFn: ({ signal }) => fetchOrderAllocationOptions(toValue(orderId)!, signal),
    enabled: () => !!toValue(orderId),
    staleTime: 0,
    gcTime: 0,
    retry: false,
    meta: { persist: false },
  })
}

export function useCreateOrder() {
  return useOfflineMutation(createOrder)
}

export function useUpdateOrder() {
  return useOfflineMutation(updateOrder)
}

export function useDeleteOrder() {
  return useOfflineMutation(deleteOrder)
}
