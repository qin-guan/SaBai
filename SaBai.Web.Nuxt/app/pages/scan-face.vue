<script setup lang="ts">
import { PhUserPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'

/**
 * Front desk: find the patient by face or by searching (name, ID, phone), then
 * start a visit for them — or register a new patient.
 */
type Mode = 'matching' | 'registering'
const image = ref<string | null>(null)
const mode = ref<Mode>('matching')
const { selectedVillage, isLoading } = useVillageCode()
useKeyShortcut('n', () => { mode.value = 'registering' }, { enabled: () => mode.value === 'matching' && !image.value })
useHead({ title: "Scan Face · Sa'Bai" })
</script>

<template>
  <div>
    <PageHeader title="Scan Face" :breadcrumbs="[{ label: 'Home', to: '/' }, { label: 'Scan Face' }]" />
    <StateMessage
      v-if="!isLoading && !selectedVillage"
      kind="warning"
      class="mb-3"
      message="No village selected. Choose the village for new patients & visits before starting a visit or registering."
    />
    <div class="grid grid-cols-1 gap-3 lg:grid-cols-[minmax(0,26rem)_minmax(0,1fr)] lg:items-start">
      <AppPanel title="Capture" class="lg:sticky lg:top-16">
        <WebcamCapture v-model="image" :size="400" />
      </AppPanel>
      <div class="flex min-w-0 flex-col gap-3">
        <PatientSearch v-if="mode === 'matching'" />
        <RegistrationForm v-if="mode === 'registering'" v-model:image="image" @match="mode = 'matching'" />
        <MatchingPatients v-else-if="image" :key="image" :image="image" @register="mode = 'registering'" />
        <AppPanel v-else title="Face matches">
          <template #actions>
            <Button size="sm" @click="mode = 'registering'">
              <PhUserPlus aria-hidden="true" /> Register New Patient <ShortcutHint class="hidden kb:inline-flex" keys="N" />
            </Button>
          </template>
          <StateMessage message="Capture a photo to search for matching patients." />
        </AppPanel>
      </div>
    </div>
  </div>
</template>
