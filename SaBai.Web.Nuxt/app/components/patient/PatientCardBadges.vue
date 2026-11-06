<script setup lang="ts">
import { Badge } from '@/components/ui/badge'

/** POOR / BS2 / Sabai card flags; only the cards the patient holds are shown. */
const props = defineProps<{ patient: { hasPoorCard: boolean, hasBS2Card: boolean, hasSabaiCard: boolean } }>()
</script>

<template>
  <Badge v-if="props.patient.hasPoorCard" variant="outline" class="border-warning-border bg-warning-subtle text-warning">
    Poor
  </Badge>
  <Badge v-if="props.patient.hasBS2Card" variant="outline" class="border-band-border bg-accent text-accent-foreground">
    BS2
  </Badge>
  <Badge v-if="props.patient.hasSabaiCard" variant="outline" class="border-success/40 bg-success-subtle text-success">
    Sabai
  </Badge>
</template>
