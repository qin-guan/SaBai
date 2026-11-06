/** Formats a patient id as a zero-padded, 4-digit string, e.g. 12 → "0012". */
export function formatPatientId(id: number) {
  return id.toString().padStart(4, "0");
}

/**
 * Builds the display code for a patient, for example "PC0012". Falls back to the
 * zero-padded id alone when the patient has no village (no visits yet).
 */
export function formatPatientCode(villageCode: string | null | undefined, id: number) {
  return `${villageCode ?? ""}${formatPatientId(id)}`;
}

export function calculateAge(dateOfBirth: Date | string) {
  const birthDate = new Date(dateOfBirth);
  const now = new Date();

  let age = now.getFullYear() - birthDate.getFullYear();
  const monthDifference = now.getMonth() - birthDate.getMonth();

  if (monthDifference < 0 || (monthDifference === 0 && now.getDate() < birthDate.getDate())) {
    age -= 1;
  }

  return age;
}

/**
 * Formats a Date/ISO value into the yyyy-MM-dd string a native date input expects.
 * Returns an empty string for invalid dates.
 */
export function toDateInputValue(dateOfBirth: Date | string) {
  const date = new Date(dateOfBirth);
  return Number.isNaN(date.getTime()) ? "" : date.toISOString().slice(0, 10);
}

/** Matches a patient against free-text search on id, name or identification number. */
export function patientMatches(
  patient: { id: number; name: string; identificationNumber?: string | null },
  search: string,
) {
  const needle = search.trim().toLowerCase();
  if (!needle) return false;
  return (
    patient.id.toString().includes(needle) ||
    patient.name.toLowerCase().includes(needle) ||
    (patient.identificationNumber?.toLowerCase() ?? "").includes(needle)
  );
}
