<script setup lang="ts">
import { PhFilePdf } from '@phosphor-icons/vue'
import { Button } from '@/components/ui/button'
import { paths } from '@/utils/paths'

const props = withDefaults(
  defineProps<{
    consultId: number
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
      :title="`Open consult #${props.consultId} PDF report`"
      :aria-label="`Open consult #${props.consultId} PDF report`"
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
