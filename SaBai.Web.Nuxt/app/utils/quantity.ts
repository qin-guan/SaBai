/**
 * Quantities of medication, always with their unit, so "10" is never left to
 * mean tablets or millilitres. The unit is the active ingredient's
 * `unitOfMeasurement` (free text typed by the pharmacy: "tablet", "mL",
 * "bottle"…).
 */

/**
 * Counted units that take a plural, with the plural form. Anything else
 * (symbols and measures such as "mL", "mg", "g", "IU", or a unit the pharmacy
 * made up) is left exactly as written: "5 mL", never "5 mLs".
 */
const COUNT_UNITS: Record<string, string> = {
  ampoule: "ampoules",
  ampule: "ampules",
  bag: "bags",
  bottle: "bottles",
  box: "boxes",
  capsule: "capsules",
  dose: "doses",
  drop: "drops",
  inhaler: "inhalers",
  lozenge: "lozenges",
  pack: "packs",
  packet: "packets",
  patch: "patches",
  piece: "pieces",
  pill: "pills",
  puff: "puffs",
  sachet: "sachets",
  strip: "strips",
  suppository: "suppositories",
  syringe: "syringes",
  tablet: "tablets",
  tube: "tubes",
  vial: "vials",
};

const SINGULAR_OF: Record<string, string> = Object.fromEntries(
  Object.entries(COUNT_UNITS).map(([singular, plural]) => [plural, singular]),
);

/** Writes `word` with the capitalisation of `like` ("Tablet" → "Tablets", "TABLET" → "TABLETS"). */
function matchCase(word: string, like: string): string {
  if (like.length > 1 && like === like.toUpperCase()) return word.toUpperCase();
  if (like[0] !== like[0]?.toLowerCase()) return word.charAt(0).toUpperCase() + word.slice(1);
  return word;
}

/**
 * The unit as it reads after `quantity`: "tablet" for 1, "tablets" otherwise.
 * Only known counted units change (see `COUNT_UNITS`); a unit stored in the
 * plural ("tablets") becomes singular for 1. An empty or missing unit gives "".
 * Without a `quantity` (an empty field) the plural is used.
 */
export function quantityUnit(unit: string | null | undefined, quantity?: number | null): string {
  const text = unit?.trim() ?? "";
  if (!text) return "";
  const key = text.toLowerCase();
  const singular = key in COUNT_UNITS ? key : SINGULAR_OF[key];
  if (!singular) return text;
  const one = quantity != null && Math.abs(quantity) === 1;
  return matchCase(one ? singular : COUNT_UNITS[singular]!, text);
}

/**
 * A quantity with its unit: `formatQuantity(10, "tablet")` → "10 tablets",
 * `(1, "tablet")` → "1 tablet", `(5, "mL")` → "5 mL". With no unit it is the
 * number alone ("10"); a missing quantity gives "-".
 */
export function formatQuantity(quantity: number | null | undefined, unit?: string | null): string {
  if (quantity == null || Number.isNaN(quantity)) return "-";
  const suffix = quantityUnit(unit, quantity);
  return suffix ? `${quantity} ${suffix}` : String(quantity);
}
