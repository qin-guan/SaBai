import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
  SaBaiAPIDomainOrderStatusKey,
  SaBaiAPIEndpointsOrdersListGetResponse,
  SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem,
  SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem,
  SaBaiAPIEndpointsPharmacyOrdersGetResponse,
  SaBaiAPIEndpointsPharmacyOrdersGetVisitBundle,
} from "@sabai/sdk/types";

/**
 * Pure list edits behind the medication offline writes. Every function returns
 * new arrays/objects and is safe to apply again with the same input (the
 * offline layer re-applies a queued write's edit after each refetch).
 */

export type StockRowData = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse;

export interface StockSplitData {
  id: string;
  quantity: number;
  location: string;
  stockStatus: SaBaiAPIDomainStockStatusKey;
  remarks: string | null;
}

/** Replaces the entry with the same id in place, or adds `item` at the start / end. */
export function upsertById<T extends { id: string }>(
  list: readonly T[],
  item: T,
  addAt: "start" | "end" = "end",
): T[] {
  if (list.some((x) => x.id === item.id)) return list.map((x) => (x.id === item.id ? item : x));
  return addAt === "start" ? [item, ...list] : [...list, item];
}

/**
 * The API flags a line as low when it is active and the active quantity of its
 * whole ingredient is at or below the ingredient's "fall below" level.
 */
export function withLowStockFlags<T extends StockRowData>(rows: readonly T[]): T[] {
  const activeTotals = new Map<string, number>();
  for (const row of rows) {
    if (row.stockStatus === "active") {
      activeTotals.set(row.ingredientId, (activeTotals.get(row.ingredientId) ?? 0) + row.quantity);
    }
  }
  return rows.map((row) => {
    const isLowStock =
      row.stockStatus === "active" && (activeTotals.get(row.ingredientId) ?? 0) <= row.fallBelow;
    return row.isLowStock === isLowStock ? row : { ...row, isLowStock };
  });
}

/** Stock lists show only lots that still have quantity, newest (largest id) first. */
function visibleStock<T extends StockRowData>(rows: readonly T[]): T[] {
  return withLowStockFlags(rows.filter((row) => row.quantity !== 0));
}

/** Adds or replaces a stock line, keeping the list newest first. */
export function upsertStock(rows: readonly StockRowData[], row: StockRowData): StockRowData[] {
  const without = rows.filter((x) => x.id !== row.id);
  const index = without.findIndex((x) => x.id < row.id);
  const next = [...without];
  next.splice(index === -1 ? next.length : index, 0, row);
  return visibleStock(next);
}

/** Changes fields of one stock line; lines left without quantity drop out, as in the API list. */
export function patchStock(
  rows: readonly StockRowData[],
  id: string,
  patch: Partial<StockRowData>,
): StockRowData[] {
  return visibleStock(rows.map((x) => (x.id === id ? { ...x, ...patch } : x)));
}

/**
 * Applies a split: the parent line ends at zero quantity (and so leaves the
 * list) and one new line per split appears, keyed by the split's own id.
 * `parent` is the line as it was when the user split it, so the result does not
 * depend on what the cache holds now.
 */
export function applySplit(
  rows: readonly StockRowData[],
  parent: StockRowData,
  splits: readonly StockSplitData[],
): StockRowData[] {
  let next = rows.filter((x) => x.id !== parent.id);
  for (const split of splits) {
    next = upsertStock(next, {
      ...parent,
      id: split.id,
      quantity: split.quantity,
      location: split.location,
      stockStatus: split.stockStatus,
      remarks: split.remarks,
    });
  }
  return next;
}

/**
 * Deducts quantity across active stock lots for a medication brand using FIFO (earliest expiry first, then id).
 * If `snapshotLots` is provided, first restores/merges `snapshotLots` by id into `rows` for idempotency.
 * Any lot reaching quantity === 0 is dropped. Returns visible stock.
 */
export function deductStockFifo(
  rows: readonly StockRowData[],
  medicationBrandId: string,
  quantityToDeduct: number,
  snapshotLots?: readonly StockRowData[],
): StockRowData[] {
  // Merge snapshot lots if provided
  let working = [...rows];
  if (snapshotLots) {
    for (const snap of snapshotLots) {
      if (!working.some((r) => r.id === snap.id)) {
        working.push(snap);
      } else {
        working = working.map((r) => (r.id === snap.id ? { ...snap } : r));
      }
    }
  }

  // Find active lots for this brand with quantity > 0
  const activeLots = working.filter(
    (r) => r.medicationBrandId === medicationBrandId && r.stockStatus === "active" && r.quantity > 0,
  );

  // Sort by expiry ascending, then id ascending
  activeLots.sort((a, b) => {
    const expA = new Date(a.expiry).getTime();
    const expB = new Date(b.expiry).getTime();
    if (expA !== expB) return expA - expB;
    return a.id.localeCompare(b.id);
  });

  const deductions = new Map<string, number>();
  let remaining = quantityToDeduct;

  for (const lot of activeLots) {
    if (remaining <= 0) break;
    const take = Math.min(lot.quantity, remaining);
    deductions.set(lot.id, lot.quantity - take);
    remaining -= take;
  }

  // Apply deductions across working rows
  let updated = working.map((row) => {
    if (deductions.has(row.id)) {
      return { ...row, quantity: deductions.get(row.id)! };
    }
    return row;
  });

  return visibleStock(updated);
}

/**
 * Idempotently inserts or replaces an order in the pharmacy queue.
 */
export function upsertOrderInPharmacyQueue(
  queue: readonly SaBaiAPIEndpointsPharmacyOrdersGetResponse[],
  input: {
    orderId: string;
    medicationName: string;
    quantity: number;
    isLowStock: boolean;
    dosageInstructions: string;
    patientDisplayId: string;
    patientName: string;
    villageCode: string | null;
    visitId: string;
    visitDate: string;
    diagnoses: readonly SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem[];
  },
): SaBaiAPIEndpointsPharmacyOrdersGetResponse[] {
  const orderItem: SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem = {
    orderId: input.orderId,
    medicationName: input.medicationName,
    quantity: input.quantity,
    dosageInstructions: input.dosageInstructions,
    isLowStock: input.isLowStock,
  };

  const queueCopy = [...queue];
  let patientIndex = queueCopy.findIndex((p) => p.patient.displayId === input.patientDisplayId);

  if (patientIndex === -1) {
    // Create new patient group with new visit bundle
    queueCopy.push({
      patient: {
        displayId: input.patientDisplayId,
        name: input.patientName,
        villageCode: input.villageCode,
      },
      visits: [
        {
          visitId: input.visitId,
          visitDate: input.visitDate,
          orders: [orderItem],
          diagnoses: [...input.diagnoses],
        },
      ],
    });
    return queueCopy;
  }

  const patientGroup = { ...queueCopy[patientIndex]! };
  const visitsCopy = [...patientGroup.visits];
  let visitIndex = visitsCopy.findIndex((v) => v.visitId === input.visitId);

  if (visitIndex === -1) {
    visitsCopy.push({
      visitId: input.visitId,
      visitDate: input.visitDate,
      orders: [orderItem],
      diagnoses: [...input.diagnoses],
    });
  } else {
    const visitBundle = { ...visitsCopy[visitIndex]! };
    const ordersCopy = [...visitBundle.orders];
    const orderExistsIndex = ordersCopy.findIndex((o) => o.orderId === input.orderId);
    if (orderExistsIndex === -1) {
      ordersCopy.push(orderItem);
    } else {
      ordersCopy[orderExistsIndex] = orderItem;
    }
    const diagnoses = visitBundle.diagnoses.length > 0 ? visitBundle.diagnoses : [...input.diagnoses];
    visitsCopy[visitIndex] = { ...visitBundle, orders: ordersCopy, diagnoses };
  }

  patientGroup.visits = visitsCopy;
  queueCopy[patientIndex] = patientGroup;
  return queueCopy;
}

/**
 * Patches an order in the pharmacy queue. If status is APPROVED or CANCELLED, removes it.
 */
export function patchOrderInPharmacyQueue(
  queue: readonly SaBaiAPIEndpointsPharmacyOrdersGetResponse[],
  orderId: string,
  patch: { status?: SaBaiAPIDomainOrderStatusKey; dosageInstructions?: string | null },
): SaBaiAPIEndpointsPharmacyOrdersGetResponse[] {
  if (patch.status === "APPROVED" || patch.status === "CANCELLED") {
    return removeOrderFromPharmacyQueue(queue, orderId);
  }

  if (patch.dosageInstructions === undefined) {
    return [...queue];
  }

  return queue
    .map((patientGroup) => {
      const visits = patientGroup.visits
        .map((visit) => {
          const orders = visit.orders.map((order) => {
            if (order.orderId === orderId) {
              return { ...order, dosageInstructions: patch.dosageInstructions ?? "" };
            }
            return order;
          });
          return { ...visit, orders };
        })
        .filter((visit) => visit.orders.length > 0);
      return { ...patientGroup, visits };
    })
    .filter((patientGroup) => patientGroup.visits.length > 0);
}

/**
 * Removes an order from the pharmacy queue and prunes empty visits/patient groups.
 */
export function removeOrderFromPharmacyQueue(
  queue: readonly SaBaiAPIEndpointsPharmacyOrdersGetResponse[],
  orderId: string,
): SaBaiAPIEndpointsPharmacyOrdersGetResponse[] {
  return queue
    .map((patientGroup) => {
      const visits = patientGroup.visits
        .map((visit) => {
          const orders = visit.orders.filter((order) => order.orderId !== orderId);
          return { ...visit, orders };
        })
        .filter((visit) => visit.orders.length > 0);
      return { ...patientGroup, visits };
    })
    .filter((patientGroup) => patientGroup.visits.length > 0);
}

