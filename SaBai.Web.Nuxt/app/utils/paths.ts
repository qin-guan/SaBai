import type { StationKey } from "./journey";

const withVisit = (path: string, visitId?: string | number) =>
  visitId === undefined ? path : `${path}?visit=${visitId}`;

export const paths = {
  home: () => "/",
  login: () => "/login",
  /** The patient list; `station` filters it to who that station is waiting for. */
  patient: (station?: StationKey) => (station ? `/patient?station=${station}` : "/patient"),
  patientDetail: (patientId: string | number) => `/patient/${patientId}`,
  scanFace: () => "/scan-face",
  vitals: () => "/vitals",
  vitalsDetail: (patientId: string | number, visitId?: string | number) => withVisit(`/vitals/${patientId}`, visitId),
  vision: () => "/vision",
  visionUpdateGlasses: (patientId: string | number, visitId?: string | number) =>
    withVisit(`/vision/update-glasses/${patientId}`, visitId),
  dental: () => "/dental",
  dentalDetail: (patientId: string | number, visitId?: string | number) => withVisit(`/dental/${patientId}`, visitId),
  consults: () => "/consults",
  consultsDetail: (patientId: string | number, visitId?: string | number, consultId?: string | number) => {
    const query = new URLSearchParams();
    if (visitId !== undefined) query.set("visit", String(visitId));
    if (consultId !== undefined) query.set("consult", String(consultId));
    const qs = query.toString();
    return qs ? `/consults/${patientId}?${qs}` : `/consults/${patientId}`;
  },
  consultPdf: (consultId: string | number) => `/api/consults/${consultId}/pdf`,
  referrals: () => "/referrals",
  referralBills: () => "/referrals/bills",
  referralsDetail: (referralId: string | number) => `/referrals/${referralId}`,
  medicationStock: () => "/medication-stock",
  medicationBrands: () => "/medication-brands",
  medicationActiveIngredients: () => "/medication-active-ingredients",
  orders: () => "/orders",
  settings: () => "/settings",
  settingsSync: () => "/settings/sync",
  settingsNotifications: () => "/settings/notifications",
  settingsVillageCodes: () => "/settings/village-codes",
  settingsReferralPartners: () => "/settings/referral-partners",
  settingsUsers: () => "/settings/users",
  /** The audit log, optionally filtered to one record's or one staff user's history. */
  settingsAuditLog: (filter?: { entityType?: string; entityId?: string | number; userId?: string }) => {
    const query = new URLSearchParams();
    if (filter?.entityType) query.set("entityType", filter.entityType);
    if (filter?.entityId !== undefined) query.set("entityId", String(filter.entityId));
    if (filter?.userId) query.set("userId", filter.userId);
    const qs = query.toString();
    return qs ? `/settings/audit-log?${qs}` : "/settings/audit-log";
  },
  account: () => "/account",
};

/** Which page a patient link opens: their record, or one station's form. */
export type PatientLinkTarget = "patient" | StationKey;

export function patientLinkFor(target: PatientLinkTarget | undefined, patientId: string | number, visitId?: string | number) {
  switch (target) {
    case "vitals":
      return paths.vitalsDetail(patientId, visitId);
    case "vision":
      return paths.visionUpdateGlasses(patientId, visitId);
    case "consults":
      return paths.consultsDetail(patientId, visitId);
    case "dental":
      return paths.dentalDetail(patientId, visitId);
    case "pharmacy":
      // Dispensing has no per-patient screen yet; the orders page is the pharmacy station.
      return paths.orders();
    default:
      return paths.patientDetail(patientId);
  }
}
