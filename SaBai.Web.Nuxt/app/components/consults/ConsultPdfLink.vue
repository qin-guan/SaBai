<script setup lang="ts">
import { PhFilePdf } from '@phosphor-icons/vue'
import { toast } from 'vue-sonner'
import { Button } from '@/components/ui/button'
import { paths } from '@/utils/paths'

const props = withDefaults(
  defineProps<{
    consultId: string | number
    variant?: 'default' | 'outline' | 'ghost' | 'secondary'
    size?: 'default' | 'sm' | 'icon'
    label?: string
    showLabel?: boolean
  }>(),
  {
    variant: 'outline',
    size: 'sm',
    label: 'Consult PDF',
    showLabel: true,
  },
)

const { isOnline } = useSyncState()

function onClick(event: MouseEvent) {
  if (!isOnline.value) {
    event.preventDefault()
    toast.warning('PDF reports require an internet connection.')
  }
}
</script>

<template>
  <Button
    as-child
    :variant="props.variant"
    :size="props.size"
    class="gap-1.5 touch:h-9"
  >
    <a
      :href="paths.consultPdf(props.consultId)"
      target="_blank"
      rel="noopener noreferrer"
      :title="isOnline ? `Open consult PDF report` : 'PDF reports require an internet connection'"
      :aria-label="`Open consult PDF report`"
      @click="onClick"
    >
      <PhFilePdf
        class="size-4 shrink-0"
        :class="props.variant !== 'default' && 'text-destructive'"
        aria-hidden="true"
      />
      <span v-if="props.showLabel">{{ props.label }}</span>
    </a>
  </Button>
</template>
