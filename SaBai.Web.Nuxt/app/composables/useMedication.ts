import { useQuery } from '@tanstack/vue-query'
import { saBaiAPIEndpointsPharmacyOrdersGetEndpoint } from '@sabai/sdk/clients'
import {
  fetchMedicationBrands,
  fetchMedicationBrandsWithIngredient,
  fetchMedicationIngredients,
  fetchMedicationStock,
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

/** Pending pharmacy orders grouped by patient and visit. */
export function usePharmacyOrders() {
  return useQuery({
    queryKey: queryKeys.pharmacyOrders(),
    queryFn: ({ signal }) => saBaiAPIEndpointsPharmacyOrdersGetEndpoint({ ...apiConfig, signal }).unwrap(),
  })
}
