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
  createOrder,
  updateOrder,
  deleteOrder,
  PHARMACY_ROLES,
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

/**
 * Brand names by active ingredient id, so the order picker can be searched by
 * brand. The API only lets the pharmacy roles list brands (`PHARMACY_ROLES`);
 * for anyone else (a doctor-only account) the map stays empty and the picker
 * searches ingredient names alone.
 */
export function useOrderableBrandNames() {
  const { user } = useCurrentUser()
  const canReadBrands = computed(() => !!user.value?.roles.some(role => PHARMACY_ROLES.includes(role)))
  const { data } = useMedicationBrands({ enabled: canReadBrands })
  return computed(() => brandNamesByIngredient(canReadBrands.value ? data.value ?? [] : []))
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

export function useCreateOrder() {
  return useOfflineMutation(createOrder)
}

export function useUpdateOrder() {
  return useOfflineMutation(updateOrder)
}

export function useDeleteOrder() {
  return useOfflineMutation(deleteOrder)
}
