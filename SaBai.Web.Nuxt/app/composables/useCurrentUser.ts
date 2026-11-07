import { useQuery, useQueryClient, type QueryClient } from "@tanstack/vue-query";
import {
  saBaiAPIEndpointsAuthLogoutPostEndpoint,
  saBaiAPIEndpointsAuthWhoAmIGetEndpoint,
} from "@sabai/sdk/clients";
import type { SaBaiAPIEndpointsAuthUserResponse } from "@sabai/sdk/types";

export type StaffUser = SaBaiAPIEndpointsAuthUserResponse;

export const ADMIN_ROLE = "admin";

/** Signed-in user query. A 401 here is handled by the route middleware, not the global redirect. */
export function currentUserQueryOptions() {
  return {
    queryKey: queryKeys.currentUser(),
    queryFn: ({ signal }: { signal?: AbortSignal }) =>
      saBaiAPIEndpointsAuthWhoAmIGetEndpoint({ ...apiConfig, signal })
        .unwrap()
        .then((x) => x.user),
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
    return await queryClient.ensureQueryData(currentUserQueryOptions());
  } catch (error) {
    if (isUnauthorizedError(error)) return null;
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
  return async () => {
    try {
      await saBaiAPIEndpointsAuthLogoutPostEndpoint({ ...apiConfig, throwOnError: true });
    } catch (error) {
      console.error(error);
    }
    // Drafts and recent patients hold patient data; don't leave them on a shared device.
    clearAllFormDrafts();
    clearRecentPatients();
    queryClient.clear();
    await navigateTo(paths.login());
  };
}
