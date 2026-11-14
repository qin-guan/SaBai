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
  pn: "Patient Navigation: manage referral booth, party, and payment details.",
};

const PN_ROLE = "pn";

/**
 * Dropdown options for picking a PN member on a referral. Only users with the
 * "pn" Identity role can be picked, while preserving a member already saved on
 * the record even if their "pn" role was later removed.
 */
export function usePnMemberOptions(savedMemberId: MaybeRefOrGetter<string | null | undefined>) {
  const { data: users } = useStaffUsersList();
  const { data: localUsers } = useLocalQuery<Array<{ id: string; user_name: string | null; nickname: string | null }>>(
    ["staff_users"],
    async () => {
      const db = getPowerSyncInstance();
      return await db.getAll<{ id: string; user_name: string | null; nickname: string | null }>(
        "SELECT id, user_name, nickname FROM staff_users ORDER BY COALESCE(nickname, user_name) ASC",
      );
    },
  );

  return computed(() => {
    const savedId = toValue(savedMemberId);
    if (users.value?.length) {
      return users.value
        .filter(
          (u) =>
            u.roles.some((r) => r.trim().toLowerCase() === PN_ROLE) ||
            (!!savedId && u.id === savedId),
        )
        .map((u) => ({
          value: u.id,
          label: u.nickname || u.userName,
        }));
    }
    return (localUsers.value ?? []).map((u) => ({
      value: u.id,
      label: u.nickname || u.user_name || u.id,
    }));
  });
}

export type StaffUserStatus = "active" | "locked" | "needs-password" | "2fa";

export const STAFF_USER_STATUS_OPTIONS: { value: StaffUserStatus; label: string }[] = [
  { value: "active", label: "Active" },
  { value: "locked", label: "Locked" },
  { value: "needs-password", label: "Needs password" },
  { value: "2fa", label: "2FA enabled" },
];

export function matchesSingleStaffUserStatus(
  user: Pick<StaffUser, "isLocked" | "hasPassword" | "twoFactorEnabled">,
  status: StaffUserStatus,
): boolean {
  switch (status) {
    case "active":
      return !user.isLocked && user.hasPassword;
    case "locked":
      return user.isLocked;
    case "needs-password":
      return !user.isLocked && !user.hasPassword;
    case "2fa":
      return user.twoFactorEnabled;
  }
}

export function matchesStaffUserStatus(
  user: Pick<StaffUser, "isLocked" | "hasPassword" | "twoFactorEnabled">,
  selectedStatuses: readonly StaffUserStatus[] | null | undefined,
): boolean {
  if (!selectedStatuses || selectedStatuses.length === 0)
    return true;
  return selectedStatuses.some(s => matchesSingleStaffUserStatus(user, s));
}

