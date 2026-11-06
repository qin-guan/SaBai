export const paths = {
  home: () => "/",
  login: () => "/login",
  patient: () => "/patient",
  patientDetail: (patientId: number) => `/patient/${patientId}`,
  scanFace: () => "/scan-face",
  vitals: () => "/vitals",
  vitalsDetail: (patientId: number, visitId?: number) =>
    visitId === undefined ? `/vitals/${patientId}` : `/vitals/${patientId}?visit=${visitId}`,
  vision: () => "/vision",
  visionUpdateGlasses: (patientId: number) => `/vision/update-glasses/${patientId}`,
  dental: () => "/dental",
  dentalDetail: (patientId: number) => `/dental/${patientId}`,
  consults: () => "/consults",
  consultsDetail: (patientId: number) => `/consults/${patientId}`,
  medicationStock: () => "/medication-stock",
  medicationBrands: () => "/medication-brands",
  medicationActiveIngredients: () => "/medication-active-ingredients",
  orders: () => "/orders",
  settings: () => "/settings",
  settingsVillageCodes: () => "/settings/village-codes",
};

/** Which station page a patient search result should open, per page. */
export type PatientLinkTarget = "patient" | "vitals" | "vision" | "consults" | "dental";

export function patientLinkFor(target: PatientLinkTarget | undefined, patientId: number) {
  switch (target) {
    case "vitals":
      return paths.vitalsDetail(patientId);
    case "vision":
      return paths.visionUpdateGlasses(patientId);
    case "consults":
      return paths.consultsDetail(patientId);
    case "dental":
      return paths.dentalDetail(patientId);
    default:
      return paths.patientDetail(patientId);
  }
}
