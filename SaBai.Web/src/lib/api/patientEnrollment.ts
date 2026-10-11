import {
  saBaiAPIEndpointsPatientsFacesPostEndpoint,
  saBaiAPIEndpointsPatientsPhotoPutEndpoint,
} from "@sabai/sdk/clients";
import { apiConfig } from "./config";
import { dataUrlToFile } from "./patientPhoto";

/** Uploads (or replaces) a patient's profile photo in RustFS. */
export async function uploadPatientPhoto(patientId: number, image: Blob) {
  return saBaiAPIEndpointsPatientsPhotoPutEndpoint({
    ...apiConfig,
    path: { id: patientId },
    body: { image },
    throwOnError: true,
  }).unwrap();
}

/** Enrolls a face for a patient in the Qdrant face index. */
export async function enrollPatientFace(patientId: number, image: Blob) {
  return saBaiAPIEndpointsPatientsFacesPostEndpoint({
    ...apiConfig,
    path: { id: patientId },
    body: { image },
    throwOnError: true,
  }).unwrap();
}

/**
 * Stores a captured photo as both the patient's profile photo and an enrolled
 * face. Callers should treat a failure as non-fatal to the wider flow.
 */
export async function setPatientPhotoAndFace(
  patientId: number,
  dataUrl: string,
): Promise<void> {
  const file = await dataUrlToFile(dataUrl, `patient-${patientId}`);
  await uploadPatientPhoto(patientId, file);
  await enrollPatientFace(patientId, file);
}
