<script setup lang="ts">
import { PhArrowLeft, PhSignIn, PhWarningOctagon } from '@phosphor-icons/vue'
import { useMutation, useQueryClient } from '@tanstack/vue-query'
import {
  saBaiAPIEndpointsAuthLoginPostEndpoint,
  saBaiAPIEndpointsAuthLoginRecoveryCodePostEndpoint,
  saBaiAPIEndpointsAuthLoginTwoFactorPostEndpoint,
} from '@sabai/sdk/clients'
import type { SaBaiAPIEndpointsAuthSignInResponse } from '@sabai/sdk/types'
import { Button } from '@/components/ui/button'
import { Checkbox } from '@/components/ui/checkbox'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

definePageMeta({ layout: 'blank' })
useHead({ title: "Log in · Sa'Bai" })

const route = useRoute()
const queryClient = useQueryClient()

/** `password` first; `code` when the account has two-factor on. */
const step = ref<'password' | 'code'>('password')
const useRecoveryCode = ref(false)

const userName = ref('')
const password = ref('')
const rememberMe = ref(false)
const code = ref('')
const rememberMachine = ref(false)
const error = ref<string | null>(null)

const signIn = useMutation({
  mutationFn: (): Promise<SaBaiAPIEndpointsAuthSignInResponse> => {
    if (step.value === 'password') {
      return saBaiAPIEndpointsAuthLoginPostEndpoint({
        ...apiConfig,
        body: { userName: userName.value, password: password.value, rememberMe: rememberMe.value },
        throwOnError: true,
      }).unwrap()
    }
    if (useRecoveryCode.value) {
      return saBaiAPIEndpointsAuthLoginRecoveryCodePostEndpoint({
        ...apiConfig,
        body: { recoveryCode: code.value },
        throwOnError: true,
      }).unwrap()
    }
    return saBaiAPIEndpointsAuthLoginTwoFactorPostEndpoint({
      ...apiConfig,
      body: { code: code.value, rememberMe: rememberMe.value, rememberMachine: rememberMachine.value },
      throwOnError: true,
    }).unwrap()
  },
})

async function submit() {
  error.value = null
  try {
    const result = await signIn.mutateAsync()
    if (result.requiresTwoFactor || !result.user) {
      step.value = 'code'
      code.value = ''
      return
    }
    queryClient.setQueryData(queryKeys.currentUser(), result.user)
    const redirectTo = route.query.redirectTo
    await navigateTo(typeof redirectTo === 'string' && redirectTo.startsWith('/') ? redirectTo : paths.home())
  }
  catch (err) {
    const status = getApiErrorStatus(err)
    if (step.value === 'code' && status === 401)
      error.value = useRecoveryCode.value ? 'That recovery code is not valid.' : 'That code is not valid. Check the time on your phone and try again.'
    else if (status === 401)
      error.value = 'Invalid username or password.'
    else
      error.value = getApiErrorMessage(err, 'Unable to sign in.')
  }
}

function backToPassword() {
  step.value = 'password'
  useRecoveryCode.value = false
  code.value = ''
  password.value = ''
  error.value = null
}
</script>

<template>
  <div class="flex min-h-dvh flex-col bg-background">
    <header class="flex items-center gap-2 bg-chrome px-4 py-2 text-chrome-foreground">
      <img src="/favicon.ico" alt="" class="size-6">
      <span class="text-sm font-bold">Project Sa'bai</span>
    </header>
    <main class="flex flex-1 items-start justify-center px-4 py-10 sm:items-center">
      <div class="w-full max-w-sm border border-border bg-card">
        <div class="border-b border-band-border bg-band px-4 py-2">
          <h1 class="text-sm font-bold text-chrome">
            {{ step === 'password' ? 'Log in to Sabai' : 'Two-factor authentication' }}
          </h1>
        </div>
        <form class="flex flex-col gap-3 p-4" @submit.prevent="submit">
          <template v-if="step === 'password'">
            <div class="flex flex-col gap-1">
              <Label for="username" class="font-semibold">Username or email</Label>
              <Input
                id="username"
                v-model="userName"
                autocomplete="username"
                autocapitalize="none"
                spellcheck="false"
                required
                autofocus
              />
            </div>
            <div class="flex flex-col gap-1">
              <Label for="password" class="font-semibold">Password</Label>
              <Input
                id="password"
                v-model="password"
                type="password"
                autocomplete="current-password"
                required
                placeholder="••••••••"
              />
            </div>
            <div class="flex items-center gap-2">
              <Checkbox id="remember-me" v-model="rememberMe" />
              <Label for="remember-me">Keep me signed in on this device</Label>
            </div>
          </template>

          <template v-else>
            <p class="text-xs text-muted-foreground touch:text-sm">
              {{ useRecoveryCode
                ? 'Enter one of the recovery codes you saved when you turned on two-factor. Each code works once.'
                : 'Open your authenticator app and enter the 6-digit code for Sa\'Bai.' }}
            </p>
            <div class="flex flex-col gap-1">
              <Label for="code" class="font-semibold">{{ useRecoveryCode ? 'Recovery code' : 'Authentication code' }}</Label>
              <Input
                id="code"
                v-model="code"
                :inputmode="useRecoveryCode ? 'text' : 'numeric'"
                autocomplete="one-time-code"
                autocapitalize="none"
                spellcheck="false"
                :maxlength="useRecoveryCode ? 32 : 7"
                required
                autofocus
                class="font-mono tracking-widest"
              />
            </div>
            <div v-if="!useRecoveryCode" class="flex items-center gap-2">
              <Checkbox id="remember-machine" v-model="rememberMachine" />
              <Label for="remember-machine">Don't ask for a code on this device again</Label>
            </div>
          </template>

          <div
            v-if="error"
            role="alert"
            class="flex gap-2 border border-destructive/40 bg-destructive-subtle p-2 text-xs text-destructive touch:text-sm"
          >
            <PhWarningOctagon class="mt-px size-4 shrink-0" aria-hidden="true" />
            <div>
              <p class="font-bold">
                Login failed
              </p>
              <p>{{ error }}</p>
            </div>
          </div>
          <Button type="submit" class="w-full" :disabled="signIn.isPending.value">
            <PhSignIn aria-hidden="true" />
            {{ signIn.isPending.value ? 'Signing in...' : step === 'password' ? 'Continue' : 'Verify' }}
          </Button>

          <div v-if="step === 'code'" class="flex flex-wrap items-center justify-between gap-2 text-xs touch:text-sm">
            <button type="button" class="inline-flex items-center gap-1 text-primary underline-offset-2 hover:underline" @click="backToPassword">
              <PhArrowLeft class="size-3.5" aria-hidden="true" /> Back
            </button>
            <button
              type="button"
              class="text-primary underline-offset-2 hover:underline"
              @click="useRecoveryCode = !useRecoveryCode; code = ''; error = null"
            >
              {{ useRecoveryCode ? 'Use authenticator code' : 'Use a recovery code' }}
            </button>
          </div>
          <p v-else class="text-xs text-muted-foreground touch:text-sm">
            Forgot your password? Ask a clinic admin to set a new one.
          </p>
        </form>
      </div>
    </main>
  </div>
</template>
