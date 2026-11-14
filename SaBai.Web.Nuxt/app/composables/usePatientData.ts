import { useQuery } from "@tanstack/vue-query";
import type { MaybeRefOrGetter } from "vue";
import {
  fetchPatient,
  fetchPatientsList,
  fetchPatientsWithLatestVisit,
  fetchPatientVisits,
} from "~/offline/mutations/patients";

export type {
  PatientDetail,
  PatientListItem,
  PatientWithLatestVisitItem,
} from "~/offline/mutations/patients";

/**
 * Patient reads. All of them are served from the persisted query cache (also
 * offline) and refreshed from the API; patients and visits created on this
 * device while offline are added to these same queries (see
 * `app/offline/mutations/patients.ts`).
 */

/** All patients (used by the patients page and the global patient search). */
export function usePatientsList() {
  return useQuery({
    queryKey: queryKeys.patientsList(),
    queryFn: ({ signal }) => fetchPatientsList(signal),
  });
}

/** Patients with their latest visit's village (station worklists). */
export function usePatientsWithLatestVisit() {
  return useQuery({
    queryKey: queryKeys.patientsWithLatestVisit(),
    queryFn: ({ signal }) => fetchPatientsWithLatestVisit(signal),
  });
}

export function usePatient(id: MaybeRefOrGetter<string | null>) {
  return useQuery({
    queryKey: computed(() => queryKeys.patient(toValue(id) ?? "")),
    enabled: computed(() => !!toValue(id)),
    queryFn: ({ signal }) => fetchPatient(toValue(id)!, signal),
  });
}

export function usePatientVisits(id: MaybeRefOrGetter<string | null>) {
  return useQuery({
    queryKey: computed(() => queryKeys.patientVisits(toValue(id) ?? "")),
    enabled: computed(() => !!toValue(id)),
    queryFn: ({ signal }) => fetchPatientVisits(toValue(id)!, signal),
  });
}

/** Reads a route param (a UUID), returning null when it is missing. */
export function useRouteId(param: string) {
  const route = useRoute();
  return computed(() => {
    const raw = route.params[param];
    if (typeof raw !== "string") return null;
    const trimmed = raw.trim();
    return trimmed.length > 0 ? trimmed : null;
  });
}
