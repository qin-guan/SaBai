import { useQuery } from "@tanstack/react-query";
import { useEffect, useState } from "react";
import { saBaiAPIEndpointsPatientsPhotoGetEndpoint } from "@sabai/sdk/clients";
import { apiConfig } from "./config";

/** Query key for a patient's photo blob. Exported so mutations can invalidate it. */
export const patientPhotoQueryKey = (patientId: number) =>
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
  patientId: number,
  enabled = true,
): string | null {
  const [url, setUrl] = useState<string | null>(null);

  const { data } = useQuery({
    queryKey: patientPhotoQueryKey(patientId),
    enabled: enabled && patientId > 0,
    staleTime: 5 * 60 * 1000,
    retry: false,
    queryFn: async () => {
      const result = await saBaiAPIEndpointsPatientsPhotoGetEndpoint({
        ...apiConfig,
        path: { id: patientId },
        responseType: "blob",
        throwOnError: false,
      });
      if (result.status !== 200) return null;
      return (result.data as Blob | undefined) ?? null;
    },
  });

  useEffect(() => {
    if (!data) {
      setUrl(null);
      return;
    }

    const objectUrl = URL.createObjectURL(data);
    setUrl(objectUrl);
    return () => URL.revokeObjectURL(objectUrl);
  }, [data]);

  return url;
}
