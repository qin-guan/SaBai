import type {
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
  SaBaiAPIDomainStockStatusKey,
} from "@sabai/sdk/types";
import { saBaiAPIDomainStockStatus } from "@sabai/sdk/types";
import { z } from "zod";

export type StockStatus = SaBaiAPIDomainStockStatusKey;

export const stockStatusDropdown = Object.values(saBaiAPIDomainStockStatus).map(
  (status) => ({
    label: status,
    value: status,
  }),
);

export type CreateFormFields = {
  medicationBrandId: number;
  quantity: number;
  expiry: string;
  location: string;
  stockStatus: StockStatus;
  remarks?: string;
};

export type MedicationStockWithBrandAndActiveIngredient =
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse;

const stockStatusValues = Object.values(saBaiAPIDomainStockStatus) as [
  StockStatus,
  ...StockStatus[],
];

export const splitSchema = z.object({
  quantity: z.number().int().positive(),
  location: z.string(),
  stockStatus: z.enum(stockStatusValues),
  remarks: z.string().optional(),
});

export type SplitPayload = z.infer<typeof splitSchema>;
