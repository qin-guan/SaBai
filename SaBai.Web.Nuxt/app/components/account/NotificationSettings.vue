<script setup lang="ts">
import { PhBell, PhBellRinging, PhBellSlash } from '@phosphor-icons/vue'
import { useMutation } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'

/**
 * Notifications on this device for the signed-in user: asks the browser for
 * permission (from a button, as browsers require) and subscribes the device,
 * or explains why notifications cannot work here.
 */
const { config, status, isChecked, turnOn, turnOff, sendTest } = usePushNotifications()

const enable = useMutation({
  mutationFn: turnOn,
  onSuccess: () => {
    if (status.value === 'on')
      toast.success('Notifications are on for this device.')
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to turn on notifications.')),
})

const disable = useMutation({
  mutationFn: turnOff,
  onSuccess: () => toast.success('Notifications are off for this device.'),
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to turn off notifications.')),
})

const test = useMutation({
  mutationFn: sendTest,
  onSuccess: (result) => {
    if (result.sent > 0)
      toast.success('Test notification sent. It should appear in a few seconds.')
    else
      toast.error('The test notification could not be sent. Turn notifications off and on again.')
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to send a test notification.')),
})

const otherDevices = computed(() => {
  const count = (config.data.value?.deviceCount ?? 0) - (status.value === 'on' ? 1 : 0)
  return count > 0 ? `Also on for ${count} other device${count === 1 ? '' : 's'} of yours.` : ''
})
</script>

<template>
  <div class="flex flex-col gap-3">
    <StateMessage v-if="config.isLoading.value || !isChecked" kind="loading" message="Checking notifications..." />
    <StateMessage v-else-if="config.isError.value" kind="error" message="Unable to load notification settings." />

    <template v-else>
      <div class="flex flex-wrap items-center gap-2">
        <Badge v-if="status === 'on'" variant="outline" class="border-success/40 bg-success-subtle text-success">
          <PhBellRinging aria-hidden="true" /> On for this device
        </Badge>
        <Badge v-else variant="outline">
          <PhBellSlash aria-hidden="true" /> Off for this device
        </Badge>
        <span v-if="otherDevices" class="text-xs text-muted-foreground touch:text-sm">{{ otherDevices }}</span>
      </div>

      <StateMessage
        v-if="status === 'needs-install'"
        kind="warning"
        message="On iPhone and iPad, notifications only work from the Home Screen. In Safari, tap Share, then Add to Home Screen, open Sa'Bai from there and come back to this page."
      />
      <StateMessage
        v-else-if="status === 'unsupported'"
        message="This browser cannot receive notifications. Use a current version of Chrome, Edge, Firefox or Safari."
      />
      <StateMessage
        v-else-if="status === 'server-off'"
        message="Notifications are not set up on this server yet. Ask an admin."
      />
      <StateMessage
        v-else-if="status === 'blocked'"
        kind="warning"
        message="Notifications are blocked for Sa'Bai on this device. Allow them in the browser's site settings (or the device's notification settings for the installed app), then reload this page."
      />

      <template v-else-if="status === 'off'">
        <p class="max-w-prose text-xs text-muted-foreground touch:text-sm">
          Get Sa'Bai notifications on this device, even when the app is closed. Your browser will ask for
          permission. Notifications are per device, so turn them on wherever you want them.
        </p>
        <div>
          <Button :disabled="enable.isPending.value" @click="enable.mutate()">
            <PhBell aria-hidden="true" /> {{ enable.isPending.value ? 'Turning on...' : 'Turn on notifications' }}
          </Button>
        </div>
      </template>

      <div v-else class="flex flex-wrap gap-2">
        <Button variant="outline" :disabled="test.isPending.value" @click="test.mutate()">
          <PhBellRinging aria-hidden="true" /> {{ test.isPending.value ? 'Sending...' : 'Send test notification' }}
        </Button>
        <Button variant="outline" :disabled="disable.isPending.value" @click="disable.mutate()">
          <PhBellSlash aria-hidden="true" /> Turn off
        </Button>
      </div>
    </template>
  </div>
</template>
