<script setup lang="ts">
import { PhSignIn, PhWarningOctagon } from '@phosphor-icons/vue'
import { useMutation } from '@tanstack/vue-query'
import { saBaiAPIEndpointsAuthLoginPostEndpoint } from '@sabai/sdk/clients'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

definePageMeta({ layout: 'blank' })
useHead({ title: "Log in · Sa'Bai" })

const route = useRoute()
const email = ref('')
const password = ref('')
const error = ref<string | null>(null)

const login = useMutation({
  mutationFn: (body: { email: string, password: string }) =>
    saBaiAPIEndpointsAuthLoginPostEndpoint({ ...apiConfig, body, throwOnError: true }).unwrap(),
})

async function submit() {
  error.value = null
  try {
    const result = await login.mutateAsync({ email: email.value, password: password.value })
    setToken(result.accessToken, result.expiresAt)
    const redirectTo = route.query.redirectTo
    await navigateTo(typeof redirectTo === 'string' && redirectTo.startsWith('/') ? redirectTo : paths.home())
  }
  catch (err) {
    error.value = getApiErrorMessage(err, 'Invalid email or password.')
  }
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
            Log in to Sabai
          </h1>
        </div>
        <form class="flex flex-col gap-3 p-4" @submit.prevent="submit">
          <div class="flex flex-col gap-1">
            <Label for="email" class="font-semibold">Email address</Label>
            <Input
              id="email"
              v-model="email"
              type="email"
              autocomplete="email"
              required
              autofocus
              placeholder="you@example.com"
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
          <div
            v-if="error"
            role="alert"
            class="flex gap-2 border border-destructive/40 bg-destructive-subtle p-2 text-xs text-destructive"
          >
            <PhWarningOctagon class="mt-px size-4 shrink-0" aria-hidden="true" />
            <div>
              <p class="font-bold">
                Login failed
              </p>
              <p>{{ error }}</p>
            </div>
          </div>
          <Button type="submit" class="w-full" :disabled="login.isPending.value">
            <PhSignIn aria-hidden="true" />
            {{ login.isPending.value ? 'Signing in...' : 'Continue' }}
          </Button>
        </form>
      </div>
    </main>
  </div>
</template>
