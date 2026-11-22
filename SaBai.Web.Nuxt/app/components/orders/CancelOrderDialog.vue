<script setup lang="ts">
import { PhX } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import type { MedicationOrder } from '~/offline/mutations/medication'

/** Confirms cancelling a pending order; its reserved stock becomes available again. */
const props = defineProps<{
  order: MedicationOrder
  unit?: string
  visitId?: string
  patientName?: string
}>()
const emit = defineEmits<{ close: [] }>()

const updateOrderMutation = useUpdateOrder()
const serverError = ref<string | null>(null)

async function confirm() {
  if (updateOrderMutation.isPending.value)
    return
  serverError.value = null
  try {
    const outcome = await updateOrderMutation.mutateAsync({
      id: props.order.id,
      changeId: uuidv7(),
      status: 'CANCELLED',
      before: orderSnapshot(props.order),
      visitId: props.visitId,
      patientName: props.patientName,
    })
    notifySaved(outcome, `Cancelled ${props.order.activeIngredientName}.`)
    emit('close')
  }
  catch (error) {
    serverError.value = getApiErrorMessage(error, 'Failed to cancel the order.')
  }
}
</script>

<template>
  <AppDialog title="Cancel this order?" size="sm" @close="emit('close')">
    <dl class="grid grid-cols-[7rem_1fr] gap-y-1">
      <dt class="text-muted-foreground">
        Medication
      </dt>
      <dd class="font-semibold">
        {{ order.activeIngredientName }} × {{ order.quantity }}{{ formatQuantityUnit(unit) }}
      </dd>
      <dt class="text-muted-foreground">
        Dosage
      </dt>
      <dd>{{ order.dosageInstructions }}</dd>
      <template v-if="patientName">
        <dt class="text-muted-foreground">
          Patient
        </dt>
        <dd>{{ patientName }}</dd>
      </template>
    </dl>
    <p class="mt-2 text-xs text-muted-foreground touch:text-sm">
      The stock reserved for it becomes available again. A cancelled order cannot be reopened.
    </p>
    <p v-if="serverError" class="mt-2 text-xs font-medium text-destructive touch:text-sm" role="alert">
      {{ serverError }}
    </p>
    <template #footer>
      <Button variant="outline" @click="emit('close')">
        Keep order
      </Button>
      <Button variant="destructive" :disabled="updateOrderMutation.isPending.value" @click="confirm">
        <PhX aria-hidden="true" /> Cancel order
      </Button>
    </template>
  </AppDialog>
</template>
