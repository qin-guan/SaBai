<script setup lang="ts">
import {
  PhClockCounterClockwise,
  PhKey,
  PhLock,
  PhLockOpen,
  PhPencilSimple,
  PhPlus,
  PhShieldCheck,
  PhShieldSlash,
} from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import {
  saBaiAPIEndpointsAuthUsersLockPostEndpoint,
  saBaiAPIEndpointsAuthUsersResetTwoFactorPostEndpoint,
  saBaiAPIEndpointsAuthUsersUnlockPostEndpoint,
} from '@sabai/sdk/clients'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'

definePageMeta({ requiresAdmin: true })
useHead({ title: "Staff Users · Sa'Bai" })

const { data, isLoading, isError } = useStaffUsersList()
const { user: me } = useCurrentUser()

const filter = ref('')
const users = computed(() => {
  const q = cleanKhmerText(filter.value)
  const all = data.value ?? []
  if (!q)
    return all
  return all.filter(u =>
    [u.nickname, u.userName, u.email ?? '', ...u.roles].some(x => matchesSearchText(x, q)),
  )
})

type Mode = 'edit' | 'password' | 'lock' | 'unlock' | 'reset-2fa'
const mode = ref<Mode | null>(null)
const active = ref<StaffUser | null>(null)
function open(next: Mode, user: StaffUser | null) {
  active.value = user
  mode.value = next
}
function close() {
  mode.value = null
  active.value = null
}

const queryClient = useQueryClient()
function action(run: (id: string) => Promise<unknown>, success: (u: StaffUser) => string, failure: string) {
  return useMutation({
    mutationFn: (user: StaffUser) => run(user.id),
    onSuccess: (_, user) => {
      queryClient.invalidateQueries({ queryKey: queryKeys.staffUsers() })
      toast.success(success(user))
      close()
    },
    onError: err => toast.error(getApiErrorMessage(err, failure)),
  })
}
const lock = action(
  id => saBaiAPIEndpointsAuthUsersLockPostEndpoint({ ...apiConfig, path: { id }, throwOnError: true }),
  u => `${u.nickname} is locked and has been signed out.`,
  'Unable to lock the account.',
)
const unlock = action(
  id => saBaiAPIEndpointsAuthUsersUnlockPostEndpoint({ ...apiConfig, path: { id }, throwOnError: true }),
  u => `${u.nickname} can sign in again.`,
  'Unable to unlock the account.',
)
const resetTwoFactor = action(
  id => saBaiAPIEndpointsAuthUsersResetTwoFactorPostEndpoint({ ...apiConfig, path: { id }, throwOnError: true }),
  u => `Two-factor is off for ${u.nickname}.`,
  'Unable to reset two-factor.',
)

function lockedLabel(user: StaffUser) {
  if (isLockedByAdmin(user))
    return 'Locked'
  const until = user.lockoutEnd ? new Date(user.lockoutEnd) : null
  return until ? `Locked until ${until.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' })}` : 'Locked'
}
</script>

<template>
  <div>
    <PageHeader
      title="Staff Users"
      description="Accounts that can sign in to this clinic deployment."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Staff Users' }]"
    >
      <template #actions>
        <Input v-model="filter" type="search" placeholder="Filter by name, username, role…" aria-label="Filter staff users" class="w-56 max-sm:w-full" />
        <Button @click="open('edit', null)">
          <PhPlus aria-hidden="true" /> New Staff User
        </Button>
      </template>
    </PageHeader>

    <AppPanel title="Staff" flush>
      <StateMessage v-if="isLoading" kind="loading" message="Loading staff users..." />
      <StateMessage v-else-if="isError" kind="error" message="Unable to load staff users." />
      <StateMessage v-else-if="users.length === 0" message="No staff users match." />
      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Username</TableHead>
            <TableHead>Email</TableHead>
            <TableHead>Roles</TableHead>
            <TableHead>Status</TableHead>
            <TableHead class="text-right">
              Actions
            </TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="user in users" :key="user.id">
            <TableCell data-label="Name" class="font-semibold">
              {{ user.nickname }}
              <span v-if="user.id === me?.id" class="font-normal text-muted-foreground">(you)</span>
            </TableCell>
            <TableCell data-label="Username" class="font-mono">
              {{ user.userName }}
            </TableCell>
            <TableCell data-label="Email">
              {{ user.email ?? '—' }}
            </TableCell>
            <TableCell data-label="Roles">
              <span class="flex flex-wrap gap-1">
                <Badge
                  v-for="role in user.roles"
                  :key="role"
                  variant="outline"
                  :class="role === ADMIN_ROLE && 'border-chrome/40 bg-band text-chrome'"
                >
                  {{ role }}
                </Badge>
              </span>
            </TableCell>
            <TableCell data-label="Status">
              <span class="flex flex-wrap gap-1">
                <Badge v-if="user.isLocked" variant="outline" class="border-destructive/40 bg-destructive-subtle text-destructive">
                  <PhLock aria-hidden="true" /> {{ lockedLabel(user) }}
                </Badge>
                <Badge v-else-if="!user.hasPassword" variant="outline" class="border-warning-border bg-warning-subtle text-warning">
                  Needs password
                </Badge>
                <Badge v-else variant="outline" class="border-success/40 bg-success-subtle text-success">
                  Active
                </Badge>
                <Badge v-if="user.twoFactorEnabled" variant="outline" title="Two-factor authentication is on">
                  <PhShieldCheck aria-hidden="true" /> 2FA
                </Badge>
              </span>
            </TableCell>
            <TableCell data-stack="actions">
              <div class="flex flex-wrap justify-end gap-1">
                <Button size="sm" variant="outline" as-child>
                  <NuxtLink :to="paths.settingsAuditLog({ userId: user.id })" :aria-label="`Activity for ${user.nickname}`">
                    <PhClockCounterClockwise aria-hidden="true" /> Activity
                  </NuxtLink>
                </Button>
                <Button size="sm" variant="outline" :aria-label="`Edit ${user.nickname}`" @click="open('edit', user)">
                  <PhPencilSimple aria-hidden="true" /> Edit
                </Button>
                <Button size="sm" variant="outline" :aria-label="`Set password for ${user.nickname}`" @click="open('password', user)">
                  <PhKey aria-hidden="true" /> Password
                </Button>
                <Button
                  v-if="user.twoFactorEnabled"
                  size="sm"
                  variant="outline"
                  :aria-label="`Reset two-factor for ${user.nickname}`"
                  @click="open('reset-2fa', user)"
                >
                  <PhShieldSlash aria-hidden="true" /> Reset 2FA
                </Button>
                <Button
                  v-if="user.isLocked"
                  size="sm"
                  variant="outline"
                  :aria-label="`Unlock ${user.nickname}`"
                  @click="open('unlock', user)"
                >
                  <PhLockOpen aria-hidden="true" /> Unlock
                </Button>
                <Button
                  v-else-if="user.id !== me?.id"
                  size="sm"
                  variant="destructive"
                  :aria-label="`Lock ${user.nickname}`"
                  @click="open('lock', user)"
                >
                  <PhLock aria-hidden="true" /> Lock
                </Button>
              </div>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <StaffUserDialog v-if="mode === 'edit'" :user="active" @close="close" />
    <SetPasswordDialog v-if="mode === 'password' && active" :user="active" @close="close" />

    <AppDialog v-if="mode === 'lock' && active" :title="`Lock ${active.nickname}?`" size="sm" @close="close">
      <p>
        <strong>{{ active.userName }}</strong> won't be able to sign in, and any open sessions end within a minute.
        Their records stay as they are. You can unlock the account at any time.
      </p>
      <template #footer>
        <Button variant="outline" @click="close">
          Cancel
        </Button>
        <Button variant="destructive" :disabled="lock.isPending.value" @click="lock.mutate(active)">
          <PhLock aria-hidden="true" /> Lock account
        </Button>
      </template>
    </AppDialog>

    <AppDialog v-if="mode === 'unlock' && active" :title="`Unlock ${active.nickname}?`" size="sm" @close="close">
      <p v-if="isLockedByAdmin(active)">
        <strong>{{ active.userName }}</strong> will be able to sign in again.
      </p>
      <p v-else>
        <strong>{{ active.userName }}</strong> was locked out after too many wrong passwords. Unlocking lets them try again now.
      </p>
      <template #footer>
        <Button variant="outline" @click="close">
          Cancel
        </Button>
        <Button variant="success" :disabled="unlock.isPending.value" @click="unlock.mutate(active)">
          <PhLockOpen aria-hidden="true" /> Unlock
        </Button>
      </template>
    </AppDialog>

    <AppDialog v-if="mode === 'reset-2fa' && active" :title="`Reset two-factor for ${active.nickname}?`" size="sm" @close="close">
      <p>
        For a lost or replaced phone. <strong>{{ active.userName }}</strong> will sign in with just their password and
        can set up a new authenticator under My Account.
      </p>
      <template #footer>
        <Button variant="outline" @click="close">
          Cancel
        </Button>
        <Button variant="destructive" :disabled="resetTwoFactor.isPending.value" @click="resetTwoFactor.mutate(active)">
          <PhShieldSlash aria-hidden="true" /> Reset two-factor
        </Button>
      </template>
    </AppDialog>
  </div>
</template>
