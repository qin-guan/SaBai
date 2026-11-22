export function PatientCode({
  villageCode,
  villageColorHex,
  displayId,
  className = "",
}: {
  villageCode: string | null;
  villageColorHex: string | null;
  /** The patient's display code from the API (village code + number), e.g. "PC0012". */
  displayId: string;
  className?: string;
}) {
  return (
    <span className={`inline-flex items-center gap-2 ${className}`}>
      {villageColorHex && (
        <span
          className="h-3 w-3 rounded-full"
          style={{ backgroundColor: villageColorHex }}
          title={villageCode ?? undefined}
        />
      )}
      {displayId}
    </span>
  );
}
