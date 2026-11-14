<script setup lang="ts">
import { PhFloppyDisk } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import {
  saBaiAPIEndpointsAuthUsersCreatePostEndpoint,
  saBaiAPIEndpointsAuthUsersUpdatePatchEndpoint,
} from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

/** Create a staff account, or edit one's profile and roles. */
const props = defineProps<{ user: StaffUser | null }>()
const emit = defineEmits<{ close: [] }>()

const MIN_PASSWORD = 8
const isEditing = computed(() => !!props.user)
const { user: me } = useCurrentUser()
const { data: roles } = useStaffRoles()

const form = ref({
  userName: props.user?.userName ?? '',
  nickname: props.user?.nickname ?? '',
  email: props.user?.email ?? '',
  password: '',
  roles: [...(props.user?.roles ?? ['member'])],
  tags: [...(props.user?.tags ?? [])],
})
const errors = ref<Partial<Record<'userName' | 'email' | 'password' | 'roles', string>>>({})
const formEl = ref<HTMLFormElement>()

function toggleRole(role: string, on: boolean) {
  form.value.roles = on ? [...new Set([...form.value.roles, role])] : form.value.roles.filter(x => x !== role)
}
const isSelf = computed(() => !!props.user && props.user.id === me.value?.id)

const queryClient = useQueryClient()
function done(message: string) {
  queryClient.invalidateQueries({ queryKey: queryKeys.staffUsers() })
  queryClient.invalidateQueries({ queryKey: queryKeys.tags() })
  if (isSelf.value)
    queryClient.invalidateQueries({ queryKey: queryKeys.currentUser() })
  toast.success(message)
  emit('close')
}

const save = useMutation({
  mutationFn: () => {
    const body = {
      userName: form.value.userName.trim(),
      nickname: form.value.nickname.trim() || undefined,
      email: form.value.email.trim(),
      roles: form.value.roles,
      tags: form.value.tags,
    }
    if (isEditing.value) {
      return saBaiAPIEndpointsAuthUsersUpdatePatchEndpoint({
        ...apiConfig,
        path: { id: props.user!.id },
        body: { ...body, nickname: body.nickname ?? body.userName } as never,
        throwOnError: true,
      }).unwrap()
    }
    return saBaiAPIEndpointsAuthUsersCreatePostEndpoint({
      ...apiConfig,
      body: { ...body, email: body.email || undefined, password: form.value.password } as never,
      throwOnError: true,
    }).unwrap()
  },
  onSuccess: () => done(isEditing.value ? 'Staff user updated.' : 'Staff user created.'),
  onError: err => toast.error(getApiErrorMessage(err, isEditing.value ? 'Unable to update staff user.' : 'Unable to create staff user.')),
})

function submit() {
  if (save.isPending.value)
    return
  errors.value = {}
  if (!form.value.userName.trim())
    errors.value.userName = 'Enter a username.'
  else if (!/^[\w.@+-]+$/.test(form.value.userName.trim()))
    errors.value.userName = 'Use letters, digits and . _ @ + - only.'
  if (form.value.email.trim() && !/^\S+@\S+\.\S+$/.test(form.value.email.trim()))
    errors.value.email = 'Enter a valid email address, or leave it empty.'
  if (!isEditing.value && form.value.password.length < MIN_PASSWORD)
    errors.value.password = `Use at least ${MIN_PASSWORD} characters.`
  if (form.value.roles.length === 0)
    errors.value.roles = 'Choose at least one role.'
  if (Object.keys(errors.value).length) {
    focusFirstInvalid(formEl.value)
    return
  }
  save.mutate()
}

useSaveShortcut(submit)
</script>

<template>
  <AppDialog :title="isEditing ? `Edit ${props.user!.nickname}` : 'New Staff User'" size="sm" @close="emit('close')">
    <form id="staff-user-form" ref="formEl" novalidate class="flex flex-col gap-3" @submit.prevent="submit">
      <FormField v-slot="{ id, describedby, invalid }" label="Username" required :error="errors.userName" hint="Used to sign in.">
        <Input
          :id="id"
          v-model="form.userName"
          autocomplete="off"
          autocapitalize="none"
          spellcheck="false"
          :aria-describedby="describedby"
          :aria-invalid="invalid || undefined"
        />
      </FormField>
      <FormField v-slot="{ id, describedby }" label="Display name" hint="Shown on consults and orders. Defaults to the username.">
        <Input :id="id" v-model="form.nickname" autocomplete="off" :aria-describedby="describedby" />
      </FormField>
      <FormField v-slot="{ id, describedby, invalid }" label="Email" :error="errors.email" hint="Optional. Can also be used to sign in.">
        <Input :id="id" v-model="form.email" type="email" autocomplete="off" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <FormField
        v-if="!isEditing"
        v-slot="{ id, describedby, invalid }"
        label="Password"
        required
        :error="errors.password"
        :hint="`At least ${MIN_PASSWORD} characters. Share it with the person; they can change it under My Account.`"
      >
        <Input :id="id" v-model="form.password" type="password" autocomplete="new-password" :aria-describedby="describedby" :aria-invalid="invalid || undefined" />
      </FormField>
      <fieldset class="flex flex-col gap-2" :aria-invalid="!!errors.roles || undefined">
        <legend class="mb-1 text-sm font-semibold">
          Roles <span class="text-destructive" aria-hidden="true">*</span>
        </legend>
        <div v-for="role in roles ?? []" :key="role" class="flex items-start gap-2">
          <Checkbox
            :id="`role-${role}`"
            :model-value="form.roles.includes(role)"
            :disabled="isSelf && role === ADMIN_ROLE"
            @update:model-value="(on) => toggleRole(role, on === true)"
          />
          <Label :for="`role-${role}`" class="flex flex-col items-start gap-0">
            <span class="font-semibold">{{ role }}</span>
            <span v-if="ROLE_DESCRIPTIONS[role]" class="text-xs font-normal text-muted-foreground touch:text-sm">{{ ROLE_DESCRIPTIONS[role] }}</span>
          </Label>
        </div>
        <p v-if="isSelf" class="text-xs text-muted-foreground touch:text-sm">
          You can't remove your own admin role.
        </p>
        <p v-if="errors.roles" class="text-xs font-medium text-destructive touch:text-sm" role="alert">
          {{ errors.roles }}
        </p>
      </fieldset>
      <FormField v-slot="{ id, describedby }" label="Tags" hint="Optional freeform labels (e.g. Khmer-speaking, Pediatric).">
        <TagInput :id="id" v-model="form.tags" :describedby="describedby" />
      </FormField>
    </form>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Cancel
      </Button>
      <Button type="submit" form="staff-user-form" variant="success" :disabled="save.isPending.value">
        <PhFloppyDisk aria-hidden="true" /> Save
      </Button>
    </template>
  </AppDialog>
</template>
