import type { MaybeRefOrGetter } from 'vue'

const enabledFor = (visitId: MaybeRefOrGetter<string | null | undefined>) =>
  computed(() => {
    const v = toValue(visitId)
    return typeof v === 'string' && v.trim().length > 0
  })

export function useVisitVitals(visitId: MaybeRefOrGetter<string>) {
  return useLocalQuery<Record<string, unknown> | null>(
    ['vitals', 'puberty_assessments', 'child_vitals'],
    async () => {
      const vid = toValue(visitId)
      if (!vid) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<Record<string, unknown>>(
        'SELECT * FROM vitals WHERE visit_id = ? LIMIT 1',
        [vid],
      )
      if (!row) return null
      const puberty = await db.getOptional<Record<string, unknown>>(
        'SELECT * FROM puberty_assessments WHERE vitals_id = ? LIMIT 1',
        [row.id],
      )
      const childVitals = await db.getOptional<Record<string, unknown>>(
        'SELECT * FROM child_vitals WHERE vitals_id = ? LIMIT 1',
        [row.id],
      )
      return {
        id: row.id,
        visitId: row.visit_id,
        recordedAt: row.recorded_at,
        height: row.height,
        weight: row.weight,
        temperature: row.temperature,
        systolic: row.systolic,
        diastolic: row.diastolic,
        heartRate: row.heart_rate,
        hemocueCount: row.hemocue_count,
        diabetesMellitus: row.diabetes_mellitus,
        urineTest: row.urine_test,
        bloodGlucoseNonFasting: row.blood_glucose_non_fasting,
        bloodGlucoseFasting: row.blood_glucose_fasting,
        hba1c: row.hba1c,
        others: row.others,
        pubertyAssessment: puberty
          ? {
              id: puberty.id,
              pubarche: Boolean(puberty.pubarche),
              pubarcheAge: puberty.pubarche_age,
              thelarche: Boolean(puberty.thelarche),
              thelarcheAge: puberty.thelarche_age,
              menarche: Boolean(puberty.menarche),
              menarcheAge: puberty.menarche_age,
              voiceChange: Boolean(puberty.voice_change),
              voiceChangeAge: puberty.voice_change_age,
              testicularGrowth: Boolean(puberty.testicular_growth),
              testicularGrowthAge: puberty.testicular_growth_age,
              additionalNotes: puberty.additional_notes,
            }
          : null,
        childVitals: childVitals
          ? {
              id: childVitals.id,
              grossMotor: childVitals.gross_motor,
              redReflex: childVitals.red_reflex,
              scoliosis: childVitals.scoliosis,
              pallor: childVitals.pallor,
              oralCavity: childVitals.oral_cavity,
              heart: childVitals.heart,
              abdomen: childVitals.abdomen,
              lungs: childVitals.lungs,
              hernialOrifices: childVitals.hernial_orifices,
            }
          : null,
      }
    },
    { enabled: enabledFor(visitId), watchDeps: [() => toValue(visitId)] },
  )
}

export function useVisitEyesight(visitId: MaybeRefOrGetter<string>) {
  return useLocalQuery<Record<string, unknown> | null>(
    ['eyesights'],
    async () => {
      const vid = toValue(visitId)
      if (!vid) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<Record<string, unknown>>(
        'SELECT * FROM eyesights WHERE visit_id = ? LIMIT 1',
        [vid],
      )
      if (!row) return null
      return {
        id: row.id,
        visitId: row.visit_id,
        recordedAt: row.recorded_at,
        leftEyeDegree: row.left_eye_degree,
        rightEyeDegree: row.right_eye_degree,
        leftEyePinhole: row.left_eye_pinhole,
        rightEyePinhole: row.right_eye_pinhole,
        leftAstigmatism: row.left_astigmatism,
        rightAstigmatism: row.right_astigmatism,
        comments: row.comments,
        leftPrescribedGlassesDegree: row.left_prescribed_glasses_degree,
        rightPrescribedGlassesDegree: row.right_prescribed_glasses_degree,
      }
    },
    { enabled: enabledFor(visitId), watchDeps: [() => toValue(visitId)] },
  )
}

export function useVisitConsults(visitId: MaybeRefOrGetter<string>) {
  return useLocalQuery<{ consults: Array<Record<string, unknown>> }>(
    ['consults', 'diagnoses', 'orders', 'referrals', 'staff_users', 'medication_brands'],
    async () => {
      const vid = toValue(visitId)
      if (!vid) return { consults: [] }
      const db = getPowerSyncInstance()
      const rows = await db.getAll<Record<string, unknown>>(
        `SELECT c.*, su.nickname AS doctor_name
         FROM consults c
         LEFT JOIN staff_users su ON su.id = c.doctor_id
         WHERE c.visit_id = ?
         ORDER BY c.date DESC`,
        [vid],
      )
      const consults = []
      for (const c of rows) {
        const diagnoses = await db.getAll<Record<string, unknown>>(
          'SELECT id, details, category FROM diagnoses WHERE consult_id = ?',
          [c.id],
        )
        const orders = await db.getAll<Record<string, unknown>>(
          `SELECT o.*, mb.name AS medication_brand_name
           FROM orders o
           LEFT JOIN medication_brands mb ON mb.id = o.medication_brand_id
           WHERE o.consult_id = ?`,
          [c.id],
        )
        const referrals = await db.getAll<Record<string, unknown>>(
          'SELECT * FROM referrals WHERE consult_id = ?',
          [c.id],
        )
        consults.push({
          id: c.id,
          visitId: c.visit_id,
          date: c.date,
          pastMedicalHistory: c.past_medical_history,
          consultation: c.consultation,
          treatmentPlan: c.treatment_plan,
          referredFor: c.referred_for,
          referralNotes: c.referral_notes,
          remarks: c.remarks,
          doctorId: c.doctor_id,
          doctorName: c.doctor_name,
          doctorNickname: c.doctor_name,
          diagnoses: diagnoses.map(d => ({ id: d.id, details: d.details, category: d.category })),
          orders: orders.map(o => ({
            id: o.id,
            medicationBrandId: o.medication_brand_id,
            medicationBrandName: o.medication_brand_name,
            dosageInstructions: o.dosage_instructions,
            remarks: o.remarks,
            status: o.status,
            quantity: o.quantity,
            approvedById: o.approved_by_id,
            approvedAt: o.approved_at,
            createdAt: o.created_at,
          })),
          referrals: referrals.map(r => ({
            id: r.id,
            referredFor: r.referred_for,
            referralNotes: r.referral_notes,
            referralState: r.referral_state,
            referralOutcome: r.referral_outcome,
            createdAt: r.created_at,
          })),
        })
      }
      return { consults }
    },
    { enabled: enabledFor(visitId), watchDeps: [() => toValue(visitId)] },
  )
}

export function useConsult(consultId: MaybeRefOrGetter<string | null | undefined>) {
  return useLocalQuery<{ id: string, visitId: string } | null>(
    ['consults'],
    async () => {
      const cid = toValue(consultId)
      if (!cid) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<{ id: string, visit_id: string }>(
        'SELECT id, visit_id FROM consults WHERE id = ? LIMIT 1',
        [cid],
      )
      return row ? { id: row.id, visitId: row.visit_id } : null
    },
    { enabled: enabledFor(consultId), watchDeps: [() => toValue(consultId)] },
  )
}

export function useVisitDental(visitId: MaybeRefOrGetter<string>) {
  return useLocalQuery<Record<string, unknown> | null>(
    ['dental_consults'],
    async () => {
      const vid = toValue(visitId)
      if (!vid) return null
      const db = getPowerSyncInstance()
      const row = await db.getOptional<Record<string, unknown>>(
        'SELECT * FROM dental_consults WHERE visit_id = ? LIMIT 1',
        [vid],
      )
      if (!row) return null
      let diagnoses: string[] = []
      if (typeof row.diagnoses === 'string') {
        try {
          diagnoses = JSON.parse(row.diagnoses)
        } catch {
          diagnoses = []
        }
      }
      return {
        id: row.id,
        visitId: row.visit_id,
        recordedAt: row.recorded_at,
        previousDentalVisit: row.previous_dental_visit,
        previousDentalTreatment: row.previous_dental_treatment,
        toothache: row.toothache === null ? null : Boolean(row.toothache),
        extraction: row.extraction === null ? null : Boolean(row.extraction),
        dentalTrauma: row.dental_trauma === null ? null : Boolean(row.dental_trauma),
        currentToothache: row.current_toothache === null ? null : Boolean(row.current_toothache),
        toothSensitivity: row.tooth_sensitivity === null ? null : Boolean(row.tooth_sensitivity),
        bleedingGums: row.bleeding_gums === null ? null : Boolean(row.bleeding_gums),
        brushingFrequency: row.brushing_frequency,
        fluorideToothpaste: row.fluoride_toothpaste,
        sugarConsumption: row.sugar_consumption,
        otherDentalHistory: row.other_dental_history,
        chiefComplaint: row.chief_complaint,
        facialSymmetry: row.facial_symmetry,
        facialSwelling: row.facial_swelling,
        lymphadenopathy: row.lymphadenopathy,
        mouthOpening: row.mouth_opening,
        oralHygiene: row.oral_hygiene,
        plaque: row.plaque,
        calculus: row.calculus,
        gingivalCondition: row.gingival_condition,
        caries: row.caries,
        missingTeeth: row.missing_teeth,
        fracturedTeeth: row.fractured_teeth,
        mucosalLesions: row.mucosal_lesions,
        malocclusion: row.malocclusion,
        diagnoses,
        fluorideDone: row.fluoride_done === null ? null : Boolean(row.fluoride_done),
        fluorideDate: row.fluoride_date,
        fluorideOperator: row.fluoride_operator,
        fluorideToothNumbers: row.fluoride_tooth_numbers,
        sealantDone: row.sealant_done === null ? null : Boolean(row.sealant_done),
        sealantDate: row.sealant_date,
        sealantOperator: row.sealant_operator,
        sealantToothNumbers: row.sealant_tooth_numbers,
        sdfDone: row.sdf_done === null ? null : Boolean(row.sdf_done),
        sdfDate: row.sdf_date,
        sdfOperator: row.sdf_operator,
        sdfToothNumbers: row.sdf_tooth_numbers,
        scalingDone: row.scaling_done === null ? null : Boolean(row.scaling_done),
        scalingDate: row.scaling_date,
        scalingOperator: row.scaling_operator,
        scalingToothNumbers: row.scaling_tooth_numbers,
        referredFor: row.referred_for,
        referralPriority: row.referral_priority,
        referralNotes: row.referral_notes,
        recommendedFacility: row.recommended_facility,
        remarks: row.remarks,
        imageFileId: row.image_file_id,
      }
    },
    { enabled: enabledFor(visitId), watchDeps: [() => toValue(visitId)] },
  )
}
