import type {
  SaBaiAPIDomainStockStatusKey,
  SaBaiAPIEndpointsMedicationStockWithBrandGetResponse,
  SaBaiAPIDomainOrderStatusKey,
  SaBaiAPIEndpointsMedicationStockByIngredientGetResponse,
  SaBaiAPIEndpointsOrdersAllocationDetails,
  SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem,
  SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem,
  SaBaiAPIEndpointsPharmacyOrdersGetResponse,
} from "@sabai/sdk/types";

/**
 * Pure list edits behind the medication offline writes. Every function returns
 * new arrays/objects and is safe to apply again with the same input (the
 * offline layer re-applies a queued write's edit after each refetch).
 */

export type StockRowData = SaBaiAPIEndpointsMedicationStockWithBrandGetResponse;
export type IngredientAvailability = SaBaiAPIEndpointsMedicationStockByIngredientGetResponse;
export type OrderAllocation = SaBaiAPIEndpointsOrdersAllocationDetails;

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

// ── Pending quantity changes ─────────────────────────────────────────────
//
// Orders change quantities the server computes (an ingredient's available
// units, a batch's reserved and on-hand units). A queued write cannot set those
// to an absolute value: the server's number moves with other people's orders.
// It records a delta under its own key instead, next to the server's value the
// deltas apply to. Recording the same key again changes nothing, so re-applying
// a queued write never counts it twice; fresh data from the server carries no
// deltas, so each queued write applies once on top of it.

type Deltas = Record<string, number>;

interface PendingQuantities {
  /** Deltas by the key of the write that made them. */
  pendingDeltas?: Record<string, Deltas>;
  /** The server's values of the fields the deltas change. */
  serverValues?: Deltas;
}

/** Numeric fields of `T`. */
type NumericKey<T> = { [K in keyof T]-?: T[K] extends number ? K : never }[keyof T] & string;

/**
 * Records (`delta`) or removes (`null`) the change written under `key` and
 * recomputes the changed fields from the server's values. Returns `row`
 * itself when there is nothing to change.
 */
export function withPendingDelta<T extends object>(
  row: T,
  key: string,
  delta: Partial<Record<NumericKey<T>, number>> | null,
): T {
  const tracked = row as T & PendingQuantities;
  const pending = { ...tracked.pendingDeltas };
  if (delta) {
    if (pending[key]) return row;
    pending[key] = delta as Deltas;
  } else {
    if (!pending[key]) return row;
    delete pending[key];
  }

  const fields = row as unknown as Deltas;
  const server: Deltas = { ...tracked.serverValues };
  for (const field of Object.keys(delta ?? {})) {
    if (!(field in server)) server[field] = fields[field] ?? 0;
  }

  const values: Deltas = { ...server };
  for (const change of Object.values(pending)) {
    for (const [field, amount] of Object.entries(change)) values[field] = (values[field] ?? 0) + amount;
  }

  const { pendingDeltas: _pending, serverValues: _server, ...rest } = tracked;
  if (Object.keys(pending).length === 0) return { ...rest, ...server } as T;
  return { ...rest, ...values, pendingDeltas: pending, serverValues: server } as T;
}

/** True while `row` shows a change that has not reached the server. */
export function hasPendingDelta(row: object): boolean {
  const { pendingDeltas } = row as PendingQuantities;
  return !!pendingDeltas && Object.keys(pendingDeltas).length > 0;
}

// ── Ingredient availability (GET /medication-stock/by-ingredient) ────────

/** Adds (`delta` > 0) or takes away units an ingredient can still be ordered with, under `key`; `null` removes that change. */
export function adjustAvailability(
  rows: readonly IngredientAvailability[],
  ingredientId: string,
  key: string,
  delta: number | null,
): IngredientAvailability[] {
  return rows.map((row) =>
    row.ingredientId === ingredientId
      ? withPendingDelta(row, key, delta === null ? null : { availableQuantity: delta })
      : row,
  );
}

// ── Stock lines (GET /medication-stock/with-brand) ───────────────────────

/**
 * The API flags a line as low when it is active and the available (not
 * reserved) quantity of its whole ingredient is at or below the ingredient's
 * "fall below" level.
 */
export function withLowStockFlags<T extends StockRowData>(rows: readonly T[]): T[] {
  const activeTotals = new Map<string, number>();
  for (const row of rows) {
    if (row.stockStatus === "active") {
      activeTotals.set(row.ingredientId, (activeTotals.get(row.ingredientId) ?? 0) + row.availableQuantity);
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
 * Applies a split: the parent keeps only its reserved units (and leaves the
 * list when it has none) and one new line per split appears, keyed by the
 * split's own id. `parent` is the line as it was when the user split it, so the
 * result does not depend on what the cache holds now.
 */
export function applySplit(
  rows: readonly StockRowData[],
  parent: StockRowData,
  splits: readonly StockSplitData[],
): StockRowData[] {
  let next = patchStock(rows, parent.id, {
    quantity: parent.reservedQuantity,
    availableQuantity: 0,
  });
  for (const split of splits) {
    next = upsertStock(next, {
      ...parent,
      id: split.id,
      quantity: split.quantity,
      reservedQuantity: 0,
      availableQuantity: split.quantity,
      location: split.location,
      stockStatus: split.stockStatus,
      remarks: split.remarks,
      parentStockId: parent.id,
    });
  }
  return next;
}

/** How an order changes the batches it holds units of. */
export type AllocationChange = "dispense" | "release" | "reserve" | "take";

const ALLOCATION_DELTAS: Record<AllocationChange, (units: number) => Partial<Record<"quantity" | "reservedQuantity" | "availableQuantity", number>>> = {
  dispense: (units) => ({ quantity: -units, reservedQuantity: -units }),
  release: (units) => ({ reservedQuantity: -units, availableQuantity: units }),
  reserve: (units) => ({ reservedQuantity: units, availableQuantity: -units }),
  take: (units) => ({ quantity: -units, availableQuantity: -units }),
};

/**
 * Applies a dispense or release of an order's allocations to the stock lines,
 * under `key`; `null` change removes it. Dispensing takes the units off hand
 * and out of the reservation (available stays the same); releasing only ends
 * the reservation (they become available again). When the pharmacist picks
 * other batches, the new ones are reserved (`reserve`), or, when dispensing
 * straight from them, taken off hand without having been reserved (`take`).
 */
export function applyAllocations(
  rows: readonly StockRowData[],
  allocations: readonly Pick<OrderAllocation, "stockId" | "quantity">[],
  key: string,
  change: AllocationChange | null,
): StockRowData[] {
  const byStock = new Map<string, number>();
  for (const a of allocations) byStock.set(a.stockId, (byStock.get(a.stockId) ?? 0) + a.quantity);
  const next = rows.map((row) => {
    const units = byStock.get(row.id);
    if (units === undefined) return row;
    if (change === null) return withPendingDelta(row, key, null);
    return withPendingDelta(row, key, ALLOCATION_DELTAS[change](units));
  });
  return visibleStock(next);
}

/**
 * A manual quantity adjustment of one batch (a stock count correction), under
 * `key`: its on-hand and available units move by `quantityChange`.
 */
export function applyStockAdjustment(
  rows: readonly StockRowData[],
  stockId: string,
  key: string,
  quantityChange: number,
): StockRowData[] {
  return visibleStock(
    rows.map((row) =>
      row.id === stockId
        ? withPendingDelta(row, key, { quantity: quantityChange, availableQuantity: quantityChange })
        : row,
    ),
  );
}

// ── Pharmacy queue (GET /pharmacy-orders) ────────────────────────────────

export type PharmacyQueue = SaBaiAPIEndpointsPharmacyOrdersGetResponse;
export type PharmacyQueueOrder = SaBaiAPIEndpointsPharmacyOrdersGetPharmacyOrderItem;

/**
 * Idempotently inserts or replaces an order in the pharmacy queue.
 */
export function upsertOrderInPharmacyQueue(
  queue: readonly PharmacyQueue[],
  input: {
    orderId: string;
    medicationActiveIngredientId?: string;
    medicationName: string;
    /** The ingredient's unit, when known ("tablet"). */
    unitOfMeasurement?: string;
    quantity: number;
    isLowStock: boolean;
    dosageInstructions: string;
    allocations: readonly OrderAllocation[];
    patientDisplayId: string;
    patientName: string;
    villageCode: string | null;
    visitId: string;
    visitDate: string;
    diagnoses: readonly SaBaiAPIEndpointsPharmacyOrdersGetDiagnosisItem[];
  },
): PharmacyQueue[] {
  const orderItem: PharmacyQueueOrder = {
    orderId: input.orderId,
    medicationActiveIngredientId: input.medicationActiveIngredientId ?? "",
    medicationName: input.medicationName,
    unitOfMeasurement: input.unitOfMeasurement ?? "",
    quantity: input.quantity,
    dosageInstructions: input.dosageInstructions,
    isLowStock: input.isLowStock,
    allocations: [...input.allocations],
  };

  const queueCopy = [...queue];
  const patientIndex = queueCopy.findIndex((p) => p.patient.displayId === input.patientDisplayId);

  if (patientIndex === -1) {
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
  const visitIndex = visitsCopy.findIndex((v) => v.visitId === input.visitId);

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
 * Patches an order in the pharmacy queue (the queue lists pending orders, so a
 * dispensed or cancelled order leaves it).
 */
export function patchOrderInPharmacyQueue(
  queue: readonly PharmacyQueue[],
  orderId: string,
  patch: {
    status?: SaBaiAPIDomainOrderStatusKey;
    dosageInstructions?: string;
    medicationName?: string;
    medicationActiveIngredientId?: string;
    unitOfMeasurement?: string;
    quantity?: number;
    allocations?: readonly OrderAllocation[];
  },
): PharmacyQueue[] {
  if (patch.status === "DISPENSED" || patch.status === "CANCELLED") {
    return removeOrderFromPharmacyQueue(queue, orderId);
  }

  const { status: _status, allocations, ...fields } = patch;
  const changes: Partial<PharmacyQueueOrder> = Object.fromEntries(
    Object.entries(fields).filter(([, value]) => value !== undefined),
  );
  if (allocations) changes.allocations = [...allocations];
  if (Object.keys(changes).length === 0) return [...queue];

  return queue.map((patientGroup) => ({
    ...patientGroup,
    visits: patientGroup.visits.map((visit) => ({
      ...visit,
      orders: visit.orders.map((order) => (order.orderId === orderId ? { ...order, ...changes } : order)),
    })),
  }));
}

/**
 * Removes an order from the pharmacy queue and prunes empty visits/patient groups.
 */
export function removeOrderFromPharmacyQueue(
  queue: readonly PharmacyQueue[],
  orderId: string,
): PharmacyQueue[] {
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
