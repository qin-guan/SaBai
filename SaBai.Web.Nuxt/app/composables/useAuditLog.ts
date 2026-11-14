import { useInfiniteQuery } from "@tanstack/vue-query";
import { saBaiAPIEndpointsAuditLogsListGetEndpoint } from "@sabai/sdk/clients";
import type {
  SaBaiAPIDomainAuditActionKey,
  SaBaiAPIEndpointsAuditLogsListGetAuditLogEntryResponse,
} from "@sabai/sdk/types";
import type { MaybeRefOrGetter } from "vue";

export type AuditLogEntry = SaBaiAPIEndpointsAuditLogsListGetAuditLogEntryResponse;
export type AuditAction = SaBaiAPIDomainAuditActionKey;

export interface AuditLogFilters {
  action?: AuditAction;
  userId?: string;
  entityType?: string;
  entityId?: string;
  /** `yyyy-mm-dd`, local day; inclusive. */
  fromDate?: string;
  /** `yyyy-mm-dd`, local day; inclusive. */
  toDate?: string;
}

const PAGE_SIZE = 50;

export const AUDIT_ACTION_LABELS: Record<AuditAction, string> = {
  Created: "Created",
  Updated: "Updated",
  Deleted: "Deleted",
  SignedIn: "Signed in",
  SignInFailed: "Sign-in failed",
  SignedOut: "Signed out",
  Read: "Viewed",
  Exported: "Exported",
  Disclosed: "Disclosed",
};

/** Actions that record who looked at or released patient data (HIPAA access log), not data changes. */
export const PHI_ACCESS_ACTIONS: AuditAction[] = ["Read", "Exported", "Disclosed"];

/** Start of a local `yyyy-mm-dd` day as an ISO instant, optionally a number of days later. */
function localDayStart(date: string, addDays = 0): string {
  const [y, m, d] = date.split("-").map(Number);
  return new Date(y!, m! - 1, d! + addDays).toISOString();
}

/** Audit entries, newest first, fetched a page at a time (admin only). */
export function useAuditLog(filters: MaybeRefOrGetter<AuditLogFilters>) {
  return useInfiniteQuery({
    queryKey: computed(() => [...queryKeys.auditLog(), toValue(filters)] as const),
    initialPageParam: undefined as string | undefined,
    queryFn: ({ pageParam, signal }) => {
      const f = toValue(filters);
      return saBaiAPIEndpointsAuditLogsListGetEndpoint({
        ...apiConfig,
        signal,
        query: {
          action: f.action,
          userId: f.userId || undefined,
          // Accept the displayed form too ("Village Code" → "VillageCode").
          entityType: f.entityType?.replace(/\s+/g, "") || undefined,
          entityId: f.entityId?.trim() || undefined,
          from: f.fromDate ? localDayStart(f.fromDate) : undefined,
          to: f.toDate ? localDayStart(f.toDate, 1) : undefined,
          before: pageParam,
          take: PAGE_SIZE,
        },
      }).unwrap();
    },
    getNextPageParam: (last) => last.nextBefore ?? undefined,
  });
}

/** Splits `PascalCase` type and field names into words for display. */
export function humanizeName(name: string): string {
  return name.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z]+)([A-Z][a-z])/g, "$1 $2");
}
