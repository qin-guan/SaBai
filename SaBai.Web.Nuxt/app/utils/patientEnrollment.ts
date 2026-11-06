import {
  saBaiAPIEndpointsPatientsFacesPostEndpoint,
  saBaiAPIEndpointsPatientsPhotoPutEndpoint,
} from '@sabai/sdk/clients'

/**
 * Stores a captured photo as both the patient's profile photo (RustFS) and an
 * enrolled face (Qdrant). Callers treat a failure as non-fatal.
 */
export async function setPatientPhotoAndFace(patientId: number, dataUrl: string): Promise<void> {
  const image = await dataUrlToFile(dataUrl, `patient-${patientId}`)
  await saBaiAPIEndpointsPatientsPhotoPutEndpoint({
    ...apiConfig,
    path: { id: patientId },
    body: { image },
    throwOnError: true,
  }).unwrap()
  await saBaiAPIEndpointsPatientsFacesPostEndpoint({
    ...apiConfig,
    path: { id: patientId },
    body: { image },
    throwOnError: true,
  }).unwrap()
}
