import type { SaBaiAPIEndpointsVillageCodesCreatePostRequest } from "@sabai/sdk/types";

export const DEFAULT_FORM: SaBaiAPIEndpointsVillageCodesCreatePostRequest = {
  code: "",
  name: "",
  colorHex: "#3b82f6", // Default blue
  isVisible: true,
};

export type FormFields = {
  id?: number;
  name: string;
  code: string;
  colorHex: string;
  isVisible: boolean | undefined;
};
