<script setup lang="ts">
import { PhPlus } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'

/**
 * "New Visit" for one patient: starts a visit in the selected village and
 * opens vitals, confirming first if a visit was opened recently.
 */
const props = withDefaults(defineProps<{
  patientId: number
  name: string
  size?: 'default' | 'sm'
}>(), { size: 'default' })

const { start, busy, villageLoading, creating, duplicate, confirm, cancel } = useStartVisit()
</script>

<template>
  <Button
    variant="success"
    :size="props.size"
    :disabled="busy || villageLoading"
    :aria-label="`Start a new visit for ${name}`"
    @click.stop.prevent="start(patientId)"
  >
    <PhPlus aria-hidden="true" /> {{ busy ? 'Starting…' : 'New Visit' }}
  </Button>

  <AppDialog v-if="duplicate" title="Duplicate Visit" size="sm" @close="cancel">
    <p>
      A visit was just opened for {{ name }}<template v-if="duplicate.existingVisitDate">
        at {{ duplicate.existingVisitDate.toLocaleTimeString() }}
      </template>. Start another visit anyway, or cancel to keep using it?
    </p>
    <template #footer>
      <Button variant="outline" @click="cancel">
        Cancel
      </Button>
      <Button variant="success" :disabled="creating" @click="confirm">
        {{ creating ? 'Creating...' : 'Confirm' }}
      </Button>
    </template>
  </AppDialog>
</template>
