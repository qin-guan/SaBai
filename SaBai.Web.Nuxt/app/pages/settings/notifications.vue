<script setup lang="ts">
import {
  PhArrowsClockwise,
  PhBell,
  PhBellRinging,
  PhBellSlash,
  PhCheckCircle,
  PhPaperPlaneTilt,
  PhTrash,
  PhWarning,
  PhWarningCircle,
} from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import type { SaBaiAPIDomainBroadcastSeverityKey } from '@sabai/sdk/types'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table'
import { Textarea } from '@/components/ui/textarea'

useHead({ title: "Notifications · Sa'Bai" })

const { config, status } = usePushNotifications()
const { data: broadcasts, isLoading, isError, refetch } = useBroadcastsList()
const sendMutation = useSendBroadcast()
const deleteMutation = useDeleteBroadcast()

// Compose form state
const title = ref('')
const body = ref('')
const severity = ref<SaBaiAPIDomainBroadcastSeverityKey>('info')
const submitted = ref(false)

const severityOptions = [
  { label: 'Info', value: 'info' },
  { label: 'Warning', value: 'warning' },
  { label: 'Critical', value: 'critical' },
] as const

const titleError = computed(() => {
  if (!submitted.value)
    return null
  const trimmed = title.value.trim()
  if (!trimmed)
    return 'Notification title is required.'
  if (trimmed.length > 255)
    return 'Title must be 255 characters or fewer.'
  return null
})

const bodyError = computed(() => {
  if (!submitted.value)
    return null
  const trimmed = body.value.trim()
  if (!trimmed)
    return 'Notification message is required.'
  if (trimmed.length > 5000)
    return 'Message must be 5000 characters or fewer.'
  return null
})

async function handleSend() {
  submitted.value = true
  if (titleError.value || bodyError.value)
    return

  try {
    await sendMutation.mutateAsync({
      title: title.value,
      body: body.value,
      severity: severity.value,
    })
    toast.success('Push notification sent to all staff devices.')
    title.value = ''
    body.value = ''
    severity.value = 'info'
    submitted.value = false
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Unable to send push notification.'))
  }
}

useSaveShortcut(handleSend, { enabled: () => !sendMutation.isPending.value })

// Delete confirmation dialog
const activeToDelete = ref<BroadcastItem | null>(null)

function confirmDelete(item: BroadcastItem) {
  activeToDelete.value = item
}

function cancelDelete() {
  activeToDelete.value = null
}

async function handleDelete() {
  if (!activeToDelete.value)
    return
  try {
    await deleteMutation.mutateAsync(activeToDelete.value.id)
    toast.success('Notification removed from history.')
    activeToDelete.value = null
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Unable to delete notification.'))
  }
}

function formatDateTime(iso: string) {
  return new Date(iso).toLocaleString(undefined, {
    dateStyle: 'medium',
    timeStyle: 'short',
  })
}
</script>

<template>
  <div class="flex flex-col gap-4">
    <PageHeader
      title="Notifications"
      description="Compose and send clinic-wide broadcasts and push notifications to all staff devices."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings', to: paths.settings() }, { label: 'Notifications' }]"
    />

    <!-- Push system status banner -->
    <div
      v-if="config.data.value"
      class="flex flex-wrap items-center justify-between gap-3 border border-border bg-card p-3 text-sm"
    >
      <div class="flex items-center gap-2">
        <Badge
          v-if="config.data.value.isEnabled"
          variant="outline"
          class="border-success/40 bg-success-subtle text-success"
        >
          <PhCheckCircle aria-hidden="true" /> Push service active
        </Badge>
        <Badge v-else variant="outline" class="border-destructive/40 bg-destructive-subtle text-destructive">
          <PhBellSlash aria-hidden="true" /> Push keys unconfigured
        </Badge>
        <span class="text-xs text-muted-foreground touch:text-sm">
          {{ config.data.value.deviceCount }} staff device{{ config.data.value.deviceCount === 1 ? '' : 's' }} registered
        </span>
      </div>

      <div class="flex items-center gap-2">
        <span v-if="status === 'on'" class="inline-flex items-center gap-1 text-xs text-success">
          <PhBellRinging class="size-3.5" aria-hidden="true" /> This device receives notifications
        </span>
        <span v-else class="inline-flex items-center gap-1 text-xs text-muted-foreground">
          <PhBellSlash class="size-3.5" aria-hidden="true" /> This device is not subscribed
        </span>
        <Button variant="ghost" size="sm" as-child>
          <NuxtLink :to="paths.account()">
            Device settings
          </NuxtLink>
        </Button>
      </div>
    </div>

    <!-- Compose notification panel -->
    <AppPanel title="Send notification">
      <form class="flex flex-col gap-4" @submit.prevent="handleSend">
        <FormField label="Title" required :error="titleError" hint="A short summary displayed prominently in the push notification (max 255 chars).">
          <template #default="{ id, describedby, invalid }">
            <Input
              :id="id"
              v-model="title"
              :aria-describedby="describedby"
              :aria-invalid="invalid"
              maxlength="255"
              placeholder="e.g. Clinic closing early, Patient transfer alert"
            />
          </template>
        </FormField>

        <FormField label="Severity" hint="Sets the visual urgency pill on staff screens.">
          <div class="flex flex-wrap items-center gap-3">
            <ChoiceGroup v-model="severity" :options="severityOptions" />
            <Badge
              v-if="severity === 'info'"
              variant="outline"
              class="border-band-border bg-accent text-accent-foreground"
            >
              <PhBell aria-hidden="true" /> Info notice
            </Badge>
            <Badge
              v-else-if="severity === 'warning'"
              variant="outline"
              class="border-warning/40 bg-warning-subtle text-warning"
            >
              <PhWarning aria-hidden="true" /> Warning alert
            </Badge>
            <Badge
              v-else-if="severity === 'critical'"
              variant="outline"
              class="border-destructive/40 bg-destructive-subtle text-destructive"
            >
              <PhWarningCircle aria-hidden="true" /> Critical urgent
            </Badge>
          </div>
        </FormField>

        <FormField
          label="Message"
          required
          :error="bodyError"
          :hint="`${body.length}/5000 characters. Detailed body of the notification shown when tapped or expanded.`"
        >
          <template #default="{ id, describedby, invalid }">
            <Textarea
              :id="id"
              v-model="body"
              :aria-describedby="describedby"
              :aria-invalid="invalid"
              maxlength="5000"
              rows="3"
              placeholder="Enter message for staff devices..."
            />
          </template>
        </FormField>

        <div class="flex flex-wrap items-center justify-between gap-2 pt-2">
          <p class="text-xs text-muted-foreground touch:text-sm">
            Notifications are sent to all staff devices and saved to clinic history.
            <span class="hidden kb:inline"><ShortcutHint keys="Ctrl+S" /> to send</span>
          </p>
          <div class="flex items-center gap-2">
            <Button
              v-if="title || body"
              type="button"
              variant="ghost"
              size="sm"
              @click="title = ''; body = ''; submitted = false"
            >
              Clear
            </Button>
            <Button
              type="submit"
              :disabled="sendMutation.isPending.value || !title.trim() || !body.trim()"
            >
              <PhPaperPlaneTilt aria-hidden="true" />
              {{ sendMutation.isPending.value ? 'Sending...' : 'Send push notification' }}
            </Button>
          </div>
        </div>
      </form>
    </AppPanel>

    <!-- Notification history table -->
    <AppPanel title="Notification history" flush>
      <template #actions>
        <Button variant="ghost" size="sm" @click="() => refetch()">
          <PhArrowsClockwise aria-hidden="true" /> Refresh
        </Button>
      </template>

      <StateMessage v-if="isLoading" kind="loading" message="Loading notifications history..." />
      <StateMessage v-else-if="isError" kind="error" message="Unable to load notifications history." />
      <StateMessage v-else-if="!broadcasts || broadcasts.length === 0" message="No notifications have been sent yet." />

      <Table v-else class="grid-table stack-table">
        <TableHeader>
          <TableRow>
            <TableHead class="w-24">Severity</TableHead>
            <TableHead>Notification</TableHead>
            <TableHead class="w-48">Sent by</TableHead>
            <TableHead class="w-40">Sent at</TableHead>
            <TableHead class="w-24">Status</TableHead>
            <TableHead class="w-16 text-right">Actions</TableHead>
          </TableRow>
        </TableHeader>
        <TableBody>
          <TableRow v-for="item in broadcasts" :key="item.id">
            <TableCell data-label="Severity">
              <Badge
                v-if="item.severity === 'critical'"
                variant="outline"
                class="border-destructive/40 bg-destructive-subtle text-destructive capitalize"
              >
                {{ item.severity }}
              </Badge>
              <Badge
                v-else-if="item.severity === 'warning'"
                variant="outline"
                class="border-warning/40 bg-warning-subtle text-warning capitalize"
              >
                {{ item.severity }}
              </Badge>
              <Badge
                v-else
                variant="outline"
                class="border-band-border bg-accent text-accent-foreground capitalize"
              >
                {{ item.severity }}
              </Badge>
            </TableCell>

            <TableCell data-label="Notification">
              <div class="flex flex-col gap-0.5">
                <span class="font-bold text-foreground">{{ item.title }}</span>
                <span class="line-clamp-2 text-xs text-muted-foreground whitespace-pre-line">{{ item.body }}</span>
              </div>
            </TableCell>

            <TableCell data-label="Sent by" class="text-xs text-muted-foreground font-mono">
              {{ item.createdBy ?? '—' }}
            </TableCell>

            <TableCell data-label="Sent at" class="text-xs text-muted-foreground">
              {{ formatDateTime(item.createdAt) }}
            </TableCell>

            <TableCell data-label="Status">
              <Badge
                v-if="item.publishedAt"
                variant="outline"
                class="border-success/40 bg-success-subtle text-success"
              >
                Published
              </Badge>
              <Badge v-else variant="outline">
                Draft
              </Badge>
            </TableCell>

            <TableCell data-label="Actions" class="text-right">
              <Button
                variant="ghost"
                size="sm"
                class="size-8 p-0 text-muted-foreground hover:text-destructive touch:size-11"
                aria-label="Delete notification"
                @click="confirmDelete(item)"
              >
                <PhTrash class="size-4" aria-hidden="true" />
              </Button>
            </TableCell>
          </TableRow>
        </TableBody>
      </Table>
    </AppPanel>

    <!-- Delete confirmation dialog -->
    <AppDialog
      v-if="activeToDelete"
      title="Delete Notification"
      size="sm"
      @close="cancelDelete"
    >
      <p class="text-sm">
        Are you sure you want to delete notification
        <strong>"{{ activeToDelete.title }}"</strong> from clinic history?
      </p>
      <template #footer>
        <Button variant="outline" @click="cancelDelete">
          Cancel
        </Button>
        <Button
          variant="destructive"
          :disabled="deleteMutation.isPending.value"
          @click="handleDelete"
        >
          <PhTrash aria-hidden="true" />
          {{ deleteMutation.isPending.value ? 'Deleting...' : 'Delete' }}
        </Button>
      </template>
    </AppDialog>
  </div>
</template>
