<script setup lang="ts">
import { PhClockCounterClockwise, PhPlus } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Input } from '@/components/ui/input'
import { Textarea } from '@/components/ui/textarea'

/**
 * HIPAA controls on a patient record: privacy consents (notice of privacy
 * practices, face recognition, data sharing), the accounting of disclosures to
 * outside parties, and, for admins, a link to who has accessed this record.
 */
const props = defineProps<{ patientId: string }>()
const { user, isAdmin } = useCurrentUser()
const roles = computed(() => user.value?.roles)
const showDisclosures = computed(() => canViewDisclosures(roles.value))
const canRecord = computed(() => canRecordDisclosures(roles.value))

const { data: consents, isLoading: consentsLoading, isError: consentsError, error: consentsErr } = usePatientConsents(() => props.patientId)
const current = computed(() => currentConsents(consents.value ?? []))
const recordConsent = useRecordConsent(() => props.patientId)

async function setConsent(type: ConsentType, granted: boolean) {
  try {
    notifySaved(await recordConsent.mutateAsync({ consentType: type, granted }), granted ? 'Consent recorded' : 'Consent revoked')
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Unable to save consent.'))
  }
}

const { data: disclosures, isLoading: disclosuresLoading, isError: disclosuresError, error: disclosuresErr }
  = usePatientDisclosures(() => props.patientId, showDisclosures)
const recordDisclosure = useRecordDisclosure(() => props.patientId)
const adding = ref(false)
const form = ref({ recipient: '', purpose: '', description: '' })
const formError = ref('')

async function submitDisclosure() {
  if (recordDisclosure.isPending.value)
    return
  if (!form.value.recipient.trim() || !form.value.purpose.trim()) {
    formError.value = 'Recipient and purpose are required.'
    return
  }
  formError.value = ''
  try {
    notifySaved(await recordDisclosure.mutateAsync(form.value), 'Disclosure recorded')
    form.value = { recipient: '', purpose: '', description: '' }
    adding.value = false
  }
  catch (err) {
    toast.error(getApiErrorMessage(err, 'Unable to record disclosure.'))
  }
}

const dateFormat = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })
const fmt = (iso: string) => dateFormat.format(new Date(iso))
const types = Object.keys(CONSENT_LABELS) as ConsentType[]
</script>

<template>
  <AppPanel title="Privacy & disclosures" description="HIPAA consents and accounting of disclosures" collapsible collapse-key="patient-privacy">
    <template v-if="isAdmin" #headerActions>
      <Button as-child size="sm" variant="outline">
        <NuxtLink :to="paths.settingsAuditLog({ entityType: 'Patient', entityId: patientId })">
          <PhClockCounterClockwise aria-hidden="true" />
          <span class="max-sm:hidden">Who accessed this record</span>
          <span class="sm:hidden">Access log</span>
        </NuxtLink>
      </Button>
    </template>

    <h3 class="mb-1 text-xs font-bold text-chrome uppercase tracking-wide">
      Consents
    </h3>
    <StateMessage v-if="consentsLoading" kind="loading" message="Loading consents..." />
    <StateMessage v-else-if="consentsError && !consents" kind="error" :message="describeLoadError(consentsErr, 'Unable to load consents.')" />
    <ul v-else class="divide-y divide-border border border-border">
      <li v-for="type in types" :key="type" class="flex flex-wrap items-center justify-between gap-2 px-3 py-2">
        <div class="min-w-0">
          <p class="font-semibold">
            {{ CONSENT_LABELS[type] }}
          </p>
          <p class="text-xs text-muted-foreground touch:text-sm">
            <template v-if="current.get(type)">
              {{ current.get(type)!.granted ? 'Granted' : 'Revoked' }} {{ fmt(current.get(type)!.recordedAt) }}
            </template>
            <template v-else>
              Not recorded
            </template>
          </p>
        </div>
        <div class="flex items-center gap-2">
          <Badge
            v-if="current.get(type)"
            variant="outline"
            :class="current.get(type)!.granted ? 'border-success/40 bg-success-subtle text-success' : 'border-destructive/40 bg-destructive-subtle text-destructive'"
          >
            {{ current.get(type)!.granted ? 'Granted' : 'Revoked' }}
          </Badge>
          <Button
            size="sm"
            variant="outline"
            :disabled="recordConsent.isPending.value"
            @click="setConsent(type, !(current.get(type)?.granted ?? false))"
          >
            {{ current.get(type)?.granted ? 'Revoke' : 'Record consent' }}
          </Button>
        </div>
      </li>
    </ul>

    <template v-if="showDisclosures">
      <div class="mt-4 mb-1 flex items-center justify-between gap-2">
        <h3 class="text-xs font-bold text-chrome uppercase tracking-wide">
          Disclosures to outside parties
        </h3>
        <Button v-if="canRecord && !adding" size="sm" variant="outline" @click="adding = true">
          <PhPlus aria-hidden="true" /> Record disclosure
        </Button>
      </div>

      <form v-if="adding" class="mb-3 flex flex-col gap-2 border border-border p-3" @submit.prevent="submitDisclosure">
        <FormField v-slot="{ id }" label="Recipient" :error="formError && !form.recipient.trim() ? formError : undefined">
          <Input :id="id" v-model="form.recipient" maxlength="255" placeholder="Who received the information" />
        </FormField>
        <FormField v-slot="{ id }" label="Purpose" :error="formError && !form.purpose.trim() ? formError : undefined">
          <Input :id="id" v-model="form.purpose" maxlength="255" placeholder="Why it was shared" />
        </FormField>
        <FormField v-slot="{ id }" label="What was shared (optional)">
          <Textarea :id="id" v-model="form.description" />
        </FormField>
        <div class="flex justify-end gap-2">
          <Button type="button" variant="outline" @click="adding = false">
            Cancel
          </Button>
          <Button type="submit" :disabled="recordDisclosure.isPending.value">
            {{ recordDisclosure.isPending.value ? 'Saving…' : 'Save disclosure' }}
          </Button>
        </div>
      </form>

      <StateMessage v-if="disclosuresLoading" kind="loading" message="Loading disclosures..." />
      <StateMessage v-else-if="disclosuresError && !disclosures" kind="error" :message="describeLoadError(disclosuresErr, 'Unable to load disclosures.')" />
      <StateMessage v-else-if="!disclosures?.length" message="No disclosures recorded for this patient." />
      <ul v-else class="divide-y divide-border border border-border">
        <li v-for="d in disclosures" :key="d.id" class="px-3 py-2">
          <div class="flex flex-wrap items-baseline justify-between gap-2">
            <p class="font-semibold">
              {{ d.recipient }}
            </p>
            <p class="text-xs text-muted-foreground touch:text-sm">
              {{ fmt(d.disclosedAt) }}
            </p>
          </div>
          <p>{{ d.purpose }}</p>
          <p v-if="d.description" class="text-xs text-muted-foreground touch:text-sm">
            {{ d.description }}
          </p>
        </li>
      </ul>
    </template>
  </AppPanel>
</template>
