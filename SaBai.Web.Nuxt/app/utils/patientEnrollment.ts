import {
  saBaiAPIEndpointsPatientsFacesPostEndpoint,
  saBaiAPIEndpointsPatientsPhotoPutEndpoint,
} from '@sabai/sdk/clients'

/**
 * Stores a captured photo as both the patient's profile photo (RustFS) and an
 * enrolled face (Qdrant). Callers treat a failure as non-fatal.
 */
export async function setPatientPhotoAndFace(patientId: string | number, dataUrl: string): Promise<void> {
  const image = await dataUrlToFile(dataUrl, `patient-${patientId}`)
  const id = patientId as unknown as number
  await saBaiAPIEndpointsPatientsPhotoPutEndpoint({
    ...apiConfig,
    path: { id },
    body: { image },
    throwOnError: true,
  }).unwrap()
  await saBaiAPIEndpointsPatientsFacesPostEndpoint({
    ...apiConfig,
    path: { id },
    body: { image },
    throwOnError: true,
  }).unwrap()
}
