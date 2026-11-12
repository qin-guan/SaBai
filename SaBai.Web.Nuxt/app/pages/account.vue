<script setup lang="ts">
import { Badge } from '@/components/ui/badge'

useHead({ title: "My Account · Sa'Bai" })

const { user } = useCurrentUser()
</script>

<template>
  <div class="flex flex-col gap-3">
    <PageHeader
      title="My Account"
      description="Your sign-in details. Ask an admin to change your name, username or roles."
      :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'My Account' }]"
    />

    <AppPanel title="Profile">
      <dl v-if="user" class="grid grid-cols-[max-content_1fr] gap-x-4 gap-y-1 text-sm">
        <dt class="font-semibold">
          Name
        </dt>
        <dd>{{ user.nickname }}</dd>
        <dt class="font-semibold">
          Username
        </dt>
        <dd class="font-mono">
          {{ user.userName }}
        </dd>
        <dt class="font-semibold">
          Email
        </dt>
        <dd>{{ user.email ?? '—' }}</dd>
        <dt class="font-semibold">
          Roles
        </dt>
        <dd class="flex flex-wrap gap-1">
          <Badge v-for="role in user.roles" :key="role" variant="outline">
            {{ role }}
          </Badge>
        </dd>
      </dl>
    </AppPanel>

    <AppPanel title="Password">
      <ChangePasswordForm />
    </AppPanel>

    <AppPanel title="Two-factor authentication">
      <TwoFactorSettings />
    </AppPanel>

    <AppPanel title="Notifications">
      <NotificationSettings />
    </AppPanel>
  </div>
</template>
