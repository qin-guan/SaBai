import type { PharmacyQueue } from "./medicationCache";

/**
 * Narrowing the pharmacist's queue (`GET /pharmacy-orders`: patients → visits
 * → pending orders). A patient or visit left without orders drops out.
 */

export interface QueueFilter {
  /**
   * Words to find: in the patient's name or ID, or a visit's diagnoses (keeps
   * all of its orders), or in an order's medication, instructions, remarks or
   * the brand and location of its batches.
   */
  query?: string;
  /** A village code; empty for all. */
  villageCode?: string;
  /** Only orders of an ingredient that is low on stock. */
  lowStockOnly?: boolean;
  /** Deep link: one visit. */
  visitId?: string | null;
  /** Deep link: a patient by display ID, record id (see `patientIdOf`) or part of the name. */
  patient?: string | null;
}

export interface QueueFilterContext {
  /** An order's remarks (the queue items do not carry them). */
  remarksOf?: (orderId: string) => string | null | undefined;
  /** The record id of a queue patient, when the patient list is loaded. */
  patientIdOf?: (displayId: string) => string | null | undefined;
}

const fold = (text: string | null | undefined) => (text ?? "").toLowerCase();

export function filterPharmacyQueue(
  queue: readonly PharmacyQueue[],
  filter: QueueFilter,
  context: QueueFilterContext = {},
): PharmacyQueue[] {
  const query = fold(filter.query).trim();
  const patient = fold(filter.patient).trim();
  const result: PharmacyQueue[] = [];
  for (const group of queue) {
    if (filter.villageCode && group.patient.villageCode !== filter.villageCode) continue;
    if (patient) {
      const matches =
        fold(group.patient.displayId) === patient
        || fold(context.patientIdOf?.(group.patient.displayId)) === patient
        || fold(group.patient.name).includes(patient);
      if (!matches) continue;
    }
    const patientMatches = !!query && (fold(group.patient.name).includes(query) || fold(group.patient.displayId).includes(query));
    const visits = group.visits
      .filter((visit) => !filter.visitId || String(visit.visitId) === filter.visitId)
      .map((visit) => {
        const visitMatches =
          patientMatches
          || visit.diagnoses.some((d) => fold(d.details).includes(query) || fold(d.category).includes(query));
        const orders = visit.orders.filter((order) => {
          if (filter.lowStockOnly && !order.isLowStock) return false;
          if (!query || visitMatches) return true;
          return (
            fold(order.medicationName).includes(query)
            || fold(order.dosageInstructions).includes(query)
            || fold(context.remarksOf?.(order.orderId)).includes(query)
            || order.allocations.some((a) => fold(a.brandName).includes(query) || fold(a.location).includes(query))
          );
        });
        return orders.length === visit.orders.length ? visit : { ...visit, orders };
      })
      .filter((visit) => visit.orders.length > 0);
    if (visits.length > 0) result.push({ ...group, visits });
  }
  return result;
}

/** How many patients and pending orders a queue holds. */
export function countPharmacyQueue(queue: readonly PharmacyQueue[]): { patients: number; orders: number } {
  return {
    patients: queue.length,
    orders: queue.reduce((n, group) => n + group.visits.reduce((m, visit) => m + visit.orders.length, 0), 0),
  };
}
