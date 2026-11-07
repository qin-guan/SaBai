import { useQuery } from "@tanstack/vue-query";
import {
  saBaiAPIEndpointsAuthRolesListGetEndpoint,
  saBaiAPIEndpointsAuthUsersListGetEndpoint,
} from "@sabai/sdk/clients";

export function useStaffUsersList() {
  return useQuery({
    queryKey: queryKeys.staffUsers(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsAuthUsersListGetEndpoint({ ...apiConfig, signal })
        .unwrap()
        .then((x) => x.users),
  });
}

export function useStaffRoles() {
  return useQuery({
    queryKey: queryKeys.staffRoles(),
    queryFn: ({ signal }) =>
      saBaiAPIEndpointsAuthRolesListGetEndpoint({ ...apiConfig, signal })
        .unwrap()
        .then((x) => x.roles),
    staleTime: Infinity,
  });
}

/** What an admin locked stays locked "forever"; failed sign-ins lock for minutes. */
export function isLockedByAdmin(user: StaffUser): boolean {
  return !!user.lockoutEnd && new Date(user.lockoutEnd).getFullYear() >= 9999;
}

export const ROLE_DESCRIPTIONS: Record<string, string> = {
  admin: "Everything, plus managing staff accounts.",
  member: "Clinic work: patients, stations, pharmacy and settings.",
};
