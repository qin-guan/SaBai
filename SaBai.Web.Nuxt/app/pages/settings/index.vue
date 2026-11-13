<script setup lang="ts">
import { PhBell, PhCaretRight, PhClockCounterClockwise, PhMapPin, PhUserCircle, PhUserGear } from '@phosphor-icons/vue'

useHead({ title: "Settings · Sa'Bai" })

const { isAdmin } = useCurrentUser()
const settingsItems = computed(() => [
  { name: 'Village Codes', href: paths.settingsVillageCodes(), icon: PhMapPin, description: 'Manage village codes.' },
  { name: 'Notifications', href: paths.settingsNotifications(), icon: PhBell, description: 'Send clinic-wide broadcasts and push notifications to staff devices.' },
  ...(isAdmin.value
    ? [
        { name: 'Staff Users', href: paths.settingsUsers(), icon: PhUserGear, description: 'Add staff, assign roles, reset passwords and two-factor, lock accounts.' },
        { name: 'Audit Log', href: paths.settingsAuditLog(), icon: PhClockCounterClockwise, description: 'Who changed which record, and when staff signed in.' },
      ]
    : []),
  { name: 'My Account', href: paths.account(), icon: PhUserCircle, description: 'Change your password and set up two-factor authentication.' },
])
</script>

<template>
  <div>
    <PageHeader
      title="Settings"
      description="Manage configuration for this clinic deployment."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Settings' }]"
    />
    <AppPanel title="Configuration" flush>
      <ul class="divide-y divide-border">
        <li v-for="item in settingsItems" :key="item.name">
          <NuxtLink :to="item.href" class="flex items-center gap-3 px-3 py-3 hover:bg-accent focus-visible:bg-accent">
            <component :is="item.icon" class="size-5 text-chrome" aria-hidden="true" />
            <div class="flex-1">
              <p class="font-bold">
                {{ item.name }}
              </p>
              <p class="text-xs text-muted-foreground touch:text-sm">
                {{ item.description }}
              </p>
            </div>
            <PhCaretRight class="size-4 text-muted-foreground" aria-hidden="true" />
          </NuxtLink>
        </li>
      </ul>
    </AppPanel>
  </div>
</template>
