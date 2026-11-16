import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import { computed, toValue } from 'vue'
import {
  fetchMedicationBrands,
  fetchMedicationBrandsWithIngredient,
  fetchMedicationIngredients,
  fetchMedicationStock,
  fetchOrders,
  fetchPharmacyOrders,
  fetchMedicationHistory,
  createOrder,
  updateOrder,
  deleteOrder,
} from '~/offline/mutations/medication'

export {
  createOrder,
  updateOrder,
  deleteOrder,
  type MedicationStockLine,
  type MedicationBrand,
  type MedicationBrandWithIngredient,
  type MedicationIngredient,
  type MedicationOrder,
  type PharmacyPatientOrders,
  type MedicationHistoryEntry,
  type CreateOrderContext,
  type CreateOrderVars,
  type UpdateOrderVars,
  type DeleteOrderVars,
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

export function useMedicationBrands() {
  return useQuery({
    queryKey: queryKeys.medicationBrands(),
    queryFn: ({ signal }) => fetchMedicationBrands(signal),
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

export function useOrders(status?: MaybeRefOrGetter<string | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.ordersList(toValue(status))),
    queryFn: ({ signal }) => fetchOrders(toValue(status), signal),
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

export function useCreateOrder() {
  return useOfflineMutation(createOrder)
}

export function useUpdateOrder() {
  return useOfflineMutation(updateOrder)
}

export function useDeleteOrder() {
  return useOfflineMutation(deleteOrder)
}


