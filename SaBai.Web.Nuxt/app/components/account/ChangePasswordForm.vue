<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsAuthAccountPasswordPostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

const MIN_LENGTH = 8

const form = ref({ currentPassword: '', newPassword: '', confirm: '' })
const errors = ref<Partial<Record<keyof typeof form.value, string>>>({})

const change = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsAuthAccountPasswordPostEndpoint({
      ...apiConfig,
      body: { currentPassword: form.value.currentPassword, newPassword: form.value.newPassword },
      throwOnError: true,
    }),
  onSuccess: () => {
    form.value = { currentPassword: '', newPassword: '', confirm: '' }
    toast.success('Password changed. Other devices have been signed out.')
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to change password.')),
})

function submit() {
  errors.value = {}
  if (!form.value.currentPassword)
    errors.value.currentPassword = 'Enter your current password.'
  if (form.value.newPassword.length < MIN_LENGTH)
    errors.value.newPassword = `Use at least ${MIN_LENGTH} characters.`
  else if (form.value.newPassword !== form.value.confirm)
    errors.value.confirm = 'Passwords do not match.'
  if (Object.keys(errors.value).length === 0)
    change.mutate()
}
</script>

<template>
  <form novalidate class="flex max-w-sm flex-col gap-3" @submit.prevent="submit">
    <FormField v-slot="{ id, describedby, invalid }" label="Current password" required :error="errors.currentPassword">
      <Input :id="id" v-model="form.currentPassword" type="password" autocomplete="current-password" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="New password" required :error="errors.newPassword" :hint="`At least ${MIN_LENGTH} characters.`">
      <Input :id="id" v-model="form.newPassword" type="password" autocomplete="new-password" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
    </FormField>
    <FormField v-slot="{ id, describedby, invalid }" label="Confirm new password" required :error="errors.confirm">
      <Input :id="id" v-model="form.confirm" type="password" autocomplete="new-password" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
    </FormField>
    <div>
      <Button type="submit" :disabled="change.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> {{ change.isPending.value ? 'Saving...' : 'Change password' }}
      </Button>
    </div>
  </form>
</template>
