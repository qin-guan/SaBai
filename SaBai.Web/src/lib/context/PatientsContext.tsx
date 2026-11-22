import { createContext, useContext, useMemo, ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { saBaiAPIEndpointsPatientsListGetEndpointQueryOptions } from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsPatientsListGetItem } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";

type PatientsById = Record<string, SaBaiAPIEndpointsPatientsListGetItem>;

interface PatientsContextType {
  patientsById: PatientsById;
  isLoading: boolean;
  isRefetching: boolean;
}

const PatientsContext = createContext<PatientsContextType | undefined>(
  undefined,
);

export function PatientsProvider({ children }: { children: ReactNode }) {
  const { data, isLoading, isRefetching } = useQuery(
    saBaiAPIEndpointsPatientsListGetEndpointQueryOptions({}, apiConfig),
  );

  // put patients into object for quick access and mutation by id
  const patientsById = useMemo<PatientsById>(() => {
    return (data?.patients ?? []).reduce<PatientsById>(
      (accumulator, patient) => {
        accumulator[patient.id] = patient;
        return accumulator;
      },
      {},
    );
  }, [data]);

  return (
    <PatientsContext.Provider value={{ patientsById, isLoading, isRefetching }}>
      {children}
    </PatientsContext.Provider>
  );
}

/**
 * The patient's display code (e.g. "PC0012"), from the patient list when it
 * is loaded; undefined outside a `PatientsProvider` or before it loads.
 */
export function usePatientDisplayId(patientId: string | null | undefined) {
  return usePatientDisplayIds()(patientId);
}

/** {@link usePatientDisplayId} for lists: looks up any number of patients. */
export function usePatientDisplayIds() {
  const context = useContext(PatientsContext);
  return (patientId: string | null | undefined) =>
    patientId ? context?.patientsById[patientId]?.displayId : undefined;
}

export function usePatients() {
  const context = useContext(PatientsContext);
  if (context === undefined) {
    throw new Error("usePatients must be used within PatientsProvider");
  }
  return context;
}
