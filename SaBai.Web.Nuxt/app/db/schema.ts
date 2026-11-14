import { column, Schema, Table } from '@powersync/web'

const village_codes = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    code: column.text,
    name: column.text,
    color_hex: column.text,
    is_visible: column.integer,
    created_at: column.text,
  },
  { indexes: { code: ['code'] } },
)

const staff_users = new Table({
  user_name: column.text,
  nickname: column.text,
  created_at: column.text,
})

const patients = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    number: column.integer,
    village_code_id: column.text,
    name: column.text,
    identification_number: column.text,
    contact_no: column.text,
    gender: column.text,
    date_of_birth: column.text,
    drug_allergy: column.text,
    has_poor_card: column.integer,
    has_bs2_card: column.integer,
    has_sabai_card: column.integer,
    to_get_report: column.integer,
    patient_image_public_id: column.text,
    rekognition_face_id: column.text,
    created_at: column.text,
  },
  { indexes: { village: ['village_code_id'], name: ['name'] } },
)

const visits = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    patient_id: column.text,
    village_code_id: column.text,
    date: column.text,
    status: column.text,
  },
  { indexes: { patient: ['patient_id'], date: ['date'] } },
)

const vitals = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    recorded_at: column.text,
    visit_id: column.text,
    height: column.real,
    weight: column.real,
    temperature: column.real,
    systolic: column.integer,
    diastolic: column.integer,
    heart_rate: column.integer,
    hemocue_count: column.real,
    diabetes_mellitus: column.text,
    urine_test: column.text,
    blood_glucose_non_fasting: column.real,
    blood_glucose_fasting: column.real,
    hba1c: column.real,
    others: column.text,
  },
  { indexes: { visit: ['visit_id'] } },
)

const puberty_assessments = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    vitals_id: column.text,
    pubarche: column.integer,
    pubarche_age: column.integer,
    thelarche: column.integer,
    thelarche_age: column.integer,
    menarche: column.integer,
    menarche_age: column.integer,
    voice_change: column.integer,
    voice_change_age: column.integer,
    testicular_growth: column.integer,
    testicular_growth_age: column.integer,
    additional_notes: column.text,
  },
  { indexes: { vitals: ['vitals_id'] } },
)

const child_vitals = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    vitals_id: column.text,
    gross_motor: column.text,
    red_reflex: column.text,
    scoliosis: column.text,
    pallor: column.text,
    oral_cavity: column.text,
    heart: column.text,
    abdomen: column.text,
    lungs: column.text,
    hernial_orifices: column.text,
  },
  { indexes: { vitals: ['vitals_id'] } },
)

const eyesights = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    recorded_at: column.text,
    visit_id: column.text,
    left_eye_degree: column.text,
    right_eye_degree: column.text,
    left_eye_pinhole: column.text,
    right_eye_pinhole: column.text,
    left_astigmatism: column.text,
    right_astigmatism: column.text,
    comments: column.text,
    left_prescribed_glasses_degree: column.text,
    right_prescribed_glasses_degree: column.text,
  },
  { indexes: { visit: ['visit_id'] } },
)

const dental_consults = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    recorded_at: column.text,
    visit_id: column.text,
    previous_dental_visit: column.text,
    previous_dental_treatment: column.text,
    toothache: column.integer,
    extraction: column.integer,
    dental_trauma: column.integer,
    current_toothache: column.integer,
    tooth_sensitivity: column.integer,
    bleeding_gums: column.integer,
    brushing_frequency: column.text,
    fluoride_toothpaste: column.text,
    sugar_consumption: column.text,
    other_dental_history: column.text,
    chief_complaint: column.text,
    facial_symmetry: column.text,
    facial_swelling: column.text,
    lymphadenopathy: column.text,
    mouth_opening: column.text,
    oral_hygiene: column.text,
    plaque: column.text,
    calculus: column.text,
    gingival_condition: column.text,
    caries: column.text,
    missing_teeth: column.text,
    fractured_teeth: column.text,
    mucosal_lesions: column.text,
    malocclusion: column.text,
    diagnoses: column.text,
    fluoride_done: column.integer,
    fluoride_date: column.text,
    fluoride_operator: column.text,
    fluoride_tooth_numbers: column.text,
    sealant_done: column.integer,
    sealant_date: column.text,
    sealant_operator: column.text,
    sealant_tooth_numbers: column.text,
    sdf_done: column.integer,
    sdf_date: column.text,
    sdf_operator: column.text,
    sdf_tooth_numbers: column.text,
    scaling_done: column.integer,
    scaling_date: column.text,
    scaling_operator: column.text,
    scaling_tooth_numbers: column.text,
    referred_for: column.text,
    referral_priority: column.text,
    referral_notes: column.text,
    recommended_facility: column.text,
    remarks: column.text,
    image_file_id: column.text,
  },
  { indexes: { visit: ['visit_id'] } },
)

const consults = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    date: column.text,
    past_medical_history: column.text,
    consultation: column.text,
    treatment_plan: column.text,
    referred_for: column.text,
    referral_notes: column.text,
    remarks: column.text,
    doctor_id: column.text,
    visit_id: column.text,
  },
  { indexes: { visit: ['visit_id'], doctor: ['doctor_id'] } },
)

const diagnoses = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    details: column.text,
    category: column.text,
    coding_system: column.text,
    code: column.text,
    code_display: column.text,
    consult_id: column.text,
  },
  { indexes: { consult: ['consult_id'] } },
)

const referral_partners = new Table({
  version: column.integer,
  updated_at: column.text,
  name: column.text,
  remarks: column.text,
  is_active: column.integer,
  created_at: column.text,
})

const referrals = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    referred_for: column.text,
    referral_notes: column.text,
    referral_state: column.text,
    referral_outcome: column.text,
    consult_id: column.text,
    created_at: column.text,
    referred_to_id: column.text,
    member_in_charge_id: column.text,
    guardians_contacted: column.text,
    class_of_service: column.text,
    description_of_service: column.text,
    patient_summary_from_screening: column.text,
    remarks_at_booth: column.text,
    member_on_run_id: column.text,
    present_at_referral_party: column.integer,
    referral_party_results: column.text,
    follow_up: column.integer,
    doctors_updated: column.integer,
    amount_paid_usd: column.real,
    amount_paid_riels: column.integer,
    member_who_paid_id: column.text,
  },
  { indexes: { consult: ['consult_id'], partner: ['referred_to_id'] } },
)

const tags = new Table(
  {
    name: column.text,
    normalized_name: column.text,
    created_at: column.text,
  },
  { indexes: { normalized: ['normalized_name'] } },
)

const patient_tags = new Table(
  {
    patient_id: column.text,
    tag_id: column.text,
    tag_name: column.text,
  },
  { indexes: { patient: ['patient_id'], tag: ['tag_id'] } },
)

const medication_active_ingredients = new Table({
  version: column.integer,
  updated_at: column.text,
  name: column.text,
  unit_of_measurement: column.text,
  fall_below: column.integer,
  remarks: column.text,
  rx_norm_code: column.text,
  atc_code: column.text,
})

const medication_brands = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    name: column.text,
    active_ingredient_id: column.text,
    remarks: column.text,
  },
  { indexes: { ingredient: ['active_ingredient_id'] } },
)

const medication_stocks = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    medication_brand_id: column.text,
    quantity: column.integer,
    expiry: column.text,
    location: column.text,
    stock_status: column.text,
    remarks: column.text,
  },
  { indexes: { brand: ['medication_brand_id'] } },
)

const stock_changes = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    stock_id: column.text,
    field: column.text,
    previous_value: column.text,
    new_value: column.text,
    user_id: column.text,
    created_at: column.text,
  },
  { indexes: { stock: ['stock_id'] } },
)

const orders = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    consult_id: column.text,
    medication_brand_id: column.text,
    dosage_instructions: column.text,
    remarks: column.text,
    status: column.text,
    quantity: column.integer,
    approved_by_id: column.text,
    approved_at: column.text,
    created_at: column.text,
  },
  { indexes: { consult: ['consult_id'], brand: ['medication_brand_id'] } },
)

const files = new Table(
  {
    version: column.integer,
    updated_at: column.text,
    patient_id: column.text,
    file_name: column.text,
    stored_file_name: column.text,
    description: column.text,
    size_bytes: column.integer,
    is_deleted: column.integer,
    created_at: column.text,
  },
  { indexes: { patient: ['patient_id'] } },
)

export const AppSchema = new Schema({
  village_codes,
  staff_users,
  patients,
  visits,
  vitals,
  puberty_assessments,
  child_vitals,
  eyesights,
  dental_consults,
  consults,
  diagnoses,
  referral_partners,
  referrals,
  tags,
  patient_tags,
  medication_active_ingredients,
  medication_brands,
  medication_stocks,
  stock_changes,
  orders,
  files,
})

export type Database = (typeof AppSchema)['types']
