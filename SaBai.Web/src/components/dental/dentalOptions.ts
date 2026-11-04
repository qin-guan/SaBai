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

export type Option<T extends string = string> = { label: string; value: T };

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

const ABSENT_PRESENT: Option<SaBaiAPIDomainAbsentPresentKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Present", value: "Present" },
];

const FACIAL_SYMMETRY: Option<SaBaiAPIDomainFacialSymmetryKey>[] = [
  { label: "Normal", value: "Normal" },
  { label: "Asymmetrical", value: "Asymmetrical" },
];

const LYMPHADENOPATHY: Option<SaBaiAPIDomainLymphadenopathyKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Present", value: "Present" },
  { label: "Not assessed", value: "NotAssessed" },
];

const MOUTH_OPENING: Option<SaBaiAPIDomainMouthOpeningKey>[] = [
  { label: "Normal", value: "Normal" },
  { label: "Limited", value: "Limited" },
];

const ORAL_HYGIENE: Option<SaBaiAPIDomainOralHygieneKey>[] = [
  { label: "Good", value: "Good" },
  { label: "Fair", value: "Fair" },
  { label: "Poor", value: "Poor" },
];

const PLAQUE: Option<SaBaiAPIDomainPlaqueLevelKey>[] = [
  { label: "None", value: "None" },
  { label: "Mild", value: "Mild" },
  { label: "Moderate", value: "Moderate" },
  { label: "Heavy", value: "Heavy" },
];

const GINGIVAL_CONDITION: Option<SaBaiAPIDomainGingivalConditionKey>[] = [
  { label: "Healthy", value: "Healthy" },
  { label: "Inflamed", value: "Inflamed" },
  { label: "Bleeding", value: "Bleeding" },
];

const CARIES: Option<SaBaiAPIDomainCariesStatusKey>[] = [
  { label: "Absent", value: "Absent" },
  { label: "Non-cavitated", value: "NonCavitated" },
  { label: "Cavitated", value: "Cavitated" },
];

const MALOCCLUSION: Option<SaBaiAPIDomainMalocclusionKey>[] = [
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
    options: FACIAL_SYMMETRY,
  },
  { name: "facialSwelling", label: "Facial swelling", options: ABSENT_PRESENT },
  {
    name: "lymphadenopathy",
    label: "Lymphadenopathy",
    options: LYMPHADENOPATHY,
  },
  { name: "mouthOpening", label: "Mouth opening", options: MOUTH_OPENING },
] as const;

export const INTRAORAL_EXAM = [
  { name: "oralHygiene", label: "Oral hygiene", options: ORAL_HYGIENE },
  { name: "plaque", label: "Plaque", options: PLAQUE },
  { name: "calculus", label: "Calculus", options: ABSENT_PRESENT },
  {
    name: "gingivalCondition",
    label: "Gingival condition",
    options: GINGIVAL_CONDITION,
  },
  { name: "caries", label: "Caries", options: CARIES },
  { name: "missingTeeth", label: "Missing teeth", options: ABSENT_PRESENT },
  { name: "fracturedTeeth", label: "Fractured teeth", options: ABSENT_PRESENT },
  { name: "mucosalLesions", label: "Mucosal lesions", options: ABSENT_PRESENT },
  { name: "malocclusion", label: "Malocclusion", options: MALOCCLUSION },
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
