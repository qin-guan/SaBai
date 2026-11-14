import { useQuery, useQueryClient, type QueryClient } from "@tanstack/vue-query";
import {
  saBaiAPIEndpointsAuthLogoutPostEndpoint,
  saBaiAPIEndpointsAuthWhoAmIGetEndpoint,
} from "@sabai/sdk/clients";
import type { SaBaiAPIEndpointsAuthUserResponse } from "@sabai/sdk/types";

export type StaffUser = SaBaiAPIEndpointsAuthUserResponse;

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
    meta: { skipAuthRedirect: true },
  };
}

/**
 * Resolves the session for route guards: the cached user, or a fresh `whoami`.
 * Returns null when signed out.
 */
export async function loadCurrentUser(queryClient: QueryClient): Promise<StaffUser | null> {
  try {
    const user = await queryClient.ensureQueryData(currentUserQueryOptions());
    if (user && import.meta.client) {
      const nuxtApp = useNuxtApp();
      (nuxtApp.$connectPowerSync as (() => void) | undefined)?.();
    }
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
 * Ends the session on the API (clears the HttpOnly cookie), drops all cached
 * data and local patient data, then goes to the login page.
 */
export function useSignOut() {
  const queryClient = useQueryClient();
  return async (options?: { force?: boolean }) => {
    const pending = await refreshPendingCrudCount();
    if (pending > 0 && !options?.force) {
      const { toast } = await import("vue-sonner");
      toast.error(
        `You have ${pending} unsynced offline change(s). Connect to sync before signing out.`,
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
    try {
      const db = getPowerSyncInstance();
      await db.disconnectAndClear();
    } catch {
      // ignore if PowerSync is not initialized
    }
    // Drafts and recent patients hold patient data; don't leave them on a shared device.
    clearAllFormDrafts();
    clearRecentPatients();
    queryClient.clear();
    await navigateTo(paths.login());
  };
}
