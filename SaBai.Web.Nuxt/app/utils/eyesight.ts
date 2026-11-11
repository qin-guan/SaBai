import { z } from "zod";
import { cleanKhmerText, normalizeKhmerDigits } from "./khmer";

/**
 * Validation primitives for eyesight fields.
 *
 * Per clinic SOP: Snellen fractions 6/6..6/120 (6m tumbling-E chart) with an
 * optional "+N"/"-N" suffix for letters read over/under on the next line
 * (e.g. "6/9+2", "6/9-2"), plus the low-vision codes CF (counting fingers),
 * HM (hand movement), LP (light perception), NLP (no light perception).
 *
 * 6/7.5 and 6/15 are also accepted — they appear in the old db.
 */
const ACUITY_PATTERN = "6\\/(7\\.5|6|9|12|15|18|24|36|60|120)([+-]\\d{1,2})?|CF|HM|LP|NLP";

/** Visual acuity (degree) fields. Case-insensitive. */
export const VISUAL_ACUITY_REGEX = new RegExp(`^(${ACUITY_PATTERN})$`, "i");

/** Pinhole fields: acuity values plus "NI" (no improvement). Case-insensitive. */
export const PINHOLE_REGEX = new RegExp(`^(${ACUITY_PATTERN}|NI)$`, "i");

// Optional, trimmed string field; empty string means "not recorded". Khmer digits (៦/៩) are normalized to ASCII.
const optionalField = (regex: RegExp, message: string) =>
  z
    .string()
    .transform(v => normalizeKhmerDigits(cleanKhmerText(v)))
    .pipe(z.string().regex(regex, message).toUpperCase().or(z.literal("")))
    .optional();

export const visualAcuitySchema = optionalField(
  VISUAL_ACUITY_REGEX,
  "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120) or CF, HM, LP, NLP",
);

export const pinholeSchema = optionalField(
  PINHOLE_REGEX,
  "Must be VA notation (e.g. 6/9, 6/9+2, 6/9-2, 6/120), CF, HM, LP, NLP, or NI",
);

/** Returns the first validation message for `value`, or null when valid. */
export function schemaError(schema: z.ZodType, value: unknown): string | null {
  const result = schema.safeParse(value ?? "");
  return result.success ? null : (result.error.issues[0]?.message ?? "Invalid value");
}

export type EyesightFormValues = {
  leftEyeDegree: string;
  rightEyeDegree: string;
  leftEyePinhole: string;
  rightEyePinhole: string;
  leftAstigmatism: string;
  rightAstigmatism: string;
  leftPrescribedGlassesDegree: string;
  rightPrescribedGlassesDegree: string;
  comments: string;
};

export const BLANK_EYESIGHT: EyesightFormValues = {
  leftEyeDegree: "",
  rightEyeDegree: "",
  leftEyePinhole: "",
  rightEyePinhole: "",
  leftAstigmatism: "",
  rightAstigmatism: "",
  leftPrescribedGlassesDegree: "",
  rightPrescribedGlassesDegree: "",
  comments: "",
};
