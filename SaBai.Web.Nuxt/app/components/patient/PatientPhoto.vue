<script setup lang="ts">
import { PhUser } from '@phosphor-icons/vue'

/**
 * A patient's photo (fetched through the authenticated API), or a placeholder
 * when they have none. `hasPhoto` skips the request when no photo exists.
 */
const props = withDefaults(
  defineProps<{ patientId: number, hasPhoto?: boolean, size?: number, alt?: string }>(),
  { hasPhoto: false, size: 48 },
)
const url = usePatientPhoto(() => props.patientId, () => props.hasPhoto)
</script>

<template>
  <img
    v-if="url"
    :src="url"
    :alt="props.alt ?? 'Patient photo'"
    :width="props.size"
    :height="props.size"
    class="shrink-0 border border-border bg-muted object-cover"
    :style="{ width: `${props.size}px`, height: `${props.size}px` }"
  >
  <span
    v-else
    role="img"
    :aria-label="props.alt ?? 'No patient photo'"
    class="inline-flex shrink-0 items-center justify-center border border-border bg-muted text-muted-foreground"
    :style="{ width: `${props.size}px`, height: `${props.size}px` }"
  >
    <PhUser :size="Math.round(props.size * 0.5)" aria-hidden="true" />
  </span>
</template>
