import { cleanKhmerText, matchesSearchText, normalizeForSearch } from "./khmer";

/** Formats a patient id/number as a zero-padded, 4-digit string, e.g. 12 → "0012". */
export function formatPatientId(id: string | number) {
  if (typeof id === "number") return id.toString().padStart(4, "0");
  return /^\d+$/.test(id) ? id.padStart(4, "0") : id.slice(0, 8);
}

/**
 * Builds the display code for a patient, for example "PC0012". Falls back to the
 * zero-padded id alone when the patient has no village (no visits yet).
 */
export function formatPatientCode(villageCode: string | null | undefined, id: string | number) {
  return `${villageCode ?? ""}${formatPatientId(id)}`;
}

/**
 * Age in whole years on a given date (today by default).
 * @param asOf - Date to measure the age at, e.g. the date a referral was made.
 */
export function calculateAge(dateOfBirth: Date | string, asOf: Date | string = new Date()) {
  const birthDate = new Date(dateOfBirth);
  const now = new Date(asOf);

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

/** Matches a patient against free-text search on id, name or identification number (including Khmer script and numerals). */
export function patientMatches(
  patient: { id: string | number; number?: number; displayId?: string | null; name: string; identificationNumber?: string | null },
  search: string,
) {
  if (!cleanKhmerText(search)) return false;
  return (
    matchesSearchText(patient.displayId ?? patient.number?.toString() ?? patient.id.toString(), search) ||
    matchesSearchText(patient.name, search) ||
    matchesSearchText(patient.identificationNumber, search)
  );
}

/**
 * True when a recorded drug allergy is a real allergy, not a "none" answer
 * ("nil", "NKDA", "-", or Khmer "គ្មាន" / "មិនមាន" / "ទេ"), so it can be flagged in red.
 */
export function hasDrugAllergy(drugAllergy: string | null | undefined) {
  const value = cleanKhmerText(drugAllergy).replace(/[.។៕]+$/, "").trim();
  return !!value && !/^(nil|nkda|none|no|-|n\/a|na|គ្មាន|មិនមាន|ទេ|គ្មានអាឡែស៊ី)$/i.test(value);
}

export const RECENT_VISIT_MS = 24 * 60 * 60_000;

/**
 * Whether a visit timestamp is within the last 24 hours of `now` (spanning
 * overnight clinics that run past midnight without splitting by calendar day).
 */
export function isRecentVisit(date: Date | string | null | undefined, now = new Date()) {
  if (!date) return false;
  const elapsed = now.getTime() - new Date(date).getTime();
  return elapsed >= 0 && elapsed < RECENT_VISIT_MS;
}

/**
 * Splits a free-text drug allergy answer into separate allergies
 * ("Penicillin, sulfa drugs; aspirin" → three items), so long lists can be
 * shown as a count in lists and in full on the patient banner. Supports Khmer
 * punctuation (។, ៕) and conjunction (និង). Duplicates and "none"-style answers
 * are dropped.
 */
export function parseDrugAllergies(drugAllergy: string | null | undefined): string[] {
  if (!hasDrugAllergy(drugAllergy))
    return [];
  const seen = new Set<string>();
  return (drugAllergy ?? "")
    .normalize("NFC")
    .replace(/[\u200B\uFEFF]+/g, " ")
    .split(/[,;\r\n/،，；។៕]+|\s+(?:and|&|និង)\s+/i)
    .map(item => cleanKhmerText(item).replace(/[.។៕]+$/, "").trim())
    .filter((item) => {
      const key = normalizeForSearch(item);
      if (!hasDrugAllergy(item) || seen.has(key))
        return false;
      seen.add(key);
      return true;
    });
}

export type TagMatchMode = "any" | "all";

/** Case-insensitive check for whether `tag` is in `selectedTags`. */
export function hasTagSelected(selectedTags: readonly string[] | null | undefined, tag: string): boolean {
  const needle = tag.trim().toLowerCase();
  if (!needle || !selectedTags || selectedTags.length === 0)
    return false;
  return selectedTags.some(t => t.trim().toLowerCase() === needle);
}

/** Case-insensitively toggles `tag` in `selectedTags` and returns the updated array. */
export function toggleTagSelection(selectedTags: readonly string[] | null | undefined, tag: string): string[] {
  const trimmed = tag.trim();
  if (!trimmed)
    return [...(selectedTags ?? [])];
  const current = [...(selectedTags ?? [])];
  const needle = trimmed.toLowerCase();
  const idx = current.findIndex(t => t.trim().toLowerCase() === needle);
  if (idx >= 0) {
    current.splice(idx, 1);
    return current;
  }
  return [...current, trimmed];
}

/**
 * Matches an item's tags against `selectedTags` using `'any'` (OR) or `'all'` (AND).
 * Returns true when no tags are selected.
 */
export function matchesTags(
  itemTags: readonly string[] | null | undefined,
  selectedTags: readonly string[] | null | undefined,
  mode: TagMatchMode = "any",
): boolean {
  if (!selectedTags || selectedTags.length === 0)
    return true;
  const normalizedSelected = selectedTags.map(t => t.trim().toLowerCase()).filter(Boolean);
  if (normalizedSelected.length === 0)
    return true;
  const normalizedItem = new Set((itemTags ?? []).map(t => t.trim().toLowerCase()).filter(Boolean));
  return mode === "all"
    ? normalizedSelected.every(t => normalizedItem.has(t))
    : normalizedSelected.some(t => normalizedItem.has(t));
}

/** Collects unique tags across one or more tag lists (case-insensitive deduplication, sorted). */
export function collectUniqueTags(lists: readonly (readonly string[] | null | undefined)[]): string[] {
  const seen = new Map<string, string>();
  for (const list of lists) {
    for (const raw of list ?? []) {
      const trimmed = raw.trim();
      if (!trimmed)
        continue;
      const key = trimmed.toLowerCase();
      if (!seen.has(key))
        seen.set(key, trimmed);
    }
  }
  return [...seen.values()].sort((a, b) => a.localeCompare(b, undefined, { sensitivity: "base" }));
}

export const MAX_TAG_LENGTH = 64;

export interface TagDropdownOption {
  type: "existing" | "create";
  tag: string;
}

/**
 * Builds the inline autocomplete dropdown options for a tag input field:
 * - Matching existing tags (excluding tags already selected on the record), case-insensitively filtered by `query`.
 * - A `"create"` option for the trimmed query when it is non-empty and does not case-insensitively match any existing or already-selected tag.
 */
export function buildTagDropdownOptions(
  knownTags: readonly string[] | null | undefined,
  selectedTags: readonly string[] | null | undefined,
  query: string,
  maxLength = MAX_TAG_LENGTH,
): TagDropdownOption[] {
  const selectedSet = new Set(
    (selectedTags ?? []).map(t => t.trim().toLowerCase()).filter(Boolean),
  );
  const available = collectUniqueTags([knownTags]).filter(
    t => !selectedSet.has(t.toLowerCase()),
  );

  const trimmed = query.trim().slice(0, maxLength);
  const needle = trimmed.toLowerCase();

  const matching = needle
    ? available.filter(t => t.toLowerCase().includes(needle))
    : available;

  const options: TagDropdownOption[] = matching.map(tag => ({
    type: "existing",
    tag,
  }));

  if (
    trimmed &&
    !selectedSet.has(needle) &&
    !available.some(t => t.toLowerCase() === needle)
  ) {
    options.push({
      type: "create",
      tag: trimmed,
    });
  }

  return options;
}
