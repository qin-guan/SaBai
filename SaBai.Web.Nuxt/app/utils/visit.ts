/**
 * Formats a visit date into a readable string in GB locale,
 * e.g. "22 June 2026, 14:30".
 */
export function formatVisitDate(date: Date | string): string {
  return new Date(date).toLocaleString("en-GB", {
    day: "numeric",
    month: "long",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}
