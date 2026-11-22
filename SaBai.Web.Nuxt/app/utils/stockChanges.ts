import type { SaBaiAPIEndpointsMedicationStockChangesGetResponse } from "@sabai/sdk/types";
import { formatQuantity } from "./quantity";

/**
 * A batch's change log (`GET /medication-stock/{id}/changes`) as events. The
 * API returns one row per changed field; rows written together (creating a
 * batch writes its quantity, location and status at one instant, an adjustment
 * its quantity and remarks) are one event here.
 */

export type StockChangeRow = SaBaiAPIEndpointsMedicationStockChangesGetResponse;
export type StockChangeKind = StockChangeRow["kind"];

export const STOCK_CHANGE_LABELS: Record<StockChangeKind, string> = {
  created: "Batch added",
  split_in: "Split off from another batch",
  split_out: "Split into other batches",
  dispensed: "Dispensed",
  adjustment: "Quantity adjusted",
  edit: "Details edited",
};

const FIELD_LABELS: Record<string, string> = {
  location: "Location",
  stock_status: "Status",
  remarks: "Remarks",
  quantity: "Quantity",
};

/** The kind an event is named after when its rows differ (an adjustment also edits the remarks). */
const KIND_PRIORITY: StockChangeKind[] = ["created", "split_in", "split_out", "dispensed", "adjustment", "edit"];

export interface StockLogEvent {
  /** The id of the event's first row. */
  id: string;
  kind: StockChangeKind;
  createdAt: string;
  userId: string;
  userName: string;
  /** Units added (+) or taken (−); null when the quantity did not change. */
  quantityChange: number | null;
  /** Units on hand after the event; null when the quantity did not change. */
  quantityAfter: number | null;
  /** The other fields that changed, in words ("Location: Shelf A → Shelf B"). */
  details: string[];
  dispensing: StockChangeRow["dispensing"];
}

function describeField(row: StockChangeRow, isNew: boolean): string | null {
  const label = FIELD_LABELS[row.field] ?? row.field;
  const before = row.previousValue?.trim() ?? "";
  const after = row.newValue?.trim() ?? "";
  if (isNew || !before) return after ? `${label}: ${after}` : null;
  if (!after) return `${label} removed (was ${before})`;
  return `${label}: ${before} → ${after}`;
}

/** Collapses the rows (newest first, as the API returns them) into events, keeping their order. */
export function groupStockChanges(rows: readonly StockChangeRow[]): StockLogEvent[] {
  const events: { rows: StockChangeRow[] }[] = [];
  for (const row of rows) {
    const last = events[events.length - 1];
    const first = last?.rows[0];
    const together = first
      && first.createdAt === row.createdAt
      && first.userId === row.userId
      // Two quantity changes are two events, even at the same instant.
      && !(row.field === "quantity" && last!.rows.some((r) => r.field === "quantity"));
    if (together) last!.rows.push(row);
    else events.push({ rows: [row] });
  }
  return events.map(({ rows: group }) => {
    const kind = KIND_PRIORITY.find((k) => group.some((r) => r.kind === k)) ?? group[0]!.kind;
    const quantityRow = group.find((r) => r.field === "quantity");
    const isNew = kind === "created" || kind === "split_in";
    const order = ["location", "stock_status", "remarks"];
    const details = group
      .filter((r) => r.field !== "quantity")
      .sort((a, b) => order.indexOf(a.field) - order.indexOf(b.field))
      .map((r) => describeField(r, isNew))
      .filter((text): text is string => !!text);
    return {
      id: group[0]!.id,
      kind,
      createdAt: group[0]!.createdAt,
      userId: group[0]!.userId,
      userName: group[0]!.userName,
      quantityChange: quantityRow?.quantityChange ?? null,
      quantityAfter: quantityRow?.quantityAfter ?? null,
      details,
      dispensing: group.find((r) => r.dispensing)?.dispensing ?? null,
    };
  });
}

/** Units a dispensing took from the batch. */
export function dispensedUnits(event: Pick<StockLogEvent, "quantityChange">): number {
  return event.quantityChange ? -event.quantityChange : 0;
}

/** Total units dispensed from the batch across `events`. */
export function totalDispensed(events: readonly StockLogEvent[]): number {
  return events.filter((e) => e.kind === "dispensed").reduce((sum, e) => sum + dispensedUnits(e), 0);
}

/** "+10 tablets" / "−4 tablets" (a real minus sign); "" when the quantity did not change. */
export function formatQuantityChange(change: number | null | undefined, unit?: string | null): string {
  if (change == null || change === 0) return "";
  return `${change > 0 ? "+" : "−"}${formatQuantity(Math.abs(change), unit)}`;
}

/** "22 Nov 2026, 17:21": the log's timestamp, short enough for a narrow column. */
export function formatLogTime(value: string): string {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return value;
  return date.toLocaleString("en-GB", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" });
}
