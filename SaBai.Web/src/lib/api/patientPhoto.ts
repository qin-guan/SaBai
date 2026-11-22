import { useQuery } from "@tanstack/react-query";
import { useEffect, useMemo } from "react";
import { saBaiAPIEndpointsPatientsPhotoGetEndpoint } from "@sabai/sdk/clients";
import { apiConfig } from "./config";

/** Query key for a patient's photo blob. Exported so mutations can invalidate it. */
export const patientPhotoQueryKey = (patientId: string) =>
  ["patient-photo", patientId] as const;

/**
 * Converts a captured data URL (e.g. from the webcam) into a `File` so it can be
 * sent as multipart/form-data to the photo and face endpoints.
 */
export async function dataUrlToFile(
  dataUrl: string,
  fileName = "patient",
): Promise<File> {
  const blob = await (await fetch(dataUrl)).blob();
  const extension = blob.type.split("/")[1]?.split("+")[0] ?? "jpg";
  const name = fileName.includes(".") ? fileName : `${fileName}.${extension}`;
  return new File([blob], name, { type: blob.type });
}

/**
 * Loads a patient's profile photo as an object URL.
 *
 * Photos are served by the API behind bearer auth, so an `<img src>` cannot fetch
 * them directly; we fetch the blob through the SDK and expose a local URL.
 */
export function usePatientPhoto(
  patientId: string,
  enabled = true,
): string | null {
  const { data } = useQuery({
    queryKey: patientPhotoQueryKey(patientId),
    enabled: enabled && patientId.length > 0,
    staleTime: 5 * 60 * 1000,
    retry: false,
    queryFn: async () => {
      const result = (await saBaiAPIEndpointsPatientsPhotoGetEndpoint({
        ...apiConfig,
        path: { id: patientId },
        responseType: "blob",
        throwOnError: false,
      })) as unknown as { status: number; data?: Blob | null };
      if (result.status !== 200) return null;
      return result.data ?? null;
    },
  });

  // One object URL per loaded blob, released when the blob changes or the
  // component unmounts.
  const url = useMemo(() => (data ? URL.createObjectURL(data) : null), [data]);
  useEffect(() => {
    if (!url) return;
    return () => URL.revokeObjectURL(url);
  }, [url]);

  return url;
}
