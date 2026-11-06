import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import {
  saBaiAPIEndpointsConsultsListByVisitGetEndpoint,
  saBaiAPIEndpointsDentalGetByVisitGetEndpoint,
  saBaiAPIEndpointsEyesightGetByVisitGetEndpoint,
  saBaiAPIEndpointsVitalsGetByVisitGetEndpoint,
} from '@sabai/sdk/clients'

/*
 * Per-visit clinical records. The API answers 400/404 when a visit has no
 * record yet — that is the normal "empty form" state, so these never retry and
 * a missing record surfaces as `data === undefined`.
 */

const enabledFor = (visitId: MaybeRefOrGetter<number>) =>
  computed(() => Number.isFinite(toValue(visitId)) && toValue(visitId) > 0)

export function useVisitVitals(visitId: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.vitals(toValue(visitId))),
    enabled: enabledFor(visitId),
    retry: false,
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsVitalsGetByVisitGetEndpoint({ ...apiConfig, path: { visitId: toValue(visitId) }, signal }).unwrap(),
  })
}

export function useVisitEyesight(visitId: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.eyesight(toValue(visitId))),
    enabled: enabledFor(visitId),
    retry: false,
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsEyesightGetByVisitGetEndpoint({ ...apiConfig, path: { visitId: toValue(visitId) }, signal }).unwrap(),
  })
}

export function useVisitConsults(visitId: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.consults(toValue(visitId))),
    enabled: enabledFor(visitId),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsConsultsListByVisitGetEndpoint({ ...apiConfig, path: { visitId: toValue(visitId) }, signal }).unwrap(),
  })
}

export function useVisitDental(visitId: MaybeRefOrGetter<number>) {
  return useQuery({
    queryKey: computed(() => queryKeys.dental(toValue(visitId))),
    enabled: enabledFor(visitId),
    retry: false,
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsDentalGetByVisitGetEndpoint({ ...apiConfig, path: { visitId: toValue(visitId) }, signal }).unwrap(),
  })
}
