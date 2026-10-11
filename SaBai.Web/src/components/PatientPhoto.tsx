import { ImgProps } from "next/dist/shared/lib/get-img-props";
import Image from "next/image";
import { usePatientPhoto } from "@/lib/api/patientPhoto";

/**
 * Displays a patient's photo, or a default image when they have none.
 *
 * The photo is fetched through the authenticated API (see `usePatientPhoto`);
 * `hasPhoto` lets the component skip the request entirely when the patient
 * record says no photo exists.
 */
export function PatientPhoto({
  patientId,
  hasPhoto = false,
  ...imgProps
}: { patientId: number; hasPhoto?: boolean } & Partial<ImgProps>) {
  const pictureUrl = usePatientPhoto(patientId, hasPhoto);

  return (
    <Image
      src={pictureUrl ?? "/default-profile.webp"}
      unoptimized={true}
      alt="Patient Photo"
      height={100}
      width={100}
      {...imgProps}
    />
  );
}
