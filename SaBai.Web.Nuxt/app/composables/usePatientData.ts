import { useQuery } from "@tanstack/vue-query";
import type { MaybeRefOrGetter } from "vue";
import {
  saBaiAPIEndpointsPatientsGetEndpoint,
  saBaiAPIEndpointsPatientsListGetEndpoint,
  saBaiAPIEndpointsPatientsVisitsGetEndpoint,
  saBaiAPIEndpointsPatientsWithLatestVisitGetEndpoint,
} from "@sabai/sdk/clients";

import type {
  SaBaiAPIEndpointsPatientsGetResponse,
  SaBaiAPIEndpointsPatientsListGetItem,
  SaBaiAPIEndpointsPatientsWithLatestVisitGetItem,
} from "@sabai/sdk/types";

export type PatientDetail = SaBaiAPIEndpointsPatientsGetResponse & { tags?: string[] };
export type PatientListItem = SaBaiAPIEndpointsPatientsListGetItem & { tags?: string[] };
export type PatientWithLatestVisitItem = SaBaiAPIEndpointsPatientsWithLatestVisitGetItem & {
  tags?: string[];
};

/** All patients (used by the patients page and the global patient search). */
export function usePatientsList() {
  return useQuery({
    queryKey: queryKeys.patientsList(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsListGetEndpoint({ ...apiConfig, signal })
        .unwrap()
        .then((x) => x as { patients: PatientListItem[] }),
  });
}

/** Patients with their latest visit's village (station worklists). */
export function usePatientsWithLatestVisit() {
  return useQuery({
    queryKey: queryKeys.patientsWithLatestVisit(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsWithLatestVisitGetEndpoint({ ...apiConfig, signal })
        .unwrap()
        .then((x) => x as { patients: PatientWithLatestVisitItem[] }),
  });
}

export function usePatient(id: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => queryKeys.patient(toValue(id) ?? 0)),
    enabled: computed(() => !!toValue(id)),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsGetEndpoint({
        ...apiConfig,
        path: { id: toValue(id)! },
        signal,
      })
        .unwrap()
        .then((x) => x as PatientDetail),
  });
}

export function usePatientVisits(id: MaybeRefOrGetter<number | null>) {
  return useQuery({
    queryKey: computed(() => queryKeys.patientVisits(toValue(id) ?? 0)),
    enabled: computed(() => !!toValue(id)),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsPatientsVisitsGetEndpoint({
        ...apiConfig,
        path: { patientId: toValue(id)! },
        signal,
      }).unwrap(),
  });
}

/** Parses a numeric route param, returning null for anything that isn't an integer. */
export function useRouteId(param: string) {
  const route = useRoute();
  return computed(() => {
    const raw = route.params[param];
    if (typeof raw !== "string") return null;
    const parsed = Number.parseInt(raw, 10);
    return Number.isNaN(parsed) ? null : parsed;
  });
}
