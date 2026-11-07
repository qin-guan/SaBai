<script setup lang="ts">
import { PhArrowsClockwise, PhCheckCircle, PhQrCode, PhShieldCheck, PhShieldSlash } from '@phosphor-icons/vue'
import { useMutation, useQuery, useQueryClient } from '@tanstack/vue-query'
import { toast } from 'vue-sonner'
import { renderSVG } from 'uqr'
import {
  saBaiAPIEndpointsAuthAccountAuthenticatorPostEndpoint,
  saBaiAPIEndpointsAuthAccountRecoveryCodesPostEndpoint,
  saBaiAPIEndpointsAuthAccountTwoFactorDisablePostEndpoint,
  saBaiAPIEndpointsAuthAccountTwoFactorEnablePostEndpoint,
  saBaiAPIEndpointsAuthAccountTwoFactorGetEndpoint,
} from '@sabai/sdk/clients'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'

/**
 * Authenticator-app (TOTP) two-factor for the signed-in user: set up with a QR
 * code, confirm with a code, keep recovery codes, or turn it off again.
 */
const queryClient = useQueryClient()
const status = useQuery({
  queryKey: queryKeys.twoFactor(),
  queryFn: ({ signal }) => saBaiAPIEndpointsAuthAccountTwoFactorGetEndpoint({ ...apiConfig, signal }).unwrap(),
})

const setup = ref<{ sharedKey: string, authenticatorUri: string } | null>(null)
const qrSvg = computed(() => (setup.value ? renderSVG(setup.value.authenticatorUri, { border: 1 }) : ''))
const code = ref('')
const codeError = ref<string | null>(null)
const recoveryCodes = ref<string[] | null>(null)
const showDisable = ref(false)
const disablePassword = ref('')

function refresh() {
  queryClient.invalidateQueries({ queryKey: queryKeys.twoFactor() })
  queryClient.invalidateQueries({ queryKey: queryKeys.currentUser() })
}

const start = useMutation({
  mutationFn: () => saBaiAPIEndpointsAuthAccountAuthenticatorPostEndpoint({ ...apiConfig, throwOnError: true }).unwrap(),
  onSuccess: (result) => {
    setup.value = result
    code.value = ''
    codeError.value = null
    recoveryCodes.value = null
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to start authenticator setup.')),
})

const enable = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsAuthAccountTwoFactorEnablePostEndpoint({ ...apiConfig, body: { code: code.value }, throwOnError: true }).unwrap(),
  onSuccess: (result) => {
    setup.value = null
    recoveryCodes.value = result.recoveryCodes
    refresh()
    toast.success('Two-factor authentication is on.')
  },
  onError: (err) => {
    codeError.value = getApiErrorMessage(err, 'The verification code is not valid.')
  },
})

const regenerate = useMutation({
  mutationFn: () => saBaiAPIEndpointsAuthAccountRecoveryCodesPostEndpoint({ ...apiConfig, throwOnError: true }).unwrap(),
  onSuccess: (result) => {
    recoveryCodes.value = result.recoveryCodes
    refresh()
    toast.success('New recovery codes created. The old ones no longer work.')
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to create recovery codes.')),
})

const disable = useMutation({
  mutationFn: () =>
    saBaiAPIEndpointsAuthAccountTwoFactorDisablePostEndpoint({ ...apiConfig, body: { password: disablePassword.value }, throwOnError: true }),
  onSuccess: () => {
    showDisable.value = false
    disablePassword.value = ''
    recoveryCodes.value = null
    refresh()
    toast.success('Two-factor authentication is off.')
  },
  onError: err => toast.error(getApiErrorMessage(err, 'Unable to turn off two-factor authentication.')),
})

function confirmCode() {
  codeError.value = null
  if (!/^\d{6}$/.test(code.value.replace(/\s/g, ''))) {
    codeError.value = 'Enter the 6-digit code from your app.'
    return
  }
  enable.mutate()
}
</script>

<template>
  <div class="flex flex-col gap-3">
    <StateMessage v-if="status.isLoading.value" kind="loading" message="Loading two-factor status..." />
    <StateMessage v-else-if="status.isError.value" kind="error" message="Unable to load two-factor status." />

    <template v-else-if="status.data.value">
      <div class="flex flex-wrap items-center gap-2">
        <Badge
          v-if="status.data.value.isEnabled"
          variant="outline"
          class="border-success/40 bg-success-subtle text-success"
        >
          <PhShieldCheck aria-hidden="true" /> On
        </Badge>
        <Badge v-else variant="outline">
          <PhShieldSlash aria-hidden="true" /> Off
        </Badge>
        <span v-if="status.data.value.isEnabled" class="text-xs text-muted-foreground touch:text-sm">
          {{ status.data.value.recoveryCodesLeft }} recovery code{{ status.data.value.recoveryCodesLeft === 1 ? '' : 's' }} left
          <template v-if="status.data.value.isMachineRemembered"> · this device skips the code</template>
        </span>
      </div>

      <StateMessage
        v-if="status.data.value.isEnabled && status.data.value.recoveryCodesLeft <= 2 && !recoveryCodes"
        kind="warning"
        message="You are running out of recovery codes. Create new ones."
      />

      <RecoveryCodesList v-if="recoveryCodes" :codes="recoveryCodes" />

      <!-- Off: offer setup -->
      <template v-if="!status.data.value.isEnabled">
        <p v-if="!setup" class="max-w-prose text-xs text-muted-foreground touch:text-sm">
          Two-factor adds a 6-digit code from an authenticator app (Google Authenticator, Microsoft
          Authenticator, 1Password…) to your sign-in, so a password alone is not enough.
        </p>
        <div v-if="!setup">
          <Button :disabled="start.isPending.value" @click="start.mutate()">
            <PhQrCode aria-hidden="true" /> Set up authenticator app
          </Button>
        </div>

        <div v-else class="flex flex-col gap-3">
          <ol class="flex list-decimal flex-col gap-3 pl-5 text-sm">
            <li>
              Scan this QR code with your authenticator app.
              <div class="mt-2 size-48 border border-border bg-white p-1 [&>svg]:size-full" role="img" aria-label="QR code for the authenticator app" v-html="qrSvg" />
              <p class="mt-2 text-xs text-muted-foreground touch:text-sm">
                Can't scan it? Enter this key instead:
              </p>
              <code class="mt-1 inline-block break-all border border-border bg-stripe px-2 py-1 font-mono text-sm">{{ setup.sharedKey }}</code>
            </li>
            <li>
              Enter the 6-digit code the app shows.
              <form novalidate class="mt-2 flex max-w-sm flex-col gap-2" @submit.prevent="confirmCode">
                <FormField v-slot="{ id, describedby, invalid }" label="Verification code" required :error="codeError">
                  <Input
                    :id="id"
                    v-model="code"
                    inputmode="numeric"
                    autocomplete="one-time-code"
                    maxlength="7"
                    class="font-mono tracking-widest"
                    :aria-describedby="describedby"
                    :aria-invalid="invalid || undefined"
                  />
                </FormField>
                <div class="flex flex-wrap gap-2">
                  <Button type="submit" variant="success" :disabled="enable.isPending.value">
                    <PhCheckCircle aria-hidden="true" /> {{ enable.isPending.value ? 'Checking...' : 'Turn on two-factor' }}
                  </Button>
                  <Button type="button" variant="outline" @click="setup = null">
                    Cancel
                  </Button>
                </div>
              </form>
            </li>
          </ol>
        </div>
      </template>

      <!-- On: manage -->
      <div v-else class="flex flex-wrap gap-2">
        <Button variant="outline" :disabled="regenerate.isPending.value" @click="regenerate.mutate()">
          <PhArrowsClockwise aria-hidden="true" /> New recovery codes
        </Button>
        <Button variant="destructive" @click="showDisable = true">
          <PhShieldSlash aria-hidden="true" /> Turn off two-factor
        </Button>
      </div>
    </template>

    <AppDialog v-if="showDisable" title="Turn off two-factor" size="sm" @close="showDisable = false; disablePassword = ''">
      <form id="disable-2fa-form" novalidate class="flex flex-col gap-3" @submit.prevent="disablePassword && disable.mutate()">
        <p>Sign-in will only need your password. Enter it to confirm.</p>
        <FormField v-slot="{ id }" label="Password" required>
          <Input :id="id" v-model="disablePassword" type="password" autocomplete="current-password" />
        </FormField>
      </form>
      <template #footer>
        <Button variant="outline" @click="showDisable = false; disablePassword = ''">
          Cancel
        </Button>
        <Button type="submit" form="disable-2fa-form" variant="destructive" :disabled="!disablePassword || disable.isPending.value">
          <PhShieldSlash aria-hidden="true" /> Turn off
        </Button>
      </template>
    </AppDialog>
  </div>
</template>
