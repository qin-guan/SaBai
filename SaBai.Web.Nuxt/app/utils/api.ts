import { apiTransport } from "~/offline/transport";

/**
 * Shared configuration passed to every @sabai/sdk call.
 *
 * The generated SDK has no configurable client instance, so `baseURL` is
 * spread into each call. Requests go to this app's own server under `/api`,
 * which proxies them to SaBai.API (server/api/[...path].ts), so the API never
 * has to be reachable from the browser. Being same-origin, the browser sends
 * the API's HttpOnly session cookie by itself: no token is handled in JS.
 *
 * `transport` is the one place every call passes through: it times requests
 * out, reports whether the API was reached, records the server's clock and
 * applies offline-replay headers (app/offline/transport.ts).
 */
export const apiConfig = {
  baseURL: "/api",
  credentials: "same-origin" as const,
  transport: apiTransport,
};

/**
 * Query keys. Prefix-based so related data can be invalidated together
 * (e.g. invalidating ["patients"] refreshes the list, details and visits).
 */
export const queryKeys = {
  currentUser: () => ["auth", "me"] as const,
  twoFactor: () => ["auth", "two-factor"] as const,
  pushConfig: () => ["push", "config"] as const,
  staffUsers: () => ["staff-users"] as const,
  staffRoles: () => ["staff-roles"] as const,
  auditLog: () => ["audit-log"] as const,
  patients: () => ["patients"] as const,
  patientsList: () => ["patients", "list"] as const,
  patientsWithLatestVisit: () => ["patients", "with-latest-visit"] as const,
  patient: (id: string | number) => ["patients", "detail", id] as const,
  patientVisits: (id: string | number) => ["patients", "visits", id] as const,
  patientPhoto: (id: string | number) => ["patient-photo", id] as const,
  faceSearch: (image: string) => ["patient-face-search", image] as const,
  vitals: (visitId: string | number) => ["vitals", visitId] as const,
  eyesight: (visitId: string | number) => ["eyesight", visitId] as const,
  consults: (visitId: string | number) => ["consults", visitId] as const,
  consult: (id: string | number) => ["consult", id] as const,
  dental: (visitId: string | number) => ["dental", visitId] as const,
  villageCodes: () => ["village-codes"] as const,
  villageCodesList: (includeHidden: boolean) => ["village-codes", includeHidden] as const,
  medicationStock: () => ["medication-stock"] as const,
  medicationBrands: () => ["medication-brands"] as const,
  medicationBrandsWithIngredient: () => ["medication-brands", "with-ingredient"] as const,
  medicationIngredients: () => ["medication-ingredients"] as const,
  pharmacyOrders: () => ["pharmacy-orders"] as const,
  broadcasts: () => ["broadcasts"] as const,
  referrals: () => ["referrals"] as const,
  referral: (id: string | number) => ["referrals", id] as const,
  referralsByConsult: (consultId: string | number) => ["referrals", "consult", consultId] as const,
  referralPartners: () => ["referral-partners"] as const,
  tags: () => ["tags"] as const,
};

/** Converts a captured data URL (e.g. from the webcam) into a `File` for multipart upload. */
export async function dataUrlToFile(dataUrl: string, fileName = "patient"): Promise<File> {
  const blob = await (await fetch(dataUrl)).blob();
  const extension = blob.type.split("/")[1]?.split("+")[0] ?? "jpg";
  const name = fileName.includes(".") ? fileName : `${fileName}.${extension}`;
  return new File([blob], name, { type: blob.type });
}
