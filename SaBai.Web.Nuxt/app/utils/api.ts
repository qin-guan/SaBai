/**
 * Shared configuration passed to every @sabai/sdk call.
 *
 * The generated SDK has no configurable client instance, so `baseURL` is
 * spread into each call. Requests go to this app's own server under `/api`,
 * which proxies them to SaBai.API (server/api/[...path].ts), so the API never
 * has to be reachable from the browser. Being same-origin, the browser sends
 * the API's HttpOnly session cookie by itself: no token is handled in JS.
 */
export const apiConfig = {
  baseURL: "/api",
  credentials: "same-origin" as const,
};

/**
 * Query keys. Prefix-based so related data can be invalidated together
 * (e.g. invalidating ["patients"] refreshes the list, details and visits).
 */
export const queryKeys = {
  currentUser: () => ["auth", "me"] as const,
  twoFactor: () => ["auth", "two-factor"] as const,
  staffUsers: () => ["staff-users"] as const,
  staffRoles: () => ["staff-roles"] as const,
  auditLog: () => ["audit-log"] as const,
  patients: () => ["patients"] as const,
  patientsList: () => ["patients", "list"] as const,
  patientsWithLatestVisit: () => ["patients", "with-latest-visit"] as const,
  patient: (id: number) => ["patients", "detail", id] as const,
  patientVisits: (id: number) => ["patients", "visits", id] as const,
  patientPhoto: (id: number) => ["patient-photo", id] as const,
  faceSearch: (image: string) => ["patient-face-search", image] as const,
  vitals: (visitId: number) => ["vitals", visitId] as const,
  eyesight: (visitId: number) => ["eyesight", visitId] as const,
  consults: (visitId: number) => ["consults", visitId] as const,
  dental: (visitId: number) => ["dental", visitId] as const,
  villageCodes: () => ["village-codes"] as const,
  villageCodesList: (includeHidden: boolean) => ["village-codes", includeHidden] as const,
  medicationStock: () => ["medication-stock"] as const,
  medicationBrands: () => ["medication-brands"] as const,
  medicationBrandsWithIngredient: () => ["medication-brands", "with-ingredient"] as const,
  medicationIngredients: () => ["medication-ingredients"] as const,
};

/** Converts a captured data URL (e.g. from the webcam) into a `File` for multipart upload. */
export async function dataUrlToFile(dataUrl: string, fileName = "patient"): Promise<File> {
  const blob = await (await fetch(dataUrl)).blob();
  const extension = blob.type.split("/")[1]?.split("+")[0] ?? "jpg";
  const name = fileName.includes(".") ? fileName : `${fileName}.${extension}`;
  return new File([blob], name, { type: blob.type });
}
