import {
  createContext,
  useContext,
  ReactNode,
  useCallback,
  useEffect,
} from "react";
import { useQuery } from "@tanstack/react-query";
import { saBaiAPIEndpointsVillageCodesListGetEndpointQueryOptions } from "@sabai/sdk/hooks";
import type { SaBaiAPIEndpointsVillageCodesListGetItem } from "@sabai/sdk/types";
import { apiConfig } from "@/lib/api/config";
import { useSaveOnWrite } from "@/hooks/useSaveOnWrite";

export type VillageCode = SaBaiAPIEndpointsVillageCodesListGetItem;

interface VillageCodeContextValue {
  selectedVillageCodeId: number | null;
  setSelectedVillageCodeId: (id: number | null) => void;
  villageCodes: VillageCode[] | undefined;
  isLoading: boolean;
  isError: boolean;
  error: unknown;
}

const VillageCodeContext = createContext<VillageCodeContextValue | undefined>(
  undefined,
);

export function VillageCodeProvider({ children }: { children: ReactNode }) {
  const { data, isLoading, isError, error } = useQuery(
    saBaiAPIEndpointsVillageCodesListGetEndpointQueryOptions(
      { query: { includeHidden: false } },
      apiConfig,
    ),
  );

  const villageCodes = data?.villageCodes;

  const [villageState, setVillageState] = useSaveOnWrite<{
    selectedVillageCodeId: number | null;
  }>("villageCodeState", { selectedVillageCodeId: null }, [], 0);

  const selectedVillageCodeId = villageState.selectedVillageCodeId;

  const setSelectedVillageCodeId = useCallback(
    (id: number | null) => {
      setVillageState({ selectedVillageCodeId: id });
    },
    [setVillageState],
  );

  // A hidden/deleted village drops out of the (visible-only) list, so clear it from storage
  useEffect(() => {
    if (
      villageCodes &&
      selectedVillageCodeId !== null &&
      !villageCodes.some((v) => v.id === selectedVillageCodeId)
    ) {
      setSelectedVillageCodeId(null);
    }
  }, [villageCodes, selectedVillageCodeId, setSelectedVillageCodeId]);

  return (
    <VillageCodeContext.Provider
      value={{
        selectedVillageCodeId,
        setSelectedVillageCodeId,
        villageCodes,
        isLoading,
        isError,
        error,
      }}
    >
      {children}
    </VillageCodeContext.Provider>
  );
}

export function useVillageCode() {
  const context = useContext(VillageCodeContext);
  if (context === undefined) {
    throw new Error("useVillageCode must be used within a VillageCodeProvider");
  }
  return context;
}
