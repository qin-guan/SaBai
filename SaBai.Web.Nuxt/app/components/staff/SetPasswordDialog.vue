<script setup lang="ts">
import { PhKey } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { saBaiAPIEndpointsAuthUsersResetPasswordPostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/** Admin sets a new password for someone who forgot theirs (or never had one). */
const props = defineProps<{ user: StaffUser }>()
const emit = defineEmits<{ close: [] }>()

const MIN_PASSWORD = 8
const password = ref('')
const error = ref<string | null>(null)

const queryClient = useQueryClient()
const save = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsAuthUsersResetPasswordPostEndpoint({
      ...apiConfig,
      path: { id: props.user.id },
      body: { password: password.value },
      throwOnError: true,
    }).unwrap(),
  onSuccess: () => {
    queryClient.invalidateQueries({ queryKey: queryKeys.staffUsers() })
    toast.success(`New password set for ${props.user.nickname}.`)
    emit('close')
  },
  onError: (err) => {
    error.value = getApiErrorMessage(err, 'Unable to set the password.')
  },
})

function submit() {
  error.value = password.value.length < MIN_PASSWORD ? `Use at least ${MIN_PASSWORD} characters.` : null
  if (!error.value)
    save.mutate()
}
</script>

<template>
  <AppDialog :title="`Set password for ${props.user.nickname}`" size="sm" @close="emit('close')">
    <form id="set-password-form" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <p>
        <template v-if="props.user.hasPassword">
          Replaces the current password and signs <strong>{{ props.user.userName }}</strong> out everywhere.
        </template>
        <template v-else>
          <strong>{{ props.user.userName }}</strong> has no password yet and cannot sign in until you set one.
        </template>
      </p>
      <FormField v-slot="{ id, describedby, invalid }" label="New password" required :error="error" :hint="`At least ${MIN_PASSWORD} characters.`">
        <Input :id="id" v-model="password" type="password" autocomplete="new-password" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="set-password-form" variant="success" :disabled="save.isPending.value">
        <PhKey aria-hidden="true" /> Set password
      </Button>
    </template>
  </AppDialog>
</template>
