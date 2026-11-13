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
        .then((x) => x.users as StaffUser[]),
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

const PN_TAG = "PN";

/**
 * Dropdown options for picking a PN member on a referral. Only users with the
 * "PN" tag can be picked, while preserving a member already saved on the record
 * even if their PN tag was later removed.
 */
export function usePnMemberOptions(savedMemberId: MaybeRefOrGetter<string | null | undefined>) {
  const { data: users } = useStaffUsersList();

  return computed(() => {
    const savedId = toValue(savedMemberId);
    return (users.value ?? [])
      .filter(
        (u) =>
          (u.tags ?? []).some((t) => t.trim().toUpperCase() === PN_TAG) ||
          (!!savedId && u.id === savedId),
      )
      .map((u) => ({
        value: u.id,
        label: u.nickname || u.userName,
      }));
  });
}
