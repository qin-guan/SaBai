import type {
  SaBaiAPIEndpointsDentalDentalConsultResponse,
  SaBaiAPIEndpointsDentalUpsertByVisitPutRequest,
} from "@sabai/sdk/types";
import type {
  SaBaiAPIDomainAbsentPresentKey,
  SaBaiAPIDomainBrushingFrequencyKey,
  SaBaiAPIDomainCariesStatusKey,
  SaBaiAPIDomainDentalReferralPriorityKey,
  SaBaiAPIDomainDentalReferralTypeKey,
  SaBaiAPIDomainFacialSymmetryKey,
  SaBaiAPIDomainGingivalConditionKey,
  SaBaiAPIDomainLymphadenopathyKey,
  SaBaiAPIDomainMalocclusionKey,
  SaBaiAPIDomainMouthOpeningKey,
  SaBaiAPIDomainOralHygieneKey,
  SaBaiAPIDomainPlaqueLevelKey,
  SaBaiAPIDomainSugarConsumptionKey,
  SaBaiAPIDomainYesNoUnknownKey,
} from "@sabai/sdk/types";

import type { Option } from "./options";

export const YES_NO: Option<"yes" | "no">[] = [
  { label: "Yes", value: "yes" },
  { label: "No", value: "no" },
];

export const YES_NO_UNKNOWN: Option<SaBaiAPIDomainYesNoUnknownKey>[] = [
  { label: "Yes", value: "Yes" },
  { label: "No", value: "No" },
  { label: "Unknown", value: "Unknown" },
];

export const BRUSHING_FREQUENCY: Option<SaBaiAPIDomainBrushingFrequencyKey>[] =
  [
    { label: "Once", value: "Once" },
    { label: "Twice", value: "Twice" },
    { label: "3+ times", value: "ThreeOrMore" },
    { label: "Occasionally", value: "Occasionally" },
    { label: "Never", value: "Never" },
  ];

export const SUGAR_CONSUMPTION: Option<SaBaiAPIDomainSugarConsumptionKey>[] = [
  { label: "Low", value: "Low" },
  { label: "Moderate", value: "Moderate" },
  { label: "High", value: "High" },
];

export const DENTAL_ABSENT_PRESENT: Option<SaBaiAPIDomainAbsentPresentKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Present", value: "Present" },
];

export const DENTAL_FACIAL_SYMMETRY: Option<SaBaiAPIDomainFacialSymmetryKey>[] = [
  { label: "Normal", value: "Normal" },
  { label: "Asymmetrical", value: "Asymmetrical" },
];

export const DENTAL_LYMPHADENOPATHY: Option<SaBaiAPIDomainLymphadenopathyKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Present", value: "Present" },
  { label: "Not assessed", value: "NotAssessed" },
];

export const DENTAL_MOUTH_OPENING: Option<SaBaiAPIDomainMouthOpeningKey>[] = [
  { label: "Normal", value: "Normal" },
  { label: "Limited", value: "Limited" },
];

export const DENTAL_ORAL_HYGIENE: Option<SaBaiAPIDomainOralHygieneKey>[] = [
  { label: "Good", value: "Good" },
  { label: "Fair", value: "Fair" },
  { label: "Poor", value: "Poor" },
];

export const DENTAL_PLAQUE: Option<SaBaiAPIDomainPlaqueLevelKey>[] = [
  { label: "None", value: "None" },
  { label: "Mild", value: "Mild" },
  { label: "Moderate", value: "Moderate" },
  { label: "Heavy", value: "Heavy" },
];

export const DENTAL_GINGIVAL_CONDITION: Option<SaBaiAPIDomainGingivalConditionKey>[] = [
  { label: "Healthy", value: "Healthy" },
  { label: "Inflamed", value: "Inflamed" },
  { label: "Bleeding", value: "Bleeding" },
];

export const DENTAL_CARIES: Option<SaBaiAPIDomainCariesStatusKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Non-cavitated", value: "NonCavitated" },
  { label: "Cavitated", value: "Cavitated" },
];

export const DENTAL_MALOCCLUSION: Option<SaBaiAPIDomainMalocclusionKey>[] = [
  { label: "Not suspected", value: "NotSuspected" },
  { label: "Suspected", value: "Suspected" },
];

export const REFERRED_FOR: Option<SaBaiAPIDomainDentalReferralTypeKey>[] = [
  { label: "Restorative", value: "Restorative" },
  { label: "Extraction", value: "Extraction" },
  { label: "Perio", value: "Perio" },
  { label: "Other", value: "Other" },
  { label: "Not referred", value: "NotReferred" },
];

export const REFERRAL_PRIORITY: Option<SaBaiAPIDomainDentalReferralPriorityKey>[] =
  [
    { label: "Urgent", value: "Urgent" },
    { label: "Early", value: "Early" },
    { label: "Routine", value: "Routine" },
  ];

/** Dental history questions answered with Yes / No / Unknown. */
export const HISTORY_YES_NO_UNKNOWN = [
  { name: "previousDentalVisit", label: "Previous dental visit" },
  { name: "previousDentalTreatment", label: "Previous dental treatment" },
  { name: "fluorideToothpaste", label: "Fluoride toothpaste" },
] as const;

/** Dental history questions answered with Yes / No. */
export const HISTORY_YES_NO = [
  { name: "toothache", label: "Toothache" },
  { name: "extraction", label: "Extraction" },
  { name: "dentalTrauma", label: "Dental trauma" },
  { name: "currentToothache", label: "Current toothache" },
  { name: "toothSensitivity", label: "Tooth sensitivity" },
  { name: "bleedingGums", label: "Bleeding gums" },
] as const;

export const EXTRAORAL_EXAM = [
  {
    name: "facialSymmetry",
    label: "Facial symmetry",
    options: DENTAL_FACIAL_SYMMETRY,
  },
  { name: "facialSwelling", label: "Facial swelling", options: DENTAL_ABSENT_PRESENT },
  {
    name: "lymphadenopathy",
    label: "Lymphadenopathy",
    options: DENTAL_LYMPHADENOPATHY,
  },
  { name: "mouthOpening", label: "Mouth opening", options: DENTAL_MOUTH_OPENING },
] as const;

export const INTRAORAL_EXAM = [
  { name: "oralHygiene", label: "Oral hygiene", options: DENTAL_ORAL_HYGIENE },
  { name: "plaque", label: "Plaque", options: DENTAL_PLAQUE },
  { name: "calculus", label: "Calculus", options: DENTAL_ABSENT_PRESENT },
  {
    name: "gingivalCondition",
    label: "Gingival condition",
    options: DENTAL_GINGIVAL_CONDITION,
  },
  { name: "caries", label: "Caries", options: DENTAL_CARIES },
  { name: "missingTeeth", label: "Missing teeth", options: DENTAL_ABSENT_PRESENT },
  { name: "fracturedTeeth", label: "Fractured teeth", options: DENTAL_ABSENT_PRESENT },
  { name: "mucosalLesions", label: "Mucosal lesions", options: DENTAL_ABSENT_PRESENT },
  { name: "malocclusion", label: "Malocclusion", options: DENTAL_MALOCCLUSION },
] as const;

export const TREATMENTS = [
  { key: "fluoride", label: "Fluoride" },
  { key: "sealant", label: "Sealant" },
  { key: "sdf", label: "SDF" },
  { key: "scaling", label: "Scaling" },
] as const;

export const TREATMENT_STATUS: Option<"done" | "notDone">[] = [
  { label: "Done", value: "done" },
  { label: "Not done", value: "notDone" },
];

// ---------------------------------------------------------------------------
// Form values <-> API mapping (identical semantics to SaBai.Web's DentalForm)
// ---------------------------------------------------------------------------


export type TreatmentKey = (typeof TREATMENTS)[number]["key"];
export type YesNoName = (typeof HISTORY_YES_NO)[number]["name"];
export type ChoiceName =
  | (typeof HISTORY_YES_NO_UNKNOWN)[number]["name"]
  | (typeof EXTRAORAL_EXAM)[number]["name"]
  | (typeof INTRAORAL_EXAM)[number]["name"]
  | "brushingFrequency"
  | "sugarConsumption"
  | "referredFor"
  | "referralPriority";
export type TextName =
  | "otherDentalHistory"
  | "chiefComplaint"
  | "referralNotes"
  | "recommendedFacility"
  | "remarks";

/**
 * Every answer is held as a string so chips, selects and inputs share one shape;
 * "" means unanswered and is sent to the API as null.
 */
export type DentalFormValues = Record<YesNoName | ChoiceName | TextName, string> &
  Record<`${TreatmentKey}${"Done" | "Date" | "Operator" | "ToothNumbers"}`, string> & {
    diagnoses: { value: string }[];
  };

type DentalRequest = SaBaiAPIEndpointsDentalUpsertByVisitPutRequest;
export type DentalResponse = SaBaiAPIEndpointsDentalDentalConsultResponse;

const YES_NO_NAMES = HISTORY_YES_NO.map((q) => q.name);
const CHOICE_NAMES: ChoiceName[] = [
  ...HISTORY_YES_NO_UNKNOWN.map((q) => q.name),
  ...EXTRAORAL_EXAM.map((q) => q.name),
  ...INTRAORAL_EXAM.map((q) => q.name),
  "brushingFrequency",
  "sugarConsumption",
  "referredFor",
  "referralPriority",
];
const TEXT_NAMES: TextName[] = [
  "otherDentalHistory",
  "chiefComplaint",
  "referralNotes",
  "recommendedFacility",
  "remarks",
];

const fromBool = (value: boolean | null | undefined, yes: string, no: string) =>
  value === true ? yes : value === false ? no : "";

const toBool = (value: string, yes: string) => (value === "" ? null : value === yes);

export function toDentalFormValues(data?: Record<string, unknown> | null): DentalFormValues {
  const record = (data ?? {}) as Record<string, unknown>;
  const values: Record<string, unknown> = {};
  for (const name of YES_NO_NAMES) {
    values[name] = fromBool(record[name] as boolean | null, "yes", "no");
  }
  for (const name of [...CHOICE_NAMES, ...TEXT_NAMES]) {
    values[name] = (record[name] as string | null | undefined) ?? "";
  }
  for (const { key } of TREATMENTS) {
    values[`${key}Done`] = fromBool(record[`${key}Done`] as boolean | null, "done", "notDone");
    for (const field of ["Date", "Operator", "ToothNumbers"]) {
      values[`${key}${field}`] = (record[`${key}${field}`] as string | null | undefined) ?? "";
    }
  }
  values.diagnoses = ((data?.diagnoses as string[] | undefined) ?? []).map((value) => ({ value }));
  return values as DentalFormValues;
}

export function toDentalRequest(values: DentalFormValues): DentalRequest {
  const body: Record<string, unknown> = {};
  for (const name of YES_NO_NAMES) {
    body[name] = toBool(values[name], "yes");
  }
  for (const name of [...CHOICE_NAMES, ...TEXT_NAMES]) {
    body[name] = values[name].trim() || null;
  }
  for (const { key } of TREATMENTS) {
    body[`${key}Done`] = toBool(values[`${key}Done`], "done");
    for (const field of ["Date", "Operator", "ToothNumbers"] as const) {
      body[`${key}${field}`] = values[`${key}${field}`].trim() || null;
    }
  }
  body.diagnoses = values.diagnoses.map((d) => d.value.trim()).filter((d) => d.length > 0);
  return body as DentalRequest;
}
