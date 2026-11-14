import {
  saBaiAPIEndpointsPatientsFacesPostEndpoint,
  saBaiAPIEndpointsPatientsPhotoPutEndpoint,
} from '@sabai/sdk/clients'

/** What happened to the face enrolment after the photo was stored. */
export type FaceEnrollmentResult =
  | { enrolled: true }
  /** The API refused (403) because the patient's face recognition consent is not current. */
  | { enrolled: false, reason: string }

/** Where staff record the consent that face enrolment needs. */
export const FACE_CONSENT_HINT
  = 'Record the patient\'s face recognition consent under "Privacy & disclosures" on the patient page, then take the photo again to enrol the face.'

/**
 * Stores a captured photo as the patient's profile photo (RustFS) and enrols it
 * as a face (Qdrant). The face is only enrolled while the patient's biometric
 * consent is current; without it the photo is kept and the result says why the
 * face was skipped. Other failures throw; callers treat them as non-fatal.
 */
export async function setPatientPhotoAndFace(patientId: string, dataUrl: string): Promise<FaceEnrollmentResult> {
  const image = await dataUrlToFile(dataUrl, `patient-${patientId}`)
  await saBaiAPIEndpointsPatientsPhotoPutEndpoint({
    ...apiConfig,
    path: { id: patientId },
    body: { image },
    throwOnError: true,
  }).unwrap()
  try {
    await saBaiAPIEndpointsPatientsFacesPostEndpoint({
      ...apiConfig,
      path: { id: patientId },
      body: { image },
      throwOnError: true,
    }).unwrap()
  }
  catch (error) {
    if (getApiErrorStatus(error) === 403)
      return { enrolled: false, reason: getApiErrorMessage(error, 'Face recognition consent has not been granted for this patient.') }
    throw error
  }
  return { enrolled: true }
}
