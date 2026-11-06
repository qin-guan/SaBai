import type { Option } from "./options";

/** Clinical list of diagnosis categories (mirrors the API enum). */
export const DIAGNOSIS_CATEGORIES = [
  "Cardiovascular",
  "Dermatology",
  "Ear Nose Throat",
  "Endocrine",
  "Eye",
  "Gastrointestinal",
  "Haematology",
  "Infectious Diseases",
  "Renal & Genitourinary",
  "Respiratory",
  "Musculoskeletal",
  "Neurology",
  "Obstetrics & Gynaecology",
  "Oral Health",
  "Others",
] as const;

export type DiagnosisCategory = (typeof DIAGNOSIS_CATEGORIES)[number];

export const DIAGNOSIS_CATEGORY_OPTIONS: Option[] = DIAGNOSIS_CATEGORIES.map((category) => ({
  label: category,
  value: category,
}));
