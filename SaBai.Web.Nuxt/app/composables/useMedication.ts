import { useQuery } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsMedicationBrandsListGetEndpoint,
  saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpoint,
  saBaiAPIEndpointsMedicationIngredientsListGetEndpoint,
  saBaiAPIEndpointsMedicationStockWithBrandGetEndpoint,
} from '@sabai/sdk/clients'

export function useMedicationStock() {
  return useQuery({
    queryKey: queryKeys.medicationStock(),
    queryFn: ({ signal }) => saBaiAPIEndpointsMedicationStockWithBrandGetEndpoint({ ...apiConfig, signal }).unwrap(),
  })
}

export function useMedicationBrands() {
  return useQuery({
    queryKey: queryKeys.medicationBrands(),
    queryFn: ({ signal }) => saBaiAPIEndpointsMedicationBrandsListGetEndpoint({ ...apiConfig, signal }).unwrap(),
  })
}

export function useMedicationBrandsWithIngredient() {
  return useQuery({
    queryKey: queryKeys.medicationBrandsWithIngredient(),
    queryFn: ({ signal }) => saBaiAPIEndpointsMedicationBrandsWithIngredientGetEndpoint({ ...apiConfig, signal }).unwrap(),
  })
}

export function useMedicationIngredients() {
  return useQuery({
    queryKey: queryKeys.medicationIngredients(),
    queryFn: ({ signal }) => saBaiAPIEndpointsMedicationIngredientsListGetEndpoint({ ...apiConfig, signal }).unwrap(),
  })
}
