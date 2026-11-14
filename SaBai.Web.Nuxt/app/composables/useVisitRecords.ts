import { useQuery } from '@tanstack/vue-query'
import type { MaybeRefOrGetter } from 'vue'
import {
  fetchConsult,
  fetchVisitConsults,
  fetchVisitDental,
  fetchVisitEyesight,
  fetchVisitVitals,
} from '~/offline/mutations/visitRecords'

/*
 * Per-visit clinical records. The API answers 400/404 when a visit has no
 * record yet — that is the normal "empty form" state, so these never retry and
 * a missing record surfaces as `data === undefined`. Records already fetched
 * (or saved on this device) are served from the cache, also offline.
 */

const hasId = (id: MaybeRefOrGetter<string | null | undefined>) =>
  computed(() => {
    const value = toValue(id)
    return typeof value === 'string' && value.trim().length > 0
  })

export function useVisitVitals(visitId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.vitals(toValue(visitId))),
    enabled: hasId(visitId),
    retry: false,
    queryFn: ({ signal }) => fetchVisitVitals(toValue(visitId), signal),
  })
}

export function useVisitEyesight(visitId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.eyesight(toValue(visitId))),
    enabled: hasId(visitId),
    retry: false,
    queryFn: ({ signal }) => fetchVisitEyesight(toValue(visitId), signal),
  })
}

export function useVisitConsults(visitId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.consults(toValue(visitId))),
    enabled: hasId(visitId),
    queryFn: ({ signal }) => fetchVisitConsults(toValue(visitId), signal),
  })
}

export function useConsult(consultId: MaybeRefOrGetter<string | null | undefined>) {
  return useQuery({
    queryKey: computed(() => queryKeys.consult(toValue(consultId) ?? '')),
    enabled: hasId(consultId),
    retry: false,
    queryFn: ({ signal }) => fetchConsult(toValue(consultId)!, signal),
  })
}

export function useVisitDental(visitId: MaybeRefOrGetter<string>) {
  return useQuery({
    queryKey: computed(() => queryKeys.dental(toValue(visitId))),
    enabled: hasId(visitId),
    retry: false,
    queryFn: ({ signal }) => fetchVisitDental(toValue(visitId), signal),
  })
}
