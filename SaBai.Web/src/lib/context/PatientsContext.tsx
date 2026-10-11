import { createContext, useContext, useMemo, ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { saBaiAPIEndpointsPatientsListGetEndpointQueryOptions } from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsPatientsListGetItem } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";

type PatientsById = Record<number, SaBaiAPIEndpointsPatientsListGetItem>;

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

  const patients = data?.patients ?? [];

  // put patients into object for quick access and mutation by id
  const patientsById = useMemo<PatientsById>(() => {
    return patients.reduce<PatientsById>((accumulator, patient) => {
      accumulator[patient.id] = patient;
      return accumulator;
    }, {});
  }, [patients]);

  return (
    <PatientsContext.Provider value={{ patientsById, isLoading, isRefetching }}>
      {children}
    </PatientsContext.Provider>
  );
}

export function usePatients() {
  const context = useContext(PatientsContext);
  if (context === undefined) {
    throw new Error("usePatients must be used within PatientsProvider");
  }
  return context;
}
