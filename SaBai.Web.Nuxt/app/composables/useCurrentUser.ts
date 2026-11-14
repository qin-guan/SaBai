import { useQuery, useQueryClient, type QueryClient } from "@tanstack/vue-query";
import { toast } from "vue-sonner";
import {
  saBaiAPIEndpointsAuthLogoutPostEndpoint,
  saBaiAPIEndpointsAuthWhoAmIGetEndpoint,
} from "@sabai/sdk/clients";
import type { SaBaiAPIEndpointsAuthUserResponse } from "@sabai/sdk/types";
import { clearOfflineData, getOfflineRuntime, startOfflineSync } from "~/offline/runtime";

export type StaffUser = SaBaiAPIEndpointsAuthUserResponse & { tags?: string[] };

export const ADMIN_ROLE = "admin";

const CACHED_USER_KEY = "sabai.cachedUser";

function readCachedUser(): StaffUser | null {
  if (typeof localStorage === "undefined") return null;
  try {
    const raw = localStorage.getItem(CACHED_USER_KEY);
    if (!raw) return null;
    return JSON.parse(raw) as StaffUser;
  } catch {
    return null;
  }
}

function writeCachedUser(user: StaffUser | null) {
  if (typeof localStorage === "undefined") return;
  try {
    if (user) {
      localStorage.setItem(CACHED_USER_KEY, JSON.stringify(user));
    } else {
      localStorage.removeItem(CACHED_USER_KEY);
    }
  } catch {
    // ignore storage quota errors
  }
}

/** Signed-in user query. A 401 here is handled by the route middleware, not the global redirect. */
export function currentUserQueryOptions() {
  return {
    queryKey: queryKeys.currentUser(),
    queryFn: async ({ signal }: { signal?: AbortSignal }) => {
      try {
        const user = await saBaiAPIEndpointsAuthWhoAmIGetEndpoint({ ...apiConfig, signal })
          .unwrap()
          .then((x) => x.user);
        writeCachedUser(user);
        return user;
      } catch (error) {
        if (isUnauthorizedError(error)) {
          writeCachedUser(null);
          throw error;
        }
        // Offline / network failure: keep working with the last signed-in staff user.
        const cached = readCachedUser();
        if (cached) return cached;
        throw error;
      }
    },
    // Roles change rarely; the API re-checks the session on every request anyway.
    staleTime: 5 * 60 * 1000,
    retry: false,
    // Not saved with the offline cache: on start-up the session is asked for
    // afresh (falling back to the cached user above when offline).
    meta: { skipAuthRedirect: true, persist: false },
  };
}

/**
 * Resolves the session for route guards: the cached user, or a fresh `whoami`.
 * Returns null when signed out.
 */
export async function loadCurrentUser(queryClient: QueryClient): Promise<StaffUser | null> {
  try {
    const user = await queryClient.ensureQueryData(currentUserQueryOptions());
    // Signed in: send queued writes and download the offline working set when due.
    if (user && import.meta.client) startOfflineSync();
    return user;
  } catch (error) {
    if (isUnauthorizedError(error)) return null;
    const cached = readCachedUser();
    if (cached) return cached;
    throw error;
  }
}

export function isAdmin(user: StaffUser | null | undefined): boolean {
  return !!user?.roles.includes(ADMIN_ROLE);
}

export function useCurrentUser() {
  const query = useQuery(currentUserQueryOptions());
  return {
    ...query,
    user: computed(() => query.data.value ?? null),
    isAdmin: computed(() => isAdmin(query.data.value)),
  };
}

/**
 * Ends the session on the API (clears the HttpOnly cookie), then removes
 * everything this device keeps for the user — cached data, queued writes,
 * drafts — and goes to the login page. Writes that have not been sent yet
 * would be lost, so it asks first.
 */
export function useSignOut() {
  const queryClient = useQueryClient();
  const signOut = async (options?: { force?: boolean }) => {
    const unsent = import.meta.client ? getOfflineRuntime().outbox.getItems().length : 0;
    if (unsent > 0 && options?.force !== true) {
      toast.warning(
        `${unsent} change${unsent === 1 ? " has" : "s have"} not been sent to the server yet. Signing out deletes ${unsent === 1 ? "it" : "them"} from this device.`,
        {
          description: "Connect and open Sync to send them first.",
          duration: 15_000,
          action: { label: "Delete and sign out", onClick: () => void signOut({ force: true }) },
        },
      );
      return;
    }

    // Notifications belong to the person signed in: stop them on this device
    // (while the session can still say so) before it is handed to someone else.
    await stopPushOnThisDevice().catch(console.error);
    try {
      await saBaiAPIEndpointsAuthLogoutPostEndpoint({ ...apiConfig, throwOnError: true });
    } catch (error) {
      console.error(error);
    }
    writeCachedUser(null);
    // Cached and queued patient data must not stay on a shared device.
    await clearOfflineData().catch(console.error);
    // Drafts and recent patients hold patient data too.
    clearAllFormDrafts();
    clearRecentPatients();
    queryClient.clear();
    await navigateTo(paths.login());
  };
  return signOut;
}
